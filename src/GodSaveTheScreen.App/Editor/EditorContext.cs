using System.Collections.Specialized;
using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Pointer-Ereignis in Bildpixeln für die Werkzeuge.</summary>
public sealed class EditorPointerArgs
{
    public EditorPointerArgs(PointD position, ModifierKeys modifiers, double pressure = 1, int clickCount = 1, MouseButton button = MouseButton.Left)
    {
        Position = position;
        Modifiers = modifiers;
        Pressure = pressure;
        ClickCount = clickCount;
        Button = button;
    }

    /// <summary>Position in Bildpixeln des Basisbilds.</summary>
    public PointD Position { get; }

    public ModifierKeys Modifiers { get; }

    /// <summary>Stiftdruck 0–1 (Maus: 1).</summary>
    public double Pressure { get; }

    public int ClickCount { get; }

    public MouseButton Button { get; }

    public bool Shift => (Modifiers & ModifierKeys.Shift) != 0;

    public bool Ctrl => (Modifiers & ModifierKeys.Control) != 0;

    public bool Alt => (Modifiers & ModifierKeys.Alt) != 0;
}

/// <summary>Glättung und Druckempfindlichkeit neuer Freihandstriche.</summary>
public sealed record FreehandOptions(double Smoothing = 0.5, bool UsePressure = true);

/// <summary>Auswahl von Objekten des Dokuments.</summary>
public sealed class SelectionModel
{
    private readonly List<Annotation> _items = [];

    public event EventHandler? Changed;

    public IReadOnlyList<Annotation> Items => _items;

    public bool IsEmpty => _items.Count == 0;

    public bool Contains(Annotation annotation) => _items.Contains(annotation);

