using System.Windows.Input;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Benennung und Anzeigenamen von Objekten.</summary>
public static class AnnotationNames
{
    public static string KindName(Annotation annotation) => (Strings.ResourceManager.GetString(KindKey(annotation)) ?? KindKey(annotation));

    public static string KindKey(Annotation annotation) => annotation switch
    {
        RectangleAnnotation => "Shape_Rectangle",
        EllipseAnnotation => "Shape_Ellipse",
        TriangleAnnotation => "Shape_Triangle",
        TextAnnotation => "Shape_Text",
        MarkerAnnotation => "Shape_Marker",
        PolygonAnnotation => "Shape_Polygon",
        StrokeAnnotation { IsHighlighter: true } => "Shape_Highlighter",
        StrokeAnnotation => "Shape_Pen",
        BlurAnnotation => "Shape_Blur",
        MagnifierAnnotation => "Shape_Magnifier",
        EmojiAnnotation => "Shape_Emoji",
        StampAnnotation => "Shape_Stamp",
        LineAnnotation { EndCap: not LineEndStyle.None } or LineAnnotation { StartCap: not LineEndStyle.None } => "Shape_Arrow",
        LineAnnotation => "Shape_Line",
        _ => annotation.GetType().Name,
    };

    /// <summary>Name für ein neues Objekt: „Rechteck 3“.</summary>
    public static string NextName(IEnumerable<Annotation> existing, Annotation annotation)
    {
        var key = KindKey(annotation);
        var count = existing.Count(a => KindKey(a) == key);
        return $"{KindName(annotation)} {count + 1}";
    }

    public static string DisplayName(Annotation annotation)
    {
        if (!string.IsNullOrWhiteSpace(annotation.Name))
        {
            return annotation.Name;
        }

        if (annotation is EmojiAnnotation emoji)
        {
            return EmojiCatalog.Shared.Find(emoji.Sequence)?.GetName() ?? KindName(annotation);
        }

        return annotation is TextAnnotation { Text.Length: > 0 } text ? text.Text.Split('\n')[0] : KindName(annotation);
    }
}

