using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

public enum TriangleKind
{
    Isosceles,
    Right,
}

public abstract partial class ShapeAnnotation : Annotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    private ArgbColor _fill = ArgbColor.Transparent;

    [ObservableProperty]
    private StrokeStyle _stroke = StrokeStyle.Default;

    /// <summary>Füllung vorhanden (Alpha 0 = keine Füllung).</summary>
    [JsonIgnore]
    public bool HasFill => Fill.A > 0;

    [JsonIgnore]
    public override double VisualMargin => Stroke.IsVisible ? Stroke.Thickness : 0;

    protected double HitDistance(double tolerance) => tolerance + (Stroke.IsVisible ? Stroke.Thickness / 2 : 0);
}

public abstract partial class BoxAnnotation : ShapeAnnotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private RectD _rect;

    [JsonIgnore]
    public override RectD Bounds => Rect.Normalize();

    public override void MapPoints(Func<PointD, PointD> map) =>
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));

    protected bool HitBox(PointD point, double tolerance)
    {
        var distance = HitDistance(tolerance);
        var outer = Bounds.Inflate(distance, distance);
        if (!outer.Contains(point))
        {
            return false;
        }

        if (HasFill)
        {
            return true;
        }

        var inner = Bounds.Inflate(-distance, -distance);
        return inner.IsEmpty || !inner.Contains(point);
    }
}

public sealed partial class RectangleAnnotation : BoxAnnotation
{
    [ObservableProperty]
    private double _cornerRadius;

    public override bool HitTest(PointD point, double tolerance) => HitBox(point, tolerance);
}

public sealed partial class EllipseAnnotation : BoxAnnotation
{
    public override bool HitTest(PointD point, double tolerance)
    {
        var distance = HitDistance(tolerance);
        var r = Bounds;
        var norm = HitTesting.EllipseNorm(point, r);
        if (norm == double.MaxValue)
        {
            // Entartete Ellipse (Linie): Abstand zur Mittellinie.
            return HitTesting.DistanceToSegment(point, r.TopLeft, r.BottomRight) <= distance;
        }

        var scaled = (norm - 1) * Math.Min(r.Width, r.Height) / 2;
        return HasFill ? scaled <= distance : Math.Abs(scaled) <= distance;
    }
}

public sealed partial class TriangleAnnotation : BoxAnnotation
{
    [ObservableProperty]
    private TriangleKind _kind;

    [ObservableProperty]
    private bool _isFlipped;

    [JsonIgnore]
    public ImmutableArray<PointD> Points => HitTesting.TrianglePoints(Rect, Kind, IsFlipped);

    public override bool HitTest(PointD point, double tolerance)
    {
        var points = Points;
        if (HasFill && HitTesting.PointInPolygon(point, points))
        {
            return true;
        }

        return HitTesting.DistanceToPolyline(point, points, closed: true) <= HitDistance(tolerance);
    }
}

public sealed partial class LineAnnotation : ShapeAnnotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD _start;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD _end;

    [ObservableProperty]
    private LineEndStyle _startCap;

    [ObservableProperty]
    private LineEndStyle _endCap;

    /// <summary>Größe der Spitzen als Vielfaches der Linienstärke.</summary>
    [ObservableProperty]
    private double _capSize = 4;

    [JsonIgnore]
    public override RectD Bounds => RectD.FromPoints(Start, End);

    [JsonIgnore]
    public override double VisualMargin =>
        Math.Max(base.VisualMargin, (StartCap != LineEndStyle.None || EndCap != LineEndStyle.None) ? Math.Max(CapSize * Stroke.Thickness, 6) : 0);

    public override void MapPoints(Func<PointD, PointD> map)
    {
        Start = map(Start);
        End = map(End);
    }

    public override bool HitTest(PointD point, double tolerance) =>
        HitTesting.DistanceToSegment(point, Start, End) <= HitDistance(tolerance);
}

public sealed partial class PolygonAnnotation : ShapeAnnotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private ImmutableArray<PointD> _points = ImmutableArray<PointD>.Empty;

    [ObservableProperty]
    private bool _isClosed = true;

    [JsonIgnore]
    public override RectD Bounds => RectD.BoundingBox(Points);

    public override void MapPoints(Func<PointD, PointD> map) => Points = [.. Points.Select(map)];

    public override bool HitTest(PointD point, double tolerance)
    {
        if (Points.IsDefaultOrEmpty)
        {
            return false;
        }

        if (IsClosed && HasFill && HitTesting.PointInPolygon(point, Points))
        {
            return true;
        }

        return HitTesting.DistanceToPolyline(point, Points, IsClosed) <= HitDistance(tolerance);
    }
}
