using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Eigenschaften von Freihandstrichen (Glättung, Druck), Radierer, Unschärfe-Bereichen und Lupen. Gemischte Werte = <c>null</c>.
/// Ohne Auswahl bearbeitet das Panel die Vorlage des aktiven Werkzeugs (gilt für neue Objekte im Editorfenster).
/// </summary>
public sealed class ToolOptionsViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;

    public ToolOptionsViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        ModeChoices =
        [
            new Choice<BlurMode>(Strings.Blur_Pixelate, BlurMode.Pixelate),
            new Choice<BlurMode>(Strings.Blur_Gaussian, BlurMode.GaussianBlur),
            new Choice<BlurMode>(Strings.Blur_Redact, BlurMode.Redact),
        ];
        RegionShapeChoices =
        [
            new Choice<RegionShape>(Strings.Region_Rectangle, RegionShape.Rectangle),
            new Choice<RegionShape>(Strings.Region_Ellipse, RegionShape.Ellipse),
        ];
        MagnifierShapeChoices =
        [
            new Choice<MagnifierShape>(Strings.Magnifier_Circle, MagnifierShape.Circle),
            new Choice<MagnifierShape>(Strings.Magnifier_Rectangle, MagnifierShape.Rectangle),
        ];
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
    }

    public IReadOnlyList<Choice<BlurMode>> ModeChoices { get; }

    public IReadOnlyList<Choice<RegionShape>> RegionShapeChoices { get; }

    public IReadOnlyList<Choice<MagnifierShape>> MagnifierShapeChoices { get; }

    // ---- Zustand ----

    private IReadOnlyList<StrokeAnnotation> Strokes => [.. _context.Selection.Items.OfType<StrokeAnnotation>()];

    private IReadOnlyList<BlurAnnotation> Blurs => [.. _context.Selection.Items.OfType<BlurAnnotation>()];

    private IReadOnlyList<MagnifierAnnotation> Magnifiers => [.. _context.Selection.Items.OfType<MagnifierAnnotation>()];

    private bool NoSelection => _context.Selection.IsEmpty;

    private bool StrokeDefaults => NoSelection && _activeTool() is FreehandToolBase;

    private bool BlurDefaults => NoSelection && _activeTool() is BlurTool;

    private bool MagnifierDefaults => NoSelection && _activeTool() is MagnifierTool;

    public bool ShowStroke => Strokes.Count > 0 || StrokeDefaults;

    public bool ShowEraser => NoSelection && _activeTool() is EraserTool;

    public bool ShowBlur => Blurs.Count > 0 || BlurDefaults;

    public bool ShowMagnifier => Magnifiers.Count > 0 || MagnifierDefaults;

    public bool IsApplicable => ShowStroke || ShowEraser || ShowBlur || ShowMagnifier;

    // ---- Freihand ----

    public double? SmoothingPercent
    {
        get => StrokeDefaults ? Math.Round(_context.Freehand.Smoothing * 100) : Read(Strokes, s => Math.Round(s.Smoothing * 100));
        set
        {
            if (value is not { } percent)
            {
                return;
            }

            var smoothing = Math.Clamp(percent, 0, 100) / 100;
            if (StrokeDefaults)
            {
                _context.Freehand = _context.Freehand with { Smoothing = smoothing };
            }
            else
            {
                StyleEditing.Apply(_context, nameof(SmoothingPercent), Strokes, smoothing, s => s.Smoothing, (s, v) => s.Smoothing = v);
            }

            Refresh();
        }
    }

    public bool? UsePressure
    {
        get => StrokeDefaults ? _context.Freehand.UsePressure : Read(Strokes, s => s.UsePressure);
        set
        {
            if (value is not { } use)
            {
                return;
            }

            if (StrokeDefaults)
            {
                _context.Freehand = _context.Freehand with { UsePressure = use };
            }
            else
            {
                StyleEditing.Apply(_context, nameof(UsePressure), Strokes, use, s => s.UsePressure, (s, v) => s.UsePressure = v);
            }

            Refresh();
        }
    }

    /// <summary>Textmarker hinter allen übrigen Objekten zeichnen (gilt für das ganze Dokument und wird als Vorgabe gemerkt).</summary>
    public bool HighlightersBehind
    {
        get => _context.Document.HighlightersBehind;
        set
        {
            if (_context.Document.HighlightersBehind == value)
            {
                return;
            }

            _context.Document.HighlightersBehind = value;
            _context.Settings.Current = _context.Settings.Current with { HighlighterBehind = value };
            _ = _context.Settings.SaveAsync();
            OnPropertyChanged();
        }
    }

    public bool EraserAllObjects
    {
        get => _context.EraserAllObjects;
        set
        {
            _context.EraserAllObjects = value;
            OnPropertyChanged();
        }
    }

    // ---- Unschärfe ----

    public BlurMode? Mode
    {
        get => ReadBlur(b => b.Mode);
        set => WriteBlur(nameof(Mode), value, b => b.Mode, (b, v) => b.Mode = v);
    }

    public double? Strength
    {
        get => ReadBlur(b => b.Strength);
        set => WriteBlur(nameof(Strength), value is { } v ? Math.Clamp(v, BlurAnnotation.MinStrength, BlurAnnotation.MaxStrength) : null, b => b.Strength, (b, v) => b.Strength = v);
    }

    public RegionShape? RegionShapeValue
    {
        get => ReadBlur(b => b.Shape);
        set => WriteBlur(nameof(RegionShapeValue), value, b => b.Shape, (b, v) => b.Shape = v);
    }

    public double? FeatherEdge
    {
        get => ReadBlur(b => b.FeatherEdge);
        set => WriteBlur(nameof(FeatherEdge), value is { } v ? Math.Clamp(v, 0, 100) : null, b => b.FeatherEdge, (b, v) => b.FeatherEdge = v);
    }

    public ArgbColor? RedactColor
    {
        get => ReadBlur(b => b.RedactColor);
        set => WriteBlur(nameof(RedactColor), value, b => b.RedactColor, (b, v) => b.RedactColor = v);
    }

    public bool ShowStrength => Mode != BlurMode.Redact;

    public bool ShowRedactColor => Mode == BlurMode.Redact;

    /// <summary>Hinweis, dass Weichzeichnen rekonstruierbar sein kann.</summary>
    public bool ShowBlurWarning => Mode == BlurMode.GaussianBlur;

    // ---- Lupe ----

    public double? Zoom
    {
        get => ReadMagnifier(m => m.Zoom);
        set => WriteMagnifier(nameof(Zoom), value is { } v ? Math.Clamp(v, MagnifierAnnotation.MinZoom, MagnifierAnnotation.MaxZoom) : null, m => m.Zoom, (m, v) => m.Zoom = v);
    }

    public MagnifierShape? MagnifierShapeValue
    {
        get => ReadMagnifier(m => m.Shape);
        set => WriteMagnifier(nameof(MagnifierShapeValue), value, m => m.Shape, (m, v) => m.Shape = v);
    }

    public ArgbColor? BorderColor
    {
        get => ReadMagnifier(m => m.Border.Color);
        set => WriteMagnifier(nameof(BorderColor), value, m => m.Border.Color, (m, v) => m.Border = m.Border with { Color = v });
    }

    public double? BorderThickness
    {
        get => ReadMagnifier(m => m.Border.Thickness);
        set => WriteMagnifier(nameof(BorderThickness), value is { } v ? Math.Clamp(v, 0, 50) : null, m => m.Border.Thickness, (m, v) => m.Border = m.Border with { Thickness = v });
    }

    public bool? ShowConnector
    {
        get => ReadMagnifier(m => m.ShowConnector);
        set => WriteMagnifier(nameof(ShowConnector), value, m => m.ShowConnector, (m, v) => m.ShowConnector = v);
    }

    public bool? ShowSourceFrame
    {
        get => ReadMagnifier(m => m.ShowSourceFrame);
        set => WriteMagnifier(nameof(ShowSourceFrame), value, m => m.ShowSourceFrame, (m, v) => m.ShowSourceFrame = v);
    }

    public bool? BaseImageOnly
    {
        get => ReadMagnifier(m => m.BaseImageOnly);
        set => WriteMagnifier(nameof(BaseImageOnly), value, m => m.BaseImageOnly, (m, v) => m.BaseImageOnly = v);
    }

    public bool? SmoothScaling
    {
        get => ReadMagnifier(m => m.SmoothScaling);
        set => WriteMagnifier(nameof(SmoothScaling), value, m => m.SmoothScaling, (m, v) => m.SmoothScaling = v);
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private IEnumerable<BlurAnnotation> BlurTargets() => BlurDefaults ? [_context.BlurTemplate] : Blurs;

    private IEnumerable<MagnifierAnnotation> MagnifierTargets() => MagnifierDefaults ? [_context.MagnifierTemplate] : Magnifiers;

    private static TV? Read<TA, TV>(IEnumerable<TA> items, Func<TA, TV> get)
        where TV : struct => StyleEditing.TryCommon(items, get, out var value) ? value : null;

    private TV? ReadBlur<TV>(Func<BlurAnnotation, TV> get)
        where TV : struct => Read(BlurTargets(), get);

    private TV? ReadMagnifier<TV>(Func<MagnifierAnnotation, TV> get)
        where TV : struct => Read(MagnifierTargets(), get);

    private void WriteBlur<TV>(string key, TV? value, Func<BlurAnnotation, TV> get, Action<BlurAnnotation, TV> set)
        where TV : struct => Write(key, value, BlurDefaults, _context.BlurTemplate, Blurs, get, set);

    private void WriteMagnifier<TV>(string key, TV? value, Func<MagnifierAnnotation, TV> get, Action<MagnifierAnnotation, TV> set)
        where TV : struct => Write(key, value, MagnifierDefaults, _context.MagnifierTemplate, Magnifiers, get, set);

    private void Write<TA, TV>(string key, TV? value, bool defaults, TA template, IReadOnlyList<TA> selected, Func<TA, TV> get, Action<TA, TV> set)
        where TA : Annotation
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (defaults)
        {
            set(template, v);
        }
        else
        {
            StyleEditing.Apply(_context, key, selected, v, get, set);
        }

        Refresh();
    }
}
