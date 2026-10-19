using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Eintrag der Vorlagenliste mit den Farben für die kleine Vorschau.</summary>
public sealed class PresetItem
{
    public PresetItem(ToolPreset preset)
    {
        Preset = preset;
        (var fill, var line) = Colors(preset);
        FillBrush = ToBrush(fill);
        LineBrush = ToBrush(line);
        ShowSample = preset.Text is not null;
    }

    public ToolPreset Preset { get; }

    public string Name => Preset.Name;

    /// <summary>Füllfarbe der Vorschau (Textvorlagen: Hintergrund).</summary>
    public Brush FillBrush { get; }

    /// <summary>Linienfarbe der Vorschau (Textvorlagen: Textfarbe).</summary>
    public Brush LineBrush { get; }

    /// <summary>Textvorlagen zeigen in der Vorschau „Abc“.</summary>
    public bool ShowSample { get; }

    private static (ArgbColor Fill, ArgbColor Line) Colors(ToolPreset preset)
    {
        if (preset.Text is { } text)
        {
            return (text.Background, text.Fill);
        }

        if (preset.Marker is { } marker)
        {
            return (marker.Fill, marker.Stroke.Color);
        }

        if (preset.Blur is { } blur)
        {
            return (blur.Mode == BlurMode.Redact ? blur.RedactColor : ArgbColor.FromRgb(0xB0, 0xB0, 0xB0), ArgbColor.FromRgb(0x80, 0x80, 0x80));
        }

        if (preset.Magnifier is { } magnifier)
        {
            return (ArgbColor.Transparent, magnifier.Border.Color);
        }

        var shape = preset.Shape ?? new ShapeStyle();
        return (shape.Fill, shape.Stroke.Color);
    }

    private static SolidColorBrush ToBrush(ArgbColor color)
    {
        var brush = new SolidColorBrush(BrushCache.ToColor(color));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// Vorlagenliste des aktiven Werkzeugs bzw. der (einheitlichen) Auswahl: anwenden, als Vorlage speichern,
/// umbenennen, löschen und Standardvorlagen wiederherstellen.
/// </summary>
public sealed partial class PresetListViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly ToolRegistry _tools;
    private readonly Func<ToolBase?> _activeTool;
    private readonly IDialogService _dialogs;
    private readonly Action _refreshPanels;
    private string? _itemsToolId;
    private IReadOnlyList<PresetItem>? _items;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PresetItem? _selectedItem;

    public PresetListViewModel(EditorContext context, ToolRegistry tools, Func<ToolBase?> activeTool, IDialogService dialogs, Action refreshPanels)
    {
        _context = context;
        _tools = tools;
        _activeTool = activeTool;
        _dialogs = dialogs;
        _refreshPanels = refreshPanels;
        _context.Selection.Changed += OnStateChanged;
        _context.Presets.Changed += OnPresetsChanged;
    }

    /// <summary>Werkzeug, dessen Vorlagen angezeigt werden; <c>null</c>, wenn das Werkzeug keine Vorlagen hat.</summary>
    public string? ToolId => PresetStyles.Resolve(_context.Selection.Items, _activeTool());

    public bool IsApplicable => ToolId is not null;

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => IsApplicable && !HasItems;

    public bool HasDefaults => ToolId is { } id && ToolPresetService.HasDefaults(id);

    public IReadOnlyList<PresetItem> Items
    {
        get
        {
            var id = ToolId;
            if (id is null)
            {
                return [];
            }

            if (_items is null || _itemsToolId != id)
            {
                _items = [.. _context.Presets.GetPresets(id).Select(p => new PresetItem(p))];
                _itemsToolId = id;
            }

            return _items;
        }
    }

    public void Refresh()
    {
        if (_itemsToolId != ToolId)
        {
            _items = null;
            SelectedItem = null;
        }

        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Presets.Changed -= OnPresetsChanged;
    }

    /// <summary>Wendet die Vorlage auf die Auswahl bzw. die Werkzeug-Vorgabe an.</summary>
    [RelayCommand]
    private void Apply(PresetItem? item)
    {
        if (item is null || ToolId is not { } id)
        {
            return;
        }

        PresetStyles.Apply(_context, id, item.Preset);
        SelectedItem = item;
        _refreshPanels();
    }

    /// <summary>Speichert den aktuellen Panel-Stand unter einem Namen (Dialog, Rückfrage beim Überschreiben).</summary>
    [RelayCommand]
    private void SaveAsPreset()
    {
        if (ToolId is not { } id || _tools.Find(id) is not { } tool)
        {
            return;
        }

        if (AskName(id, Strings.Preset_SaveTitle, Strings.Preset_NamePrompt, SelectedItem?.Name ?? string.Empty, currentName: null) is not { } name)
        {
            return;
        }

        _context.Presets.Save(id, PresetStyles.Capture(_context, id, tool.BuiltInStyle, name));
        Refresh();
        SelectedItem = Items.FirstOrDefault(i => PresetNames.AreSame(i.Name, name));
    }

    [RelayCommand(CanExecute = nameof(HasSelectedItem))]
    private void Rename()
    {
        if (ToolId is not { } id || SelectedItem is not { } item)
        {
            return;
        }

        if (AskName(id, Strings.Preset_RenameTitle, Strings.Preset_NamePrompt, item.Name, currentName: item.Name) is not { } name)
        {
            return;
        }

        _context.Presets.Rename(id, item.Name, name);
        Refresh();
        SelectedItem = Items.FirstOrDefault(i => PresetNames.AreSame(i.Name, name));
    }

    [RelayCommand(CanExecute = nameof(HasSelectedItem))]
    private void Delete()
    {
        if (ToolId is not { } id || SelectedItem is not { } item)
        {
            return;
        }

        if (!_dialogs.Confirm(Strings.Preset_DeleteTitle, string.Format(CultureInfo.CurrentUICulture, Strings.Preset_DeleteQuestion, item.Name)))
        {
            return;
        }

        _context.Presets.Delete(id, item.Name);
        SelectedItem = null;
        Refresh();
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        if (ToolId is { } id)
        {
            _context.Presets.RestoreDefaults(id);
            Refresh();
        }
    }

    private bool HasSelectedItem() => SelectedItem is not null;

    /// <summary>
    /// Fragt den Namen ab. Trifft er eine andere Vorlage (ohne Beachtung der Groß-/Kleinschreibung), folgt die
    /// Rückfrage zum Überschreiben; bei „Nein“ erscheint der Dialog erneut. <c>null</c> = abgebrochen.
    /// </summary>
    private string? AskName(string toolId, string title, string prompt, string initial, string? currentName)
    {
        var names = _context.Presets.GetPresets(toolId).Select(p => p.Name).ToList();
        while (true)
        {
            var name = _dialogs.AskPresetName(title, prompt, names, initial);
            if (name is null || !PresetNames.IsValid(name))
            {
                return null;
            }

            name = PresetNames.Clean(name);
            var isSelf = currentName is not null && PresetNames.AreSame(currentName, name);
            if (isSelf || !_context.Presets.Exists(toolId, name))
            {
                return name;
            }

            var existing = _context.Presets.Find(toolId, name)!.Name;
            if (_dialogs.Confirm(Strings.Preset_OverwriteTitle, string.Format(CultureInfo.CurrentUICulture, Strings.Preset_OverwriteQuestion, existing)))
            {
                return name;
            }

            initial = name;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnPresetsChanged(object? sender, EventArgs e)
    {
        _items = null;
        Refresh();
    }
}
