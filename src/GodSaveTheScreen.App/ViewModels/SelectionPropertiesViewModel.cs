using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Eigenschaften der Auswahl (gemischte Werte = <c>null</c>). Ohne Auswahl bearbeitet es die Standardwerte
/// des aktiven Formwerkzeugs. Jede Änderung läuft über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class SelectionPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly Action<Action<ArgbColor>> _startPipette;
    private readonly EffectSwitch<double> _outlineSwitch = new(t => t > 0, 0, 2);

    public SelectionPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool, Action<Action<ArgbColor>> startPipette)
    {
        _context = context;
        _activeTool = activeTool;
        _startPipette = startPipette;

        DashChoices =
        [
            new Choice<DashKind>(Strings.Dash_Solid, DashKind.Solid),
            new Choice<DashKind>(Strings.Dash_Dash, DashKind.Dash),
            new Choice<DashKind>(Strings.Dash_Dot, DashKind.Dot),
            new Choice<DashKind>(Strings.Dash_DashDot, DashKind.DashDot),
        ];
        CapChoices =
        [
            new Choice<LineEndStyle>(Strings.Cap_None, LineEndStyle.None),
            new Choice<LineEndStyle>(Strings.Cap_Arrow, LineEndStyle.Arrow),
            new Choice<LineEndStyle>(Strings.Cap_OpenArrow, LineEndStyle.OpenArrow),
            new Choice<LineEndStyle>(Strings.Cap_Circle, LineEndStyle.Circle),
            new Choice<LineEndStyle>(Strings.Cap_Square, LineEndStyle.Square),
        ];

        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Defaults.RecentColorsChanged += OnRecentColorsChanged;
    }

    public IReadOnlyList<Choice<DashKind>> DashChoices { get; }

    public IReadOnlyList<Choice<LineEndStyle>> CapChoices { get; }

    public IReadOnlyList<ArgbColor> RecentColors => _context.Defaults.RecentColors;

    private IReadOnlyList<ShapeAnnotation> Shapes => [.. _context.Selection.Items.OfType<ShapeAnnotation>()];

    private bool HasSelection => !_context.Selection.IsEmpty;

    private ToolBase? DefaultsTool => !HasSelection && _activeTool() is { UsesShapeStyle: true } tool ? tool : null;

    /// <summary>Panel zeigt etwas an (Auswahl mit Formen oder Standardwerte eines Formwerkzeugs).</summary>
    public bool IsApplicable => Shapes.Count > 0 || DefaultsTool is not null;

    public bool IsDefaultsMode => DefaultsTool is not null;

    /// <summary>Deckkraft gilt für jedes Objekt der Auswahl (auch Text und Marker) sowie für die Standardwerte der Formwerkzeuge.</summary>
    public bool ShowOpacity => HasSelection || DefaultsTool is not null;

    public bool ShowFill => DefaultsTool is { } tool
        ? tool is not (LineTool or FreehandToolBase)
        : Shapes.Any(s => s is not (LineAnnotation or StrokeAnnotation));

    public bool ShowDash => DefaultsTool is { } tool ? tool is not FreehandToolBase : Shapes.Any(s => s is not StrokeAnnotation);

    public bool ShowCornerRadius => DefaultsTool is { } tool ? tool is RectangleTool : Shapes.Any(s => s is RectangleAnnotation);

    public bool ShowCaps => DefaultsTool is { } tool ? tool is LineTool : Shapes.Any(s => s is LineAnnotation);

    public ArgbColor? StrokeColor
    {
        get => Get(s => s.Stroke.Color, st => st.Stroke.Color);
        set => Set(nameof(StrokeColor), value, s => s.Stroke.Color, (s, v) => s.Stroke = s.Stroke with { Color = v }, (st, v) => st with { Stroke = st.Stroke with { Color = v } });
    }

    public ArgbColor? FillColor
    {
        get => Get(s => s.Fill, st => st.Fill);
        set => Set(nameof(FillColor), value, s => s.Fill, (s, v) => s.Fill = v, (st, v) => st with { Fill = v });
    }

    public double? Thickness
    {
        get => Get(s => s.Stroke.Thickness, st => st.Stroke.Thickness);
        set => Set(nameof(Thickness), value is { } v ? Math.Clamp(v, 0, 100) : null, s => s.Stroke.Thickness, (s, v) => s.Stroke = s.Stroke with { Thickness = v }, (st, v) => st with { Stroke = st.Stroke with { Thickness = v } });
    }

    /// <summary>Für den Schieberegler (gemischt = 0).</summary>
    public double ThicknessValue
    {
        get => Thickness ?? 0;
        set => Thickness = Math.Round(value * 2) / 2;
    }

    public DashKind? Dash
    {
        get => Get(s => s.Stroke.Dash, st => st.Stroke.Dash);
        set => Set(nameof(Dash), value, s => s.Stroke.Dash, (s, v) => s.Stroke = s.Stroke with { Dash = v }, (st, v) => st with { Stroke = st.Stroke with { Dash = v } });
    }

    public double? CornerRadius
    {
        get => GetFrom<RectangleAnnotation, double>(r => r.CornerRadius, st => st.CornerRadius);
        set => SetOn<RectangleAnnotation, double>(nameof(CornerRadius), value is { } v ? Math.Max(0, v) : null, r => r.CornerRadius, (r, v) => r.CornerRadius = v, (st, v) => st with { CornerRadius = v });
    }

    public LineEndStyle? StartCap
    {
        get => GetFrom<LineAnnotation, LineEndStyle>(l => l.StartCap, st => st.StartCap);
        set => SetOn<LineAnnotation, LineEndStyle>(nameof(StartCap), value, l => l.StartCap, (l, v) => l.StartCap = v, (st, v) => st with { StartCap = v });
    }

    public LineEndStyle? EndCap
    {
        get => GetFrom<LineAnnotation, LineEndStyle>(l => l.EndCap, st => st.EndCap);
        set => SetOn<LineAnnotation, LineEndStyle>(nameof(EndCap), value, l => l.EndCap, (l, v) => l.EndCap = v, (st, v) => st with { EndCap = v });
    }

    /// <summary>Kontur von Linie/Pfeil (wie beim Text: Farbe, Stärke, Unschärfe).</summary>
    public ArgbColor? OutlineColor
    {
        get => GetFrom<LineAnnotation, ArgbColor>(l => l.Outline.Color, st => (st.Outline ?? LineAnnotation.NoOutline).Color);
        set => SetOn<LineAnnotation, ArgbColor>(nameof(OutlineColor), value, l => l.Outline.Color, (l, v) => l.Outline = l.Outline with { Color = v }, (st, v) => st with { Outline = (st.Outline ?? LineAnnotation.NoOutline) with { Color = v } });
    }

    public double? OutlineThickness
    {
        get => GetFrom<LineAnnotation, double>(l => l.Outline.Thickness, st => (st.Outline ?? LineAnnotation.NoOutline).Thickness);
        set => SetOn<LineAnnotation, double>(nameof(OutlineThickness), value is { } v ? Math.Clamp(v, 0, 50) : null, l => l.Outline.Thickness, (l, v) => l.Outline = l.Outline with { Thickness = v }, (st, v) => st with { Outline = (st.Outline ?? LineAnnotation.NoOutline) with { Thickness = v } });
    }

    /// <summary>Ein/Aus-Schalter der Kontur: aus = Stärke 0, ein = zuletzt verwendete Stärke (sonst 2).</summary>
    public bool? IsOutlineOn
    {
        get => _outlineSwitch.IsOn(OutlineThickness);
        set
        {
            if (value is { } on)
            {
                OutlineThickness = _outlineSwitch.Switch(OutlineThickness, on);
            }
        }
    }

    public double? OutlineBlur
    {
        get => GetFrom<LineAnnotation, double>(l => l.OutlineBlur, st => st.OutlineBlur);
        set => SetOn<LineAnnotation, double>(nameof(OutlineBlur), value is { } v ? Math.Clamp(v, 0, TextAnnotation.MaxOutlineBlur) : null, l => l.OutlineBlur, (l, v) => l.OutlineBlur = v, (st, v) => st with { OutlineBlur = v });
    }

    /// <summary>Deckkraft in Prozent (0–100).</summary>
    public double? OpacityPercent
    {
        get
        {
            if (DefaultsTool is { } tool)
            {
                return Math.Round(Style(tool).Opacity * 100);
            }

            var values = _context.Selection.Items.Select(a => Math.Round(a.Opacity * 100)).Distinct().ToList();
            return values.Count == 1 ? values[0] : null;
        }

        set
        {
            if (value is not { } percent)
            {
                return;
            }

            var opacity = Math.Clamp(percent, 0, 100) / 100;
            if (DefaultsTool is { } tool)
            {
                SaveStyle(tool, Style(tool) with { Opacity = opacity });
            }
            else
            {
                Apply(nameof(OpacityPercent), _context.Selection.Items, opacity, a => a.Opacity, (a, v) => a.Opacity = v);
            }

            Refresh();
        }
    }

    public double OpacityValue
    {
        get => OpacityPercent ?? 100;
        set => OpacityPercent = Math.Round(value);
    }

    [RelayCommand]
    private void Pipette(Action<ArgbColor>? callback)
    {
        if (callback is not null)
        {
            _startPipette(callback);
        }
    }

    [RelayCommand]
    private void RememberColor(ArgbColor color) => _context.Defaults.AddRecentColor(color);

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Defaults.RecentColorsChanged -= OnRecentColorsChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void OnRecentColorsChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(RecentColors));

    private ShapeStyle Style(ToolBase tool) => _context.Defaults.GetShape(tool.Id, tool.BuiltInStyle);

    private void SaveStyle(ToolBase tool, ShapeStyle style) => _context.Defaults.SetShape(tool.Id, style);

    private TV? Get<TV>(Func<ShapeAnnotation, TV> fromShape, Func<ShapeStyle, TV> fromStyle)
        where TV : struct
    {
        if (DefaultsTool is { } tool)
        {
            return fromStyle(Style(tool));
        }

        var values = Shapes.Select(fromShape).Distinct().ToList();
        return values.Count == 1 ? values[0] : null;
    }

    private TV? GetFrom<TA, TV>(Func<TA, TV> fromShape, Func<ShapeStyle, TV> fromStyle)
        where TA : ShapeAnnotation
        where TV : struct
    {
        if (DefaultsTool is { } tool)
        {
            return fromStyle(Style(tool));
        }

        var values = _context.Selection.Items.OfType<TA>().Select(fromShape).Distinct().ToList();
        return values.Count == 1 ? values[0] : null;
    }

    private void Set<TV>(string key, TV? value, Func<ShapeAnnotation, TV> get, Action<ShapeAnnotation, TV> set, Func<ShapeStyle, TV, ShapeStyle> updateStyle)
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (DefaultsTool is { } tool)
        {
            SaveStyle(tool, updateStyle(Style(tool), v));
        }
        else
        {
            Apply(key, Shapes, v, get, set);
        }

        Refresh();
    }

    private void SetOn<TA, TV>(string key, TV? value, Func<TA, TV> get, Action<TA, TV> set, Func<ShapeStyle, TV, ShapeStyle> updateStyle)
        where TA : ShapeAnnotation
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (DefaultsTool is { } tool)
        {
            SaveStyle(tool, updateStyle(Style(tool), v));
        }
        else
        {
            Apply(key, _context.Selection.Items.OfType<TA>(), v, get, set);
        }

        Refresh();
    }

    /// <summary>Setzt den Wert an allen Zielen als ein Undo-Schritt.</summary>
    private void Apply<TA, TV>(string key, IEnumerable<TA> targets, TV value, Func<TA, TV> get, Action<TA, TV> set)
        where TA : Annotation
        where TV : struct
    {
        var changed = targets.Where(t => !EqualityComparer<TV>.Default.Equals(get(t), value) && !t.IsLocked).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction("Eigenschaft ändern"))
        {
            foreach (var target in changed)
            {
                var old = get(target);
                _context.Undo.Execute(new PropertyChangeAction<TV>(target, key, old, value, v => RotationMath.PreserveFrame(target, () => set(target, v))));
            }
        }

        _context.RaiseSelectionStyleEdited();
    }
}
