using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Naming;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;
using CaptureMode = GodSaveTheScreen.Core.Capture.CaptureMode;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Eintrag der Zoom-Auswahl; <see cref="Factor"/> = <c>null</c> steht für „Einpassen“.</summary>
public sealed record ZoomChoice(string Label, double? Factor);

/// <summary>ViewModel eines Editorfensters: Dokument, Werkzeuge, Zoom, Datei- und Zwischenablage-Befehle.</summary>
public sealed partial class EditorViewModel : ObservableObject, IDisposable
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 32;
    private const double PasteOffset = 10;

    private static readonly double[] ZoomSteps = [0.1, 0.25, 0.33, 0.5, 0.66, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32];

    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly IImageCodecService _codec;
    private readonly IDocumentExporter _exporter;
    private readonly IEditorService _editors;
    private readonly ISettingsService _settings;
    private readonly ICaptureLauncher _capture;
    private readonly IProjectService _projects;
    private readonly IRecentFilesService _recents;
    private readonly ILogger<EditorViewModel> _logger;
    private bool _blurHintShown;
    private ToolBase? _toolBeforePipette;
    private int _pasteCount;
    private string? _lastPasted;

    public EditorViewModel(
        EditorContext context,
        ToolRegistry tools,
        IDialogService dialogs,
        IClipboardService clipboard,
        IImageCodecService codec,
        IDocumentExporter exporter,
        IEditorService editors,
        ISettingsService settings,
        ICaptureLauncher capture,
        IProjectService projects,
        IRecentFilesService recents,
        ILogger<EditorViewModel> logger)
    {
        Context = context;
        Tools = tools;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _codec = codec;
        _exporter = exporter;
        _editors = editors;
        _settings = settings;
        _capture = capture;
        _projects = projects;
        _recents = recents;
        _logger = logger;
        _recents.Changed += OnRecentsChanged;

        _activeTool = tools.Get<SelectTool>();
        Properties = new SelectionPropertiesViewModel(context, () => ActiveTool, StartPipette);
        Layers = new LayersViewModel(context);
        SaveOptions = new SaveOptionsViewModel(settings);
        TextProperties = new TextPropertiesViewModel(context, () => ActiveTool);
        MarkerProperties = new MarkerPropertiesViewModel(context, () => ActiveTool);
        ToolOptions = new ToolOptionsViewModel(context, () => ActiveTool);
        EmojiProperties = new EmojiPropertiesViewModel(context, () => ActiveTool);
        StampProperties = new StampPropertiesViewModel(context, () => ActiveTool);
        ShadowProperties = new ShadowPropertiesViewModel(context, () => ActiveTool);
        MaskTools = new MaskToolsViewModel(context, () => ActiveTool, tools.Tools.OfType<BackgroundEraserTool>().FirstOrDefault());
        EmojiPicker = new EmojiPickerViewModel(context);
        Presets = new PresetListViewModel(context, tools, () => ActiveTool, dialogs, RefreshProperties);
        Crop = new CropViewModel(context, () => tools.Get<CropTool>());
        context.Ruler.PropertyChanged += OnRulerChanged;
        context.Crop.Changed += OnCropChanged;
        ZoomChoices =
        [
            new ZoomChoice(Strings.Editor_Fit, null),
            .. new[] { 0.1, 0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4, 8, 16, 32 }.Select(z => new ZoomChoice(FormatZoom(z), z)),
        ];

        context.ActivateToolHandler = ActivateToolById;
        context.SelectionStyleEdited += OnSelectionStyleEdited;
        Document.PropertyChanged += OnDocumentChanged;
        Context.Undo.Changed += OnUndoChanged;
        Context.Selection.Changed += OnSelectionChanged;
        _activeTool.Activate();
    }

    public EditorContext Context { get; }

    public ImageDocument Document => Context.Document;

    public ToolRegistry Tools { get; }

    public SelectionPropertiesViewModel Properties { get; }

    public LayersViewModel Layers { get; }

    public SaveOptionsViewModel SaveOptions { get; }

    public TextPropertiesViewModel TextProperties { get; }

    public MarkerPropertiesViewModel MarkerProperties { get; }

    public ToolOptionsViewModel ToolOptions { get; }

    /// <summary>Größe, Drehung und Spiegelung der Emojis bzw. Vorgaben des Emoji-Werkzeugs.</summary>
    public EmojiPropertiesViewModel EmojiProperties { get; }

    /// <summary>Stempeltext, Schrift, Rahmen, Abnutzung und Neigung bzw. Vorgaben des Stempelwerkzeugs.</summary>
    public StampPropertiesViewModel StampProperties { get; }

    /// <summary>Schatten der Auswahl bzw. Vorgaben des aktiven Werkzeugs.</summary>
    public ShadowPropertiesViewModel ShadowProperties { get; }

    /// <summary>Emoji-Auswahl mit Suche, Kategorien und Hautton.</summary>
    public EmojiPickerViewModel EmojiPicker { get; }

    public PresetListViewModel Presets { get; }

    /// <summary>Bereichsauswahl und Hintergrundradierer (entfernte Bereiche des Screenshots).</summary>
    public MaskToolsViewModel MaskTools { get; }

    public CropViewModel Crop { get; }

    /// <summary>Der Zuschnittmodus ist aktiv: alle übrigen Werkzeuge sind deaktiviert.</summary>
    public bool IsCropping => ActiveTool is CropTool;

    public bool AreToolsEnabled => !IsCropping;

    /// <summary>Angezeigter Bildbereich: der Zuschnitt bzw. die erweiterte Zeichenfläche, im Zuschnittmodus das gesamte Originalbild.</summary>
    public PixelRect ViewRect => IsCropping ? Document.BaseImage.Bounds : Document.EffectiveBounds;

    /// <summary>Seitenleiste sichtbar (im Zuschnittmodus immer, wegen der Zuschnitt-Einstellungen).</summary>
    public bool IsSidebarVisible => IsPanelOpen || IsCropping;

    public bool IsPropertyPanelVisible => IsPanelOpen && !IsCropping;

    /// <summary>Virtuelles Lineal ein- oder ausblenden (beim Einblenden zentriert im sichtbaren Bereich).</summary>
    public bool IsRulerVisible
    {
        get => Context.Ruler.IsVisible;
        set => SetRulerVisible(value);
    }

    /// <summary>Überschrift des Eigenschaftenbereichs: Objektname, „N Objekte“ oder „Standardwerte: Werkzeug“.</summary>
    public string PanelHeader
    {
        get
        {
            var items = Context.Selection.Items;
            if (items.Count == 1)
            {
                return AnnotationNames.DisplayName(items[0]);
            }

            if (items.Count > 1)
            {
                return string.Format(CultureInfo.CurrentUICulture, Strings.Props_Objects, items.Count);
            }

            return ActiveTool.HasStyleDefaults
                ? string.Format(CultureInfo.CurrentUICulture, Strings.Props_DefaultsFor, ActiveTool.DisplayName)
                : MaskTools.IsApplicable ? ActiveTool.DisplayName : string.Empty;
        }
    }

    /// <summary>Im Eigenschaftenbereich gibt es etwas zu bearbeiten.</summary>
    public bool HasPanelContent =>
        Properties.IsApplicable || TextProperties.IsApplicable || MarkerProperties.IsApplicable || ToolOptions.IsApplicable || EmojiProperties.IsApplicable || StampProperties.IsApplicable || ShadowProperties.IsApplicable || MaskTools.IsApplicable || Properties.ShowOpacity || Presets.IsApplicable;

    /// <summary>Das Panel zeigt die Einstellungen des aktiven Werkzeugs (keine Auswahl) und sie lassen sich zurücksetzen.</summary>
    public bool CanResetToolDefaults => Context.Selection.Items.Count == 0 && ToolDefaultsReset.Supports(ActiveTool);

    public IReadOnlyList<ZoomChoice> ZoomChoices { get; }

    /// <summary>Hinweis über der Zeichenfläche (z. B. mehrseitige Bilddatei).</summary>
    [ObservableProperty]
    private string? _infoMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveToolId))]
    [NotifyPropertyChangedFor(nameof(IsCropping))]
    [NotifyPropertyChangedFor(nameof(AreToolsEnabled))]
    [NotifyPropertyChangedFor(nameof(ViewRect))]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    [NotifyPropertyChangedFor(nameof(IsPropertyPanelVisible))]
    [NotifyPropertyChangedFor(nameof(ImageSizeText))]
    private ToolBase _activeTool;

    public string ActiveToolId => ActiveTool.Id;

    /// <summary>Zoomfaktor: Bildschirmpixel je Bildpixel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    private double _zoom = 1;

    /// <summary>Zoom passt sich der Fenstergröße an.</summary>
    [ObservableProperty]
    private bool _isFit = true;

    [ObservableProperty]
    private string _pointerText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    [NotifyPropertyChangedFor(nameof(IsPropertyPanelVisible))]
    private bool _isPanelOpen = true;

    public string ZoomText
    {
        get => FormatZoom(Zoom);
        set
        {
            var digits = new string([.. value.Where(c => char.IsDigit(c) || c is ',' or '.')]);
            if (double.TryParse(digits.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0)
            {
                SetZoom(percent / 100);
            }
            else if (string.Equals(value, Strings.Editor_Fit, StringComparison.OrdinalIgnoreCase))
            {
                Fit();
            }
        }
    }

    public string ImageSizeText
    {
        get
        {
            var size = IsCropping ? Context.Crop.Rect : Document.EffectiveBounds;
            return string.Format(CultureInfo.CurrentUICulture, Strings.Editor_ImageSize, size.Width, size.Height);
        }
    }

    public string WindowTitle
    {
        get
        {
            return $"{DisplayName}{(Document.IsDirty ? " *" : string.Empty)} – {Strings.App_Name}";
        }
    }

    /// <summary>Projektname, sonst Dateiname des Bilds, sonst „Unbenannt“.</summary>
    public string DisplayName =>
        !string.IsNullOrEmpty(Document.ProjectPath) ? Path.GetFileName(Document.ProjectPath)
        : !string.IsNullOrEmpty(Document.SourcePath) ? Path.GetFileName(Document.SourcePath)
        : Strings.Editor_Untitled;

    /// <summary>Zuletzt verwendete Dateien (für das Menü „Öffnen ▾“).</summary>
    public IReadOnlyList<RecentFile> RecentItems => _recents.Items;

    public bool CanUndo => Context.Undo.CanUndo;

    public bool CanRedo => Context.Undo.CanRedo;

    public bool HasSelection => !Context.Selection.IsEmpty;

    public bool IsJpegLike => _settings.Current.LastSaveFormat is ImageFormatSetting.Jpeg or ImageFormatSetting.JpegXr;

    // ---- Werkzeuge ----

    [RelayCommand]
    private void ActivateTool(ToolBase? tool)
    {
        if (tool is not null)
        {
            Activate(tool);
        }
    }

    /// <summary>Geänderte Werte der Auswahl werden die Standardwerte ihres Werkzeugs und bleiben so beim Werkzeugwechsel erhalten.</summary>
    private void OnSelectionStyleEdited(object? sender, EventArgs e)
    {
        if (PresetStyles.Resolve(Context.Selection.Items, ActiveTool) is { } id && Tools.Find(id) is { } owner)
        {
            PresetStyles.RememberSelectionAsDefaults(Context, owner);
        }
    }

    public void Activate(ToolBase tool)
    {
        if (ReferenceEquals(tool, ActiveTool))
        {
            return;
        }

        ActiveTool.Deactivate();
        if (tool.HasStyleDefaults)
        {
            // Das Panel soll die Standardwerte des Werkzeugs zeigen.
            Context.Selection.Clear();
        }

        ActiveTool = tool;
        tool.Activate();
        RefreshProperties();
        Context.InvalidateInteraction();
    }

    [RelayCommand]
    private void ResetToolDefaults()
    {
        var tool = ActiveTool;
        if (!CanResetToolDefaults
            || !_dialogs.Confirm(Strings.Props_ResetDefaults, string.Format(CultureInfo.CurrentUICulture, Strings.Props_ResetDefaultsConfirm, tool.DisplayName)))
        {
            return;
        }

        ToolDefaultsReset.Reset(Context, tool);
        RefreshProperties();
    }

    [RelayCommand]
    private void ToggleRuler() => SetRulerVisible(!Context.Ruler.IsVisible);

    private void SetRulerVisible(bool visible)
    {
        var ruler = Context.Ruler;
        if (ruler.IsVisible == visible)
        {
            return;
        }

        if (visible)
        {
            var image = Document.BaseImage;
            ruler.Center = Context.VisibleCenterProvider?.Invoke() ?? new PointD(image.Width / 2.0, image.Height / 2.0);
            ruler.Length = Math.Clamp(image.Width * 0.6, 200, 2000);
        }

        ruler.IsVisible = visible;
    }

    private void OnRecentsChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(RecentItems));

    private void OnCropChanged(object? sender, EventArgs e)
    {
        if (IsCropping)
        {
            OnPropertyChanged(nameof(ImageSizeText));
        }
    }

    private void OnRulerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RulerViewModel.IsVisible))
        {
            OnPropertyChanged(nameof(IsRulerVisible));
        }
    }

    private void RefreshProperties()
    {
        Properties.Refresh();
        TextProperties.Refresh();
        MarkerProperties.Refresh();
        ToolOptions.Refresh();
        EmojiProperties.Refresh();
        StampProperties.Refresh();
        ShadowProperties.Refresh();
        MaskTools.Refresh();
        if (EmojiProperties.IsApplicable)
        {
            // Darstellbare Emojis werden erst beim ersten Öffnen der Auswahl im Hintergrund ermittelt.
            _ = EmojiPicker.LoadAsync();
        }

        Presets.Refresh();
        OnPropertyChanged(nameof(PanelHeader));
        OnPropertyChanged(nameof(HasPanelContent));
        OnPropertyChanged(nameof(CanResetToolDefaults));
    }

    private void ActivateToolById(string id)
    {
        if (Tools.Find(id) is { } tool)
        {
            Activate(tool);
        }
    }

    private void StartPipette(Action<ArgbColor> picked)
    {
        var pipette = Tools.Get<PipetteTool>();
        _toolBeforePipette = ActiveTool == pipette ? _toolBeforePipette : ActiveTool;

        void Restore()
        {
            var previous = _toolBeforePipette ?? Tools.Get<SelectTool>();
            _toolBeforePipette = null;
            pipette.Picked = null;
            pipette.Cancelled = null;
            Activate(previous);
        }

        pipette.Picked = color =>
        {
            picked(color);
            Restore();
        };
        pipette.Cancelled = Restore;
        Activate(pipette);
    }

    // ---- Zoom ----

    public void SetZoom(double zoom, bool keepFit = false)
    {
        IsFit = keepFit;
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    [RelayCommand]
    private void Fit() => IsFit = true;

    [RelayCommand]
    private void ActualSize() => SetZoom(1);

    [RelayCommand]
    private void ZoomIn() => SetZoom(ZoomSteps.FirstOrDefault(s => s > Zoom * 1.001, MaxZoom));

    [RelayCommand]
    private void ZoomOut() => SetZoom(ZoomSteps.LastOrDefault(s => s < Zoom * 0.999, MinZoom));

    [RelayCommand]
    private void SelectZoomChoice(ZoomChoice? choice)
    {
        if (choice is null)
        {
            return;
        }

        if (choice.Factor is { } factor)
        {
            SetZoom(factor);
        }
        else
        {
            Fit();
        }
    }

    public void SetPointer(PointD? position)
    {
        // Koordinaten relativ zum Zuschnitt (im Zuschnittmodus zum Originalbild).
        var origin = IsCropping ? default : Document.EffectiveBounds;
        PointerText = position is { } p
            ? string.Format(CultureInfo.CurrentUICulture, Strings.Editor_PointerPosition, (int)Math.Floor(p.X) - origin.X, (int)Math.Floor(p.Y) - origin.Y)
            : string.Empty;
    }

    private static string FormatZoom(double zoom) => string.Format(CultureInfo.CurrentUICulture, Strings.Editor_ZoomPercent, Math.Round(zoom * 100));

    // ---- Datei ----

    [RelayCommand]
    private async Task OpenAsync()
    {
        var files = _dialogs.PickImageFiles();
        if (files.Count > 0)
        {
            await _editors.OpenFilesAsync(files);
        }
    }

    [RelayCommand]
    private async Task SaveAsync() => await TrySaveAsync(forceDialog: false);

    [RelayCommand]
    private async Task SaveAsAsync() => await TrySaveAsync(forceDialog: true);

    /// <summary>„Exportieren…“: Bild in einem Bildformat speichern (merkt sich das Ziel getrennt vom Projekt).</summary>
    [RelayCommand]
    private async Task ExportAsync() => await ExportImageAsync(forceDialog: true);

    [RelayCommand]
    private async Task OpenRecentAsync(RecentFile? file)
    {
        if (file is not null)
        {
            await _editors.OpenFilesAsync([file.Path]);
        }
    }

    [RelayCommand]
    private void ClearRecents() => _recents.Clear();

    [RelayCommand]
    private void RemoveMissingRecents()
    {
        foreach (var item in _recents.Items.Where(i => i.IsMissing))
        {
            _recents.Remove(item.Path);
        }
    }

    /// <summary>
    /// Speichert. Ein bekanntes Projekt wird wieder als Projekt geschrieben, eine geöffnete Bilddatei als Bild;
    /// sonst (oder bei <paramref name="forceDialog"/>) fragt der Dialog – mit dem Projektformat an erster Stelle. <c>false</c> bei Abbruch oder Fehler.
    /// </summary>
    public async Task<bool> TrySaveAsync(bool forceDialog)
    {
        if (!forceDialog && !string.IsNullOrEmpty(Document.ProjectPath))
        {
            return await SaveProjectAsync(Document.ProjectPath);
        }

        var path = Document.SourcePath;
        var format = ImageFormats.FromExtension(Path.GetExtension(path));
        if (!forceDialog && !string.IsNullOrEmpty(path) && format is not null)
        {
            return await SaveImageAsync(path, format.Value);
        }

        return await SaveWithDialogAsync(preferProject: Document.Annotations.Count > 0 || !string.IsNullOrEmpty(Document.ProjectPath));
    }

    private async Task<bool> SaveWithDialogAsync(bool preferProject)
    {
        var settings = _settings.Current;
        var lastFormat = ImageFormats.FromExtension(Path.GetExtension(Document.SourcePath)) ?? settings.LastSaveFormat;
        var directory = string.IsNullOrWhiteSpace(settings.LastSaveDirectory) ? AutoSaveAction.DefaultDirectory : settings.LastSaveDirectory;
        var suggestion = !string.IsNullOrEmpty(Document.ProjectPath)
            ? Path.GetFileName(Document.ProjectPath)
            : SuggestName(settings, lastFormat);
        if (_dialogs.PickDocumentSavePath(suggestion, directory, lastFormat, preferProject) is not { } picked)
        {
            return false;
        }

        return picked.Format is { } imageFormat
            ? await SaveImageAsync(picked.Path, imageFormat)
            : await SaveProjectAsync(picked.Path);
    }

    private async Task<bool> ExportImageAsync(bool forceDialog)
    {
        var settings = _settings.Current;
        var path = Document.SourcePath;
        var format = ImageFormats.FromExtension(Path.GetExtension(path));
        if (forceDialog || string.IsNullOrEmpty(path) || format is null)
        {
            var directory = string.IsNullOrWhiteSpace(settings.LastSaveDirectory) ? AutoSaveAction.DefaultDirectory : settings.LastSaveDirectory;
            var suggestion = SuggestName(settings, format ?? settings.LastSaveFormat);
            if (_dialogs.PickImageSavePath(suggestion, directory, format ?? settings.LastSaveFormat) is not { } picked)
            {
                return false;
            }

            path = picked.Path;
            format = picked.Format;
        }

        return await SaveImageAsync(path, format.Value);
    }

    private async Task<bool> SaveImageAsync(string path, ImageFormatSetting format)
    {
        try
        {
            var image = _exporter.Render(Document);
            var options = ImageSaveOptions.FromSettings(_settings.Current, format);
            await _codec.SaveAsync(image, Document.DpiX, Document.DpiY, path, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _logger.LogError(ex, "Speichern nach {Path} fehlgeschlagen", path);
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_SaveFailed, ex.Message));
            return false;
        }

        Document.SourcePath = path;
        if (string.IsNullOrEmpty(Document.ProjectPath))
        {
            // Der Bildexport eines Projekts ändert nichts am Zustand des Projekts.
            Document.MarkSaved();
        }

        _recents.Add(path);
        RememberSaveTarget(path, format);
        OnPropertyChanged(nameof(WindowTitle));
        _logger.LogInformation("Bild gespeichert: {Path}", path);
        return true;
    }

    /// <summary>Schreibt das Dokument als Projekt (<c>.gsts</c>) und merkt sich den Pfad.</summary>
    public async Task<bool> SaveProjectAsync(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ProjectSerializer.Extension, StringComparison.OrdinalIgnoreCase))
        {
            path += ProjectSerializer.Extension;
        }

        try
        {
            await _projects.SaveAsync(Document, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or ProjectFormatException)
        {
            _logger.LogError(ex, "Speichern des Projekts nach {Path} fehlgeschlagen", path);
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_SaveFailed, ex.Message));
            return false;
        }

        Document.ProjectPath = path;
        Document.MarkSaved();
        _recents.Add(path);
        _settings.Current = _settings.Current with { LastSaveDirectory = Path.GetDirectoryName(path) ?? string.Empty };
        _ = _settings.SaveAsync();
        OnPropertyChanged(nameof(WindowTitle));
        _logger.LogInformation("Projekt gespeichert: {Path}", path);

        if (!_blurHintShown && Document.Annotations.Any(a => a is BlurAnnotation))
        {
            // Das Original bleibt im Projekt unverändert lesbar.
            _blurHintShown = true;
            InfoMessage = Strings.Project_BlurHint;
        }

        return true;
    }

    private void RememberSaveTarget(string path, ImageFormatSetting format)
    {
        _settings.Current = _settings.Current with { LastSaveDirectory = Path.GetDirectoryName(path) ?? string.Empty, LastSaveFormat = format };
        _ = _settings.SaveAsync();
    }

    private static string SuggestName(AppSettings settings, ImageFormatSetting format)
    {
        var context = new FileNameContext(DateTimeOffset.Now, Strings.Editor_FileNameMode);
        var name = FileNamePattern.Render(settings.FileNamePattern, context);
        return name + ImageFormats.Extensions(format)[0];
    }

    /// <summary>Wird beim Schließen des Fensters gefragt; <c>true</c> = Fenster darf schließen.</summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (!Document.IsDirty)
        {
            return true;
        }

        // Objekte gehen in einem Bildformat verloren (nicht mehr bearbeitbar): das Projekt anbieten.
        var offerProject = Document.Annotations.Count > 0 && string.IsNullOrEmpty(Document.ProjectPath);
        return _dialogs.AskSaveChanges(Strings.Editor_UnsavedTitle, string.Format(CultureInfo.CurrentUICulture, Strings.Editor_UnsavedMessage, DisplayName), offerProject) switch
        {
            SaveChangesAnswer.Save => await TrySaveAsync(forceDialog: false),
            SaveChangesAnswer.SaveAsProject => await SaveWithDialogAsync(preferProject: true),
            SaveChangesAnswer.Discard => true,
            _ => false,
        };
    }

    [RelayCommand]
    private Task NewCaptureAsync(CaptureMode mode) => _capture.StartAsync(new CaptureRequest(mode, TimeSpan.Zero));

    [RelayCommand]
    private void NewFromClipboard() => _editors.OpenFromClipboard();

    [RelayCommand]
    private void DismissInfo() => InfoMessage = null;

    // ---- Zwischenablage ----

    /// <summary><c>Strg+C</c>: mit Auswahl die Objekte, sonst das Bild.</summary>
    [RelayCommand]
    private void Copy()
    {
        if (HasSelection)
        {
            CopyObjects();
        }
        else
        {
            CopyImage();
        }
    }

    [RelayCommand]
    private void CopyImage() => RunClipboard(() => _clipboard.SetImage(_exporter.Render(Document)));

    private void CopyObjects()
    {
        var items = Context.Selection.Items.ToList();
        RunClipboard(() => _clipboard.SetAnnotations(items, _exporter.RenderObjects(items)));
        _pasteCount = 0;
        _lastPasted = null;
    }

    [RelayCommand]
    private void Cut()
    {
        if (!HasSelection)
        {
            return;
        }

        CopyObjects();
        DeleteSelection();
    }

    [RelayCommand]
    private void Paste()
    {
        var annotations = _clipboard.GetAnnotations();
        if (annotations.Count > 0)
        {
            PasteObjects(annotations);
            return;
        }

        _editors.OpenFromClipboard();
    }

    private void PasteObjects(IReadOnlyList<Annotation> annotations)
    {
        var key = AnnotationSerializer.Serialize(annotations);
        _pasteCount = key == _lastPasted ? _pasteCount + 1 : 1;
        _lastPasted = key;
        var offset = new VectorD(PasteOffset * _pasteCount, PasteOffset * _pasteCount);
        InsertCopies(annotations, offset, Strings.Editor_PasteObjects);
    }

    private void InsertCopies(IEnumerable<Annotation> originals, VectorD offset, string description)
    {
        var copies = originals.Select(a => a.Clone()).ToList();
        if (copies.Count == 0)
        {
            return;
        }

        using (Context.Undo.BeginTransaction(description))
        {
            foreach (var copy in copies)
            {
                copy.Translate(offset);
                Context.Undo.Execute(new AddAnnotationAction(Document.Annotations, copy));
            }
        }

        Context.Selection.Set(copies);
        if (!ReferenceEquals(ActiveTool, Tools.Get<SelectTool>()))
        {
            ActivateToolById(SelectTool.ToolId);
            Context.Selection.Set(copies);
        }
    }

    private void RunClipboard(Action action)
    {
        try
        {
            action();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger.LogError(ex, "Kopieren in die Zwischenablage fehlgeschlagen");
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_CopyFailed, ex.Message));
        }
    }

    /// <summary>Daten für „Bild aus dem Editor ziehen“: temporäre PNG-Datei plus Bild.</summary>
    public (string TempFile, PixelBuffer Image)? CreateDragPayload()
    {
        try
        {
            var image = _exporter.Render(Document);
            var directory = DragTempFiles.Directory;
            Directory.CreateDirectory(directory);
            var name = string.IsNullOrEmpty(Document.SourcePath)
                ? $"Screenshot {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png"
                : Path.ChangeExtension(Path.GetFileName(Document.SourcePath), ".png");
            var file = Path.Combine(directory, name);
            File.WriteAllBytes(file, _codec.Encode(image, Document.DpiX, Document.DpiY, new ImageSaveOptions(ImageFormatSetting.Png)));
            return (file, image);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Temporäre Datei für Drag & Drop konnte nicht erstellt werden");
            return null;
        }
    }

    // ---- Bearbeiten ----

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Context.Undo.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Context.Undo.Redo();

    [RelayCommand]
    private void DeleteSelection() => Tools.Get<SelectTool>().DeleteSelection();

    [RelayCommand]
    private void SelectAll() => Context.Selection.Set(Document.Annotations.Where(a => a.IsVisible));

    [RelayCommand]
    private void Duplicate()
    {
        if (HasSelection)
        {
            InsertCopies(Context.Selection.Items.OrderBy(a => Document.Annotations.IndexOf(a)).ToList(), new VectorD(PasteOffset, PasteOffset), Strings.Editor_Duplicate);
        }
    }

    [RelayCommand]
    private void ToggleLockSelection()
    {
        var items = Context.Selection.Items.ToList();
        if (items.Count == 0)
        {
            return;
        }

        var lockAll = items.Any(a => !a.IsLocked);
        using (Context.Undo.BeginTransaction(Strings.Editor_Lock))
        {
            foreach (var item in items.Where(a => a.IsLocked != lockAll))
            {
                var annotation = item;
                Context.Undo.Execute(new PropertyChangeAction<bool>(annotation, nameof(Annotation.IsLocked), annotation.IsLocked, lockAll, v => annotation.IsLocked = v, Strings.Editor_Lock));
            }
        }
    }

    [RelayCommand]
    private void BringToFront() => Reorder(OrderOperation.ToFront);

    [RelayCommand]
    private void BringForward() => Reorder(OrderOperation.Forward);

    [RelayCommand]
    private void SendBackward() => Reorder(OrderOperation.Backward);

    [RelayCommand]
    private void SendToBack() => Reorder(OrderOperation.ToBack);

    private void Reorder(OrderOperation operation)
    {
        if (!HasSelection)
        {
            return;
        }

        var action = new ReorderAction(Document.Annotations, ZOrder.Apply([.. Document.Annotations], Context.Selection.Items, operation));
        if (!action.IsNoOp)
        {
            Context.Undo.Execute(action);
        }
    }

    // ---- Zustand ----

    /// <summary>Wählt das oberste Objekt unter dem Punkt (für das Kontextmenü); leer → Auswahl aufheben.</summary>
    public void SelectAt(PointD point)
    {
        var hit = Context.HitTest(point);
        if (hit is null)
        {
            Context.Selection.Clear();
        }
        else if (!Context.Selection.Contains(hit))
        {
            Context.Selection.Set(hit);
        }
    }

    private void OnDocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageDocument.IsDirty) or nameof(ImageDocument.SourcePath) or nameof(ImageDocument.ProjectPath))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
        else if (e.PropertyName is nameof(ImageDocument.EffectiveBounds))
        {
            OnPropertyChanged(nameof(ViewRect));
            OnPropertyChanged(nameof(ImageSizeText));
        }
    }

    private void OnUndoChanged(object? sender, EventArgs e)
    {
        if (IsCropping)
        {
            // Undo/Redo (und das Übernehmen selbst) beendet den Zuschnittmodus.
            Tools.Get<CropTool>().CancelMode();
        }

        OnPropertyChanged(nameof(PanelHeader));
        OnPropertyChanged(nameof(HasPanelContent));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        Context.InvalidateInteraction();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PanelHeader));
        OnPropertyChanged(nameof(HasPanelContent));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanResetToolDefaults));
        Context.InvalidateInteraction();
    }

    public void Dispose()
    {
        ActiveTool.Deactivate();
        Document.PropertyChanged -= OnDocumentChanged;
        Context.Undo.Changed -= OnUndoChanged;
        Context.Selection.Changed -= OnSelectionChanged;
        Context.SelectionStyleEdited -= OnSelectionStyleEdited;
        Properties.Dispose();
        TextProperties.Dispose();
        MarkerProperties.Dispose();
        ToolOptions.Dispose();
        EmojiProperties.Dispose();
        StampProperties.Dispose();
        ShadowProperties.Dispose();
        MaskTools.Dispose();
        EmojiPicker.Dispose();
        Presets.Dispose();
        Crop.Dispose();
        Context.Ruler.PropertyChanged -= OnRulerChanged;
        Context.Crop.Changed -= OnCropChanged;
        _recents.Changed -= OnRecentsChanged;
        EffectCache.Shared.RemoveImage(BaseImageIdentity.Get(Document.BaseImage));
        EffectCache.Shared.RemoveImage(BaseImageIdentity.Get(Document.MaskedImage));
        Layers.Dispose();
        Context.Dispose();
    }
}