    public void Set(IEnumerable<Annotation> items)
    {
        var next = items.Distinct().ToList();
        if (next.SequenceEqual(_items))
        {
            return;
        }

        _items.Clear();
        _items.AddRange(next);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Set(Annotation item) => Set([item]);

    public void Clear() => Set([]);

    public void Toggle(Annotation item)
    {
        if (!_items.Remove(item))
        {
            _items.Add(item);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gemeinsame geometrische Ausdehnung der Auswahl.</summary>
    public RectD? Bounds => _items.Count == 0 ? null : _items.Select(a => a.Bounds).Aggregate((x, y) => x.Union(y));
}

/// <summary>Flüchtiger Zustand der Interaktionsebene (nicht Teil des Dokuments, nicht exportiert).</summary>
public sealed class InteractionState
{
    /// <summary>Vorschau des gerade gezeichneten Objekts.</summary>
    public Annotation? Preview { get; set; }

    /// <summary>Aufgezogener Auswahlrahmen.</summary>
    public RectD? RubberBand { get; set; }

    /// <summary>Während der Textbearbeitung ausgeblendetes Objekt.</summary>
    public Annotation? Hidden { get; set; }

    /// <summary>Vom Radierer markierte Objekte (werden beim Loslassen entfernt).</summary>
    public IReadOnlyList<Annotation> Marked { get; set; } = [];

    /// <summary>Größenvorschau des Stifts bzw. Radierers: Mittelpunkt und Durchmesser in Bildpixeln.</summary>
    public (PointD Center, double Diameter)? BrushPreview { get; set; }

    /// <summary>Gerade aufgezogener Bereich der Bereichsauswahl.</summary>
    public AreaPreview? AreaPreview { get; set; }
}

/// <summary>Vorschau der Bereichsauswahl: Rechteck/Ellipse über <see cref="Rect"/>, Lasso über <see cref="Points"/>.</summary>
public sealed record AreaPreview(AreaSelectShape Shape, RectD Rect, IReadOnlyList<PointD> Points);

/// <summary>Laufende Textbearbeitung eines Text-, Stempel- oder Sprechblasenobjekts (bei <see cref="IsNew"/> noch nicht im Dokument).</summary>
public sealed record TextEditSession(Annotation Annotation, bool IsNew)
{
    public IEditableTextAnnotation Editable => (IEditableTextAnnotation)Annotation;
}

/// <summary>Gemeinsamer Zustand eines Editorfensters: Dokument, Auswahl, Interaktion, Standardwerte.</summary>
public sealed class EditorContext : IDisposable
{
    private const double HitPixels = 5;

    public EditorContext(ImageDocument document, IToolDefaultsService defaults, ISettingsService settings, IDocumentExporter exporter)
    {
        Document = document;
        Defaults = defaults;
        Settings = settings;
        Exporter = exporter;
        Presets = new ToolPresetService(settings);
        Emoji = new EmojiToolState(settings);
        Stamp = new StampToolState(settings);
        Callout = new CalloutToolState(settings);
        Image = new ImageToolState(settings);
        Document.HighlightersBehind = settings.Current.HighlighterBehind;
        Document.Annotations.CollectionChanged += OnAnnotationsChanged;
        Crop = new CropState(document);
        AreaSelection = new AreaSelection(document.BaseImage.Width, document.BaseImage.Height);
        BlurTemplate.Apply(defaults.GetBlur());
        MagnifierTemplate.Apply(defaults.GetMagnifier());
    }

    /// <summary>Benutzer-Vorlagen der Werkzeuge.</summary>
    public ToolPresetService Presets { get; }

    /// <summary>Gewähltes Emoji, Hautton, Zuletzt verwendet und Favoriten des Emoji-Werkzeugs.</summary>
    public EmojiToolState Emoji { get; }

    /// <summary>Standardwerte des Stempelwerkzeugs (letzte Konfiguration, in den Einstellungen gespeichert).</summary>
    public StampToolState Stamp { get; }

    /// <summary>Standardwerte des Sprechblasen-Werkzeugs (letzte Konfiguration, in den Einstellungen gespeichert).</summary>
    public CalloutToolState Callout { get; }

    /// <summary>Vorgaben neuer Bildobjekte (letzte Konfiguration, in den Einstellungen gespeichert).</summary>
    public ImageToolState Image { get; }

    /// <summary>Wird vom Editor gesetzt: Bilddatei wählen und um den Punkt bzw. in den Bereich einfügen.</summary>
    public Action<PointD, RectD?>? ImageInsertHandler { get; set; }

    /// <summary>Bild einfügen (Dateiauswahl folgt asynchron über <see cref="ImageInsertHandler"/>).</summary>
    public void RequestImageInsert(PointD center, RectD? area = null) => ImageInsertHandler?.Invoke(center, area);

    /// <summary>Zustand des Zuschnittmodus.</summary>
    public CropState Crop { get; }

    /// <summary>Pixelauswahl der Bereichsauswahl (zum Entfernen von Bildbereichen; nicht im Undo-Verlauf).</summary>
    public AreaSelection AreaSelection { get; }

    /// <summary>Einstellungen von Bereichsauswahl und Hintergrundradierer (in den Einstellungen gespeichert).</summary>
    public MaskToolStyle MaskTool
    {
        get => Settings.Current.MaskToolDefaults;
        set
        {
            if (value == Settings.Current.MaskToolDefaults)
            {
                return;
            }

            Settings.Current = Settings.Current with { MaskToolDefaults = value };
            _ = Settings.SaveAsync();
        }
    }

    public ImageDocument Document { get; }

    public UndoManager Undo => Document.Undo;

    public SelectionModel Selection { get; } = new();

    public InteractionState Interaction { get; } = new();

    /// <summary>Virtuelles Lineal (UI-Zustand, nicht Teil des Dokuments).</summary>
    public RulerViewModel Ruler { get; } = new();

    /// <summary>Glättung und Druck neuer Freihandstriche (in den Einstellungen gespeichert).</summary>
    public FreehandOptions Freehand
    {
        get => Defaults.GetFreehand();
        set => Defaults.SetFreehand(value);
    }

    /// <summary>Vorlage für neue Unschärfe-Bereiche (Eigenschaftenpanel ohne Auswahl); Änderungen mit <see cref="SaveBlurTemplate"/> speichern.</summary>
    public BlurAnnotation BlurTemplate { get; } = new();

    /// <summary>Vorlage für neue Lupen; Änderungen mit <see cref="SaveMagnifierTemplate"/> speichern.</summary>
    public MagnifierAnnotation MagnifierTemplate { get; } = new();

    /// <summary>Speichert die Unschärfe-Vorlage als letzte Konfiguration.</summary>
    public void SaveBlurTemplate() => Defaults.SetBlur(BlurTemplate.ToStyle());

    /// <summary>Speichert die Lupen-Vorlage als letzte Konfiguration.</summary>
    public void SaveMagnifierTemplate() => Defaults.SetMagnifier(MagnifierTemplate.ToStyle());

    /// <summary>Radierer entfernt alle Objekte, nicht nur Freihandstriche.</summary>
    public bool EraserAllObjects { get; set; }

    /// <summary>Liefert die Mitte des sichtbaren Bildausschnitts in Bildpixeln (von der Zeichenfläche gesetzt).</summary>
    public Func<PointD>? VisibleCenterProvider { get; set; }

    public IToolDefaultsService Defaults { get; }

    public ISettingsService Settings { get; }

    public IDocumentExporter Exporter { get; }

    /// <summary>Bildschirm-DIPs je Bildpixel (Zoom / DPI-Skalierung); vom Zeichenfläche-Steuerelement gesetzt.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>Treffertoleranz in Bildpixeln (konstant etwa 5 Bildschirmpixel).</summary>
    public double HitTolerance => HitPixels / Math.Max(Scale, 0.01);

    /// <summary>Größe in Bildpixeln, die auf dem Bildschirm <paramref name="screenUnits"/> DIPs entspricht.</summary>
    public double ToImageUnits(double screenUnits) => screenUnits / Math.Max(Scale, 0.01);

    public event EventHandler? InteractionInvalidated;

    /// <summary>Die Oberfläche soll ein Eingabefeld über dem Text öffnen.</summary>
    public event EventHandler<TextEditSession>? TextEditStarted;

    /// <summary>Die Textbearbeitung ist beendet (übernommen oder abgebrochen); die Oberfläche schließt das Eingabefeld.</summary>
    public event EventHandler? TextEditEnded;

    public TextEditSession? ActiveTextEdit { get; private set; }

    /// <summary>
    /// Startet die Bearbeitung direkt auf der Zeichenfläche. Das Objekt wird währenddessen ausgeblendet;
    /// die gesamte Sitzung wird zu genau einer Undo-Aktion (<see cref="CommitTextEdit"/>).
    /// </summary>
    public void BeginTextEdit(Annotation annotation, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation is not IEditableTextAnnotation)
        {
            throw new ArgumentException("Objekt hat keinen bearbeitbaren Text.", nameof(annotation));
        }

        if (ActiveTextEdit is not null)
        {
            CancelTextEdit();
        }

        ActiveTextEdit = new TextEditSession(annotation, isNew);
        Interaction.Hidden = annotation;
        if (!isNew)
        {
            Selection.Set(annotation);
        }

        InvalidateInteraction();
        TextEditStarted?.Invoke(this, ActiveTextEdit);
    }

    /// <summary>
    /// Übernimmt den Text. Neues Objekt mit leerem Text wird verworfen (kein Undo-Eintrag);
    /// bestehendes Objekt mit leerem Text wird gelöscht (ein Undo-Schritt); unveränderter Text bewirkt nichts.
    /// </summary>
    public void CommitTextEdit(string text)
    {
        var session = ActiveTextEdit;
        if (session is null)
        {
            return;
        }

        ActiveTextEdit = null;
        Interaction.Hidden = null;
        var annotation = session.Annotation;
        var editable = session.Editable;
        var isStamp = annotation is StampAnnotation;
        var callout = annotation as CalloutAnnotation;
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var isEmpty = string.IsNullOrWhiteSpace(normalized);
        var (addDescription, removeDescription) = annotation switch
        {
            StampAnnotation => ("Stempel setzen", "Stempel löschen"),
            CalloutAnnotation => ("Sprechblase einfügen", "Sprechblase löschen"),
            _ => ("Text einfügen", "Text löschen"),
        };

        if (session.IsNew)
        {
            if (!isEmpty)
            {
                editable.Text = normalized;

                // Die Sprechblase wächst nach unten, bis der Text hineinpasst.
                callout?.FitHeightToText();
                annotation.Name = AnnotationNames.NextName(Document.Annotations, annotation);
                Undo.Execute(new AddAnnotationAction(Document.Annotations, annotation, description: addDescription));
                Selection.Set(annotation);
                if (isStamp)
                {
                    // Der zuletzt gestempelte Text wird Vorgabe des nächsten Stempels.
                    Stamp.Text = normalized;
                }
            }
        }
        else if (isEmpty)
        {
            Undo.Execute(new RemoveAnnotationsAction(Document.Annotations, [annotation], removeDescription));
        }
        else if (normalized != editable.Text)
        {
            var before = annotation.Clone();

            // Gedrehter Text wächst entlang der eigenen Achsen; seine linke obere Ecke bleibt im Bild stehen.
            RotationMath.PreserveFrame(annotation, () => editable.Text = normalized);
            callout?.FitHeightToText();
            Undo.Execute(new ReplaceGeometryAction([(annotation, before, annotation.Clone())], "Text bearbeiten"));
        }

        InvalidateInteraction();
        TextEditEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Verwirft die Bearbeitung ohne Änderung am Dokument.</summary>
    public void CancelTextEdit()
    {
        if (ActiveTextEdit is null)
        {
            return;
        }

        ActiveTextEdit = null;
        Interaction.Hidden = null;
        InvalidateInteraction();
        TextEditEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Wird vom Fenster gesetzt: Werkzeug per ID aktivieren.</summary>
    public Action<string>? ActivateToolHandler { get; set; }

    public void ActivateTool(string id) => ActivateToolHandler?.Invoke(id);

    /// <summary>Eigenschaften der ausgewählten Objekte wurden im Panel geändert (Werte werden Standard des Werkzeugs).</summary>
    public event EventHandler? SelectionStyleEdited;

    public void RaiseSelectionStyleEdited() => SelectionStyleEdited?.Invoke(this, EventArgs.Empty);

    public void InvalidateInteraction() => InteractionInvalidated?.Invoke(this, EventArgs.Empty);

    /// <summary>Oberstes Objekt unter dem Punkt (sichtbar).</summary>
    public Annotation? HitTest(PointD point)
    {
        for (var i = Document.Annotations.Count - 1; i >= 0; i--)
        {
            var annotation = Document.Annotations[i];
            if (annotation.IsVisible && annotation.HitTest(point, HitTolerance))
            {
                return annotation;
            }
        }

        return null;
    }

    public void Dispose() => Document.Annotations.CollectionChanged -= OnAnnotationsChanged;

    private void OnAnnotationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Entfernte Objekte verlassen die Auswahl.
        if (!Selection.IsEmpty)
        {
            Selection.Set(Selection.Items.Where(Document.Annotations.Contains));
        }
    }
}