/// <summary>Gemeinsame Logik der Formwerkzeuge: Aufziehen mit Vorschau, Einrasten, Übernehmen als Undo-Schritt.</summary>
public abstract class ShapeToolBase<T> : ToolBase
    where T : Annotation, new()
{
    private const double MinimumSize = 2;

    private PointD _start;
    private T? _preview;

    protected ShapeToolBase(EditorContext context)
        : base(context)
    {
    }

    public override bool HasStyleDefaults => true;

    public override bool UsesShapeStyle => true;

    protected abstract void UpdateGeometry(T shape, PointD start, PointD current, ModifierKeys modifiers);

    protected abstract bool IsLargeEnough(T shape);

    protected virtual void ApplyExtra(T shape, ShapeStyle style)
    {
    }

    public override void Deactivate() => Cancel();

    public override void Cancel()
    {
        if (_preview is null)
        {
            return;
        }

        _preview = null;
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        if (args.Ctrl)
        {
            // Strg + Klick auf ein vorhandenes Objekt wählt es aus, statt zu zeichnen.
            var hit = Context.HitTest(args.Position);
            if (hit is not null)
            {
                Context.Selection.Set(hit);
                return;
            }
        }

        _start = args.Position;
        var shape = CreateShape();
        UpdateGeometry(shape, _start, _start, args.Modifiers);
        _preview = shape;
        Context.Interaction.Preview = shape;
        Context.Selection.Clear();
        Context.InvalidateInteraction();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        UpdateGeometry(_preview, _start, args.Position, args.Modifiers);
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        var shape = _preview;
        UpdateGeometry(shape, _start, args.Position, args.Modifiers);
        _preview = null;
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
        if (IsLargeEnough(shape))
        {
            Commit(shape);
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

    protected static bool ExceedsMinimum(double width, double height) => Math.Max(Math.Abs(width), Math.Abs(height)) >= MinimumSize;

    protected T CreateShape()
    {
        var style = Context.Defaults.GetShape(Id, BuiltInStyle);
        var shape = new T { Opacity = style.Opacity };
        if (shape is ShapeAnnotation styled)
        {
            styled.Stroke = style.Stroke;
            styled.Fill = style.Fill;
        }

        ApplyExtra(shape, style);
        return shape;
    }

    /// <summary>Fügt das fertige Objekt als einen Undo-Schritt ein und wählt es aus.</summary>
    protected void Commit(T shape)
    {
        shape.Name = AnnotationNames.NextName(Context.Document.Annotations, shape);
        Context.Undo.Execute(new AddAnnotationAction(Context.Document.Annotations, shape));
        Context.Selection.Set(shape);
        if (Context.Settings.Current.SwitchToSelectAfterDraw)
        {
            Context.ActivateTool(SelectTool.ToolId);
        }
    }
}

public abstract class BoxToolBase<T> : ShapeToolBase<T>
    where T : BoxAnnotation, new()
{
    protected BoxToolBase(EditorContext context)
        : base(context)
    {
    }

    protected override void UpdateGeometry(T shape, PointD start, PointD current, ModifierKeys modifiers) =>
        shape.Rect = ShapeGeometry.ConstrainBox(start, current, modifiers);

    protected override bool IsLargeEnough(T shape) => ExceedsMinimum(shape.Rect.Width, shape.Rect.Height);
}

public sealed class RectangleTool : BoxToolBase<RectangleAnnotation>
{
    public const string ToolId = "rectangle";

    public RectangleTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Rectangle";

    public override SymbolRegular Symbol => SymbolRegular.RectangleLandscape24;

    public override Key ShortcutKey => Key.R;

    protected override void ApplyExtra(RectangleAnnotation shape, ShapeStyle style) => shape.CornerRadius = style.CornerRadius;
}

public sealed class EllipseTool : BoxToolBase<EllipseAnnotation>
{
    public const string ToolId = "ellipse";

    public EllipseTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Ellipse";

    public override SymbolRegular Symbol => SymbolRegular.Circle24;

    public override Key ShortcutKey => Key.E;
}

public sealed class TriangleTool : BoxToolBase<TriangleAnnotation>
{
    public const string ToolId = "triangle";

    public TriangleTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Triangle";

    public override SymbolRegular Symbol => SymbolRegular.Shapes24;

    public override string? IconPath => "M12,3.5 L21,20 L3,20 Z";

    public override Key ShortcutKey => Key.G;

    protected override void UpdateGeometry(TriangleAnnotation shape, PointD start, PointD current, ModifierKeys modifiers)
    {
        base.UpdateGeometry(shape, start, current, modifiers);

        // Zieht man nach oben, steht das Dreieck auf dem Kopf.
        shape.IsFlipped = current.Y < start.Y;
    }
}

/// <summary>Linie bzw. Pfeil (Pfeil = Linie mit Spitze am Ende).</summary>
public class LineTool : ShapeToolBase<LineAnnotation>
{
    public const string ToolId = "line";

    public LineTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Line";

    public override SymbolRegular Symbol => SymbolRegular.Line24;

    public override Key ShortcutKey => Key.L;

    protected override void UpdateGeometry(LineAnnotation shape, PointD start, PointD current, ModifierKeys modifiers)
    {
        var (s, e) = ShapeGeometry.ConstrainLine(start, current, modifiers);
        shape.Start = s;
        shape.End = e;
    }

    protected override bool IsLargeEnough(LineAnnotation shape) =>
        ExceedsMinimum(shape.End.X - shape.Start.X, shape.End.Y - shape.Start.Y);

    protected override void ApplyExtra(LineAnnotation shape, ShapeStyle style)
    {
        shape.StartCap = style.StartCap;
        shape.EndCap = style.EndCap;
    }
}

public sealed class ArrowTool : LineTool
{
    public new const string ToolId = "arrow";

    public ArrowTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Arrow";

    public override SymbolRegular Symbol => SymbolRegular.ArrowUpRight24;

    public override Key ShortcutKey => Key.A;

    public override ShapeStyle BuiltInStyle => new() { EndCap = LineEndStyle.Arrow };
}

/// <summary>Polygon: Klick je Punkt, Doppelklick/<c>Enter</c> schließt, <c>Esc</c> bricht ab.</summary>
public sealed class PolygonTool : ShapeToolBase<PolygonAnnotation>
{
    public const string ToolId = "polygon";

    private PolygonAnnotation? _building;
    private List<PointD> _points = [];

    public PolygonTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Polygon";

    public override SymbolRegular Symbol => SymbolRegular.Shapes24;

    public override string? IconPath => "M12,3 L21,9.8 L17.6,20.5 L6.4,20.5 L3,9.8 Z";

    public override Key ShortcutKey => Key.P;

    public override void Cancel()
    {
        if (_building is null)
        {
            return;
        }

        _building = null;
        _points = [];
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        if (_building is null && args.Ctrl && Context.HitTest(args.Position) is { } hit)
        {
            Context.Selection.Set(hit);
            return;
        }

        var point = args.Shift && _points.Count > 0 ? ShapeGeometry.SnapAngle(_points[^1], args.Position) : args.Position;
        if (_building is null)
        {
            _building = CreateShape();
            _building.IsClosed = false;
            Context.Interaction.Preview = _building;
            Context.Selection.Clear();
        }

        if (args.ClickCount >= 2)
        {
            // Der erste Klick des Doppelklicks hat den Punkt schon gesetzt.
            Finish();
            return;
        }

        _points.Add(point);
        UpdatePreview(point);
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_building is null)
        {
            return;
        }

        var point = args.Shift && _points.Count > 0 ? ShapeGeometry.SnapAngle(_points[^1], args.Position) : args.Position;
        UpdatePreview(point);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (_building is null)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
                Finish();
                return true;
            case Key.Escape:
                Cancel();
                return true;
            case Key.Back when _points.Count > 0:
                _points.RemoveAt(_points.Count - 1);
                if (_points.Count == 0)
                {
                    Cancel();
                }
                else
                {
                    UpdatePreview(_points[^1]);
                }

                return true;
            default:
                return false;
        }
    }

    protected override void UpdateGeometry(PolygonAnnotation shape, PointD start, PointD current, ModifierKeys modifiers)
    {
    }

    protected override bool IsLargeEnough(PolygonAnnotation shape) => shape.Points.Length >= 3;

    private void UpdatePreview(PointD hover)
    {
        if (_building is null)
        {
            return;
        }

        _building.Points = [.. _points, hover];
        Context.InvalidateInteraction();
    }

    private void Finish()
    {
        var building = _building;
        var points = _points;
        Cancel();
        if (building is null)
        {
            return;
        }

        // Doppelte Endpunkte (Doppelklick) entfernen.
        var distinct = new List<PointD>();
        foreach (var p in points)
        {
            if (distinct.Count == 0 || distinct[^1].DistanceTo(p) > 0.5)
            {
                distinct.Add(p);
            }
        }

        if (distinct.Count < 3)
        {
            return;
        }

        building.Points = [.. distinct];
        building.IsClosed = true;
        Commit(building);
    }
}
