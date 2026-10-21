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

public abstract partial class ShapeAnnotation : Annotation, IShadowAnnotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFill))]
    private ArgbColor _fill = ArgbColor.Transparent;

    [ObservableProperty]
    private StrokeStyle _stroke = StrokeStyle.Default;

    [ObservableProperty]
    private ShadowStyle _shadow = ShadowStyle.None;

    /// <summary>Füllung vorhanden (Alpha 0 = keine Füllung).</summary>
    [JsonIgnore]
    public bool HasFill => Fill.A > 0;

    [JsonIgnore]
    public override double VisualMargin => Stroke.IsVisible ? Stroke.Thickness : 0;

    protected double HitDistance(double tolerance) => tolerance + (Stroke.IsVisible ? Stroke.Thickness / 2 : 0);
}

/// <summary>
/// Form in einem Rahmen (Rechteck, Ellipse, Dreieck). <see cref="Rect"/> ist der Rahmen vor der Drehung, gedreht wird um
/// dessen Mitte; <see cref="Bounds"/> ist die achsparallele Hülle des gedrehten Rahmens.
/// </summary>
public abstract partial class BoxAnnotation : ShapeAnnotation, IRotatableAnnotation
{
    private double _rotation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Center))]
    private RectD _rect;

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, RotationMath.Normalize(value)))
            {
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    [JsonIgnore]
    public PointD Center => Rect.Normalize().Center;

    [JsonIgnore]
    public override RectD Bounds => RotationMath.Hull(Rect, Rotation);

    [JsonIgnore]
    public override double VisualMargin => base.VisualMargin * RotationMath.HullFactor(Rotation);

    public override void MapPoints(Func<PointD, PointD> map) =>
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));

    /// <param name="point">Punkt im unverdrehten System (<see cref="RotationMath.ToLocal"/>).</param>
    protected bool HitBox(PointD point, double tolerance)
    {
        var distance = HitDistance(tolerance);
        var rect = Rect.Normalize();
        var outer = rect.Inflate(distance, distance);
        if (!outer.Contains(point))
        {
            return false;
        }

        if (HasFill)
        {
            return true;
        }

        var inner = rect.Inflate(-distance, -distance);
        return inner.IsEmpty || !inner.Contains(point);
    }
}

public sealed partial class RectangleAnnotation : BoxAnnotation
{
    [ObservableProperty]
    private double _cornerRadius;

    public override bool HitTest(PointD point, double tolerance) => HitBox(this.ToLocal(point), tolerance);
}

public sealed partial class EllipseAnnotation : BoxAnnotation
{
    public override bool HitTest(PointD point, double tolerance)
    {
        point = this.ToLocal(point);
        var distance = HitDistance(tolerance);
        var r = Rect.Normalize();
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
        point = this.ToLocal(point);
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

    /// <summary>
    /// Kontur um Linie und Spitzen (Stärke = Breite außerhalb der Linie); Stärke 0 = keine. Folgt der Strichelung.
    /// Strichart, Verbindung und Enden der Kontur werden nicht verwendet.
    /// </summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private StrokeStyle _outline = NoOutline;

    /// <summary>Weicher Rand der Kontur in Bildpixeln; 0 = scharfe Kontur. Die Linie bleibt scharf.</summary>
    [ObservableProperty]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private double _outlineBlur;

    /// <summary>Keine Kontur (Weiß, Stärke 0) – Standard neuer Linien.</summary>
    public static StrokeStyle NoOutline { get; } = new(ArgbColor.White, 0);

    [JsonIgnore]
    public override RectD Bounds => RectD.FromPoints(Start, End);

    [JsonIgnore]
    public override double VisualMargin =>
        Math.Max(base.VisualMargin, (StartCap != LineEndStyle.None || EndCap != LineEndStyle.None) ? Math.Max(CapSize * Stroke.Thickness, 6) : 0)
        + (Outline.IsVisible ? Outline.Thickness + Math.Clamp(OutlineBlur, 0, TextAnnotation.MaxOutlineBlur) : 0);

    /// <summary>Kontur im Projektformat: nur geschrieben, wenn sie vom Standard abweicht (ältere Dateien bleiben gleich).</summary>
    [JsonInclude]
    [JsonPropertyName(nameof(Outline))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private StrokeStyle? OutlineData
    {
        get => Outline == NoOutline ? null : Outline;
        set => Outline = value ?? NoOutline;
    }

    public override void MapPoints(Func<PointD, PointD> map)
    {
        Start = map(Start);
        End = map(End);
    }

    public override bool HitTest(PointD point, double tolerance) =>
        HitTesting.DistanceToSegment(point, Start, End) <= HitDistance(tolerance);
}

/// <summary>
/// Polygon bzw. Polygonzug. <see cref="Points"/> liegen unverdreht; gedreht wird um die Mitte ihres Rahmens
/// (<see cref="Rect"/>). <see cref="Bounds"/> umschließt die gedrehten Punkte.
/// </summary>
public sealed partial class PolygonAnnotation : ShapeAnnotation, IRotatableAnnotation
{
    private double _rotation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    [NotifyPropertyChangedFor(nameof(Center))]
    private ImmutableArray<PointD> _points = ImmutableArray<PointD>.Empty;

    [ObservableProperty]
    private bool _isClosed = true;

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, RotationMath.Normalize(value)))
            {
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    /// <summary>Rahmen der unverdrehten Punkte.</summary>
    [JsonIgnore]
    public RectD Rect => RectD.BoundingBox(Points);

    [JsonIgnore]
    public PointD Center => Rect.Center;

    /// <summary>Eckpunkte in Bildkoordinaten (gedreht).</summary>
    [JsonIgnore]
    public IEnumerable<PointD> WorldPoints => Rotation == 0 ? Points : Points.Select(this.ToWorld);

    [JsonIgnore]
    public override RectD Bounds => RectD.BoundingBox(WorldPoints);

    public override void MapPoints(Func<PointD, PointD> map) => Points = [.. Points.Select(map)];

    public override bool HitTest(PointD point, double tolerance)
    {
        if (Points.IsDefaultOrEmpty)
        {
            return false;
        }

        point = this.ToLocal(point);

        if (IsClosed && HasFill && HitTesting.PointInPolygon(point, Points))
        {
            return true;
        }

        return HitTesting.DistanceToPolyline(point, Points, IsClosed) <= HitDistance(tolerance);
    }
}