public enum OrderOperation
{
    ToFront,
    Forward,
    Backward,
    ToBack,
}

/// <summary>Z-Reihenfolge: Listenende = vorne.</summary>
public static class ZOrder
{
    public static List<Annotation> Apply(List<Annotation> order, IEnumerable<Annotation> selection, OrderOperation operation)
    {
        var selected = selection.ToHashSet();
        var result = new List<Annotation>(order);
        switch (operation)
        {
            case OrderOperation.ToFront:
                return [.. result.Where(a => !selected.Contains(a)), .. result.Where(selected.Contains)];
            case OrderOperation.ToBack:
                return [.. result.Where(selected.Contains), .. result.Where(a => !selected.Contains(a))];
            case OrderOperation.Forward:
                for (var i = result.Count - 2; i >= 0; i--)
                {
                    if (selected.Contains(result[i]) && !selected.Contains(result[i + 1]))
                    {
                        (result[i], result[i + 1]) = (result[i + 1], result[i]);
                    }
                }

                return result;
            default:
                for (var i = 1; i < result.Count; i++)
                {
                    if (selected.Contains(result[i]) && !selected.Contains(result[i - 1]))
                    {
                        (result[i], result[i - 1]) = (result[i - 1], result[i]);
                    }
                }

                return result;
        }
    }
}
