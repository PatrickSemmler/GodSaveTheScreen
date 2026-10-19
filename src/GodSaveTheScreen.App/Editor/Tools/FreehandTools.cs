using System.Collections.Immutable;
using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Gemeinsame Basis von Stift und Textmarker: sammelt Punkte (mit Stiftdruck), rastet am Lineal ein,
/// <c>Umschalt</c> zeichnet eine gerade Linie vom Startpunkt (mit <c>Strg</c> im 15°-Raster).
/// </summary>
public abstract class FreehandToolBase : ToolBase
{
    /// <summary>Mindestabstand zweier aufgenommener Punkte in Bildpixeln.</summary>
    private const double MinimumPointDistance = 0.5;

    private readonly List<StrokePoint> _raw = [];
    private StrokeAnnotation? _preview;
    private RulerSide? _snapSide;
    private RulerPlacement _ruler;

    protected FreehandToolBase(EditorContext context)
        : base(context)
    {
    }

    protected abstract bool IsHighlighter { get; }

    public override bool HasStyleDefaults => true;

    public override bool UsesShapeStyle => true;

    public override void Deactivate()
    {
        Cancel();
        Context.Interaction.BrushPreview = null;
        Context.InvalidateInteraction();
    }

    public override void Cancel()
    {
        if (_preview is null)
        {
            return;
        }

        _preview = null;
        _raw.Clear();
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var style = Context.Defaults.GetShape(Id, BuiltInStyle);
        _preview = new StrokeAnnotation
        {
            Stroke = style.Stroke,
            Opacity = style.Opacity,
            IsHighlighter = IsHighlighter,
            Smoothing = Context.Freehand.Smoothing,
            UsePressure = Context.Freehand.UsePressure,
        };

        _snapSide = null;
        if (Context.Ruler.IsVisible)
        {
            _ruler = Context.Ruler.Placement(Context.Scale);
            _snapSide = RulerGeometry.NearestEdge(args.Position, _ruler, Context.ToImageUnits(RulerViewModel.SnapDistanceDip));
        }

        _raw.Clear();
        AddPoint(args.Position, args.Pressure, force: true);
        Context.Selection.Clear();
        Context.Interaction.Preview = _preview;
        UpdatePreview(args);
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        Context.Interaction.BrushPreview = (args.Position, StrokeDiameter());
        if (_preview is null)
        {
            Context.InvalidateInteraction();
            return;
        }

        AddPoint(args.Position, args.Pressure, force: false);
        UpdatePreview(args);
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        AddPoint(args.Position, args.Pressure, force: false);
        var stroke = _preview;
        var points = Build(args);
        _preview = null;
        _raw.Clear();
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
        if (points.IsDefaultOrEmpty)
        {
            return;
        }

        stroke.Points = points;
        stroke.Name = AnnotationNames.NextName(Context.Document.Annotations, stroke);
        Context.Undo.Execute(new AddAnnotationAction(Context.Document.Annotations, stroke));
        if (Context.Settings.Current.SwitchToSelectAfterDraw)
        {
            Context.Selection.Set(stroke);
            Context.ActivateTool(SelectTool.ToolId);
        }
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _preview is not null)
        {
            Cancel();
            return true;
        }

        return false;
    }

    private double StrokeDiameter()
    {
        var style = Context.Defaults.GetShape(Id, BuiltInStyle);
        return Math.Max(1, style.Stroke.Thickness);
    }

    private PointD Project(PointD point)
    {
        if (_snapSide is not { } side || _preview is null)
        {
            return point;
        }

        return RulerGeometry.Snap(point, _ruler, side, _preview.Stroke.Thickness);
    }

    private void AddPoint(PointD position, double pressure, bool force)
    {
        var p = Project(position);
        if (!force && _raw.Count > 0 && _raw[^1].Point.DistanceTo(p) < MinimumPointDistance)
        {
            return;
        }

        _raw.Add(new StrokePoint(p.X, p.Y, Math.Clamp(pressure, 0, 1)));
    }

    /// <summary>Punkte des Strichs: bei <c>Umschalt</c> Gerade vom Startpunkt, sonst die aufgenommenen Punkte.</summary>
    private ImmutableArray<StrokePoint> Build(EditorPointerArgs args)
    {
        if (_raw.Count == 0)
        {
            return [];
        }

        if (args.Shift && _raw.Count > 1)
        {
            var first = _raw[0];
            var end = Project(args.Ctrl ? ShapeGeometry.SnapAngle(first.Point, args.Position) : args.Position);
            return [first, new StrokePoint(end.X, end.Y, _raw[^1].Pressure)];
        }

        return StrokeSimplifier.Reduce(_raw);
    }

    private void UpdatePreview(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        Context.Interaction.BrushPreview = (args.Position, StrokeDiameter());
        _preview.Points = args.Shift && _raw.Count > 1 ? Build(args) : [.. _raw];
        Context.InvalidateInteraction();
    }
}

public sealed class PenTool : FreehandToolBase
{
    public const string ToolId = "pen";

    public PenTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Pen";

    public override SymbolRegular Symbol => SymbolRegular.Pen24;

    public override Key ShortcutKey => Key.B;

    protected override bool IsHighlighter => false;

    public override ShapeStyle BuiltInStyle => new() { Stroke = new StrokeStyle(ArgbColor.Red, 4) };
}

public sealed class HighlighterTool : FreehandToolBase
{
    public const string ToolId = "highlighter";

    public HighlighterTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Highlighter";

    public override SymbolRegular Symbol => SymbolRegular.Highlight24;

    public override Key ShortcutKey => Key.H;

    protected override bool IsHighlighter => true;

    public override ShapeStyle BuiltInStyle => new()
    {
        Stroke = new StrokeStyle(ArgbColor.FromRgb(0xFF, 0xEB, 0x3B), 16, Cap: StrokeCapKind.Square),
        Opacity = 0.4,
    };
}

/// <summary>Objektbasierter Radierer: überstrichene Striche werden markiert und beim Loslassen in einer Undo-Aktion entfernt.</summary>
public sealed class EraserTool : ToolBase
{
    public const string ToolId = "eraser";

    private const double RadiusDip = 8;

    private readonly List<Annotation> _marked = [];
    private PointD? _last;
    private bool _active;

    public EraserTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Eraser";

    public override SymbolRegular Symbol => SymbolRegular.Eraser24;

    public override Key ShortcutKey => Key.X;

    public override bool HasStyleDefaults => false;

    public override void Deactivate()
    {
        Cancel();
        Context.Interaction.BrushPreview = null;
        Context.InvalidateInteraction();
    }

    public override void Cancel()
    {
        if (!_active && _marked.Count == 0)
        {
            return;
        }

        _active = false;
        _last = null;
        _marked.Clear();
        Context.Interaction.Marked = [];
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        _active = true;
        _marked.Clear();
        _last = null;
        Context.Selection.Clear();
        MarkAlong(args.Position);
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        Context.Interaction.BrushPreview = (args.Position, Context.ToImageUnits(RadiusDip * 2));
        if (_active)
        {
            MarkAlong(args.Position);
        }
        else
        {
            Context.InvalidateInteraction();
        }
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (!_active)
        {
            return;
        }

        MarkAlong(args.Position);
        var doomed = _marked.Where(a => !a.IsLocked).ToList();
        _active = false;
        _last = null;
        _marked.Clear();
        Context.Interaction.Marked = [];
        Context.InvalidateInteraction();
        if (doomed.Count > 0)
        {
            Context.Undo.Execute(new RemoveAnnotationsAction(Context.Document.Annotations, doomed, "Radieren"));
        }
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _active)
        {
            Cancel();
            return true;
        }

        return false;
    }

    private void MarkAlong(PointD position)
    {
        var from = _last ?? position;
        var distance = from.DistanceTo(position);
        var step = Math.Max(Context.ToImageUnits(RadiusDip) / 2, 0.5);
        var samples = Math.Max(1, (int)Math.Ceiling(distance / step));
        var radius = Context.ToImageUnits(RadiusDip);
        for (var i = 1; i <= samples; i++)
        {
            var t = (double)i / samples;
            var p = new PointD(from.X + ((position.X - from.X) * t), from.Y + ((position.Y - from.Y) * t));
            foreach (var annotation in Context.Document.Annotations)
            {
                if (!annotation.IsVisible || _marked.Contains(annotation) || (!Context.EraserAllObjects && annotation is not StrokeAnnotation))
                {
                    continue;
                }

                if (annotation.HitTest(p, radius))
                {
                    _marked.Add(annotation);
                }
            }
        }

        _last = position;
        Context.Interaction.Marked = [.. _marked];
        Context.InvalidateInteraction();
    }
}
