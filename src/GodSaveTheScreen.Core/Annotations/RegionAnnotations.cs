using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

public enum BlurMode
{
    GaussianBlur,
    Pixelate,
    Redact,
}

public enum RegionShape
{
    Rectangle,
    Ellipse,
}

public enum MagnifierShape
{
    Circle,
    Rectangle,
}

/// <summary>Maskierter Bereich: Weichzeichnen, Verpixeln oder Schwärzen. Quelle ist immer das Basisbild.</summary>
public sealed partial class BlurAnnotation : Annotation
{
    public const double MinStrength = 2;
    public const double MaxStrength = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private RectD _rect;

    [ObservableProperty]
    private BlurMode _mode = BlurMode.Pixelate;

    /// <summary>Radius (Weichzeichnen) bzw. Blockgröße (Verpixeln) in Bildpixeln.</summary>
    [ObservableProperty]
    private double _strength = 14;

    [ObservableProperty]
    private RegionShape _shape;

    /// <summary>Breite des weichen Randes in Bildpixeln (0 = harter Rand).</summary>
    [ObservableProperty]
    private double _featherEdge;

    [ObservableProperty]
    private ArgbColor _redactColor = ArgbColor.Black;

    [JsonIgnore]
    public override RectD Bounds => Rect.Normalize();

    public override void MapPoints(Func<PointD, PointD> map) =>
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));

    public override bool HitTest(PointD point, double tolerance)
    {
        var bounds = Bounds.Inflate(tolerance, tolerance);
        if (!bounds.Contains(point))
        {
            return false;
        }

        return Shape == RegionShape.Rectangle || HitTesting.EllipseNorm(point, bounds) <= 1;
    }
}

/// <summary>Lupe: vergrößert <see cref="SourceRect"/> an <see cref="TargetCenter"/>. Die Lupengröße ist <c>SourceRect.Size × Zoom</c>.</summary>
public sealed partial class MagnifierAnnotation : Annotation
{
    public const double MinZoom = 1.5;
    public const double MaxZoom = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(TargetRect))]
    private RectD _sourceRect;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(TargetRect))]
    private PointD _targetCenter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(TargetRect))]
    private double _zoom = 2;

    [ObservableProperty]
    private MagnifierShape _shape = MagnifierShape.Circle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisualMargin))]
    private StrokeStyle _border = new(ArgbColor.FromRgb(0x33, 0x33, 0x33), 3);

    /// <summary>Verbindung zwischen Quelle und Lupe (Kegel bei runder Lupe, sonst Linie).</summary>
    [ObservableProperty]
    private bool _showConnector = true;

    [ObservableProperty]
    private bool _showSourceFrame = true;

    /// <summary>Vergrößert nur das Basisbild, ohne die darunterliegenden Markierungen.</summary>
    [ObservableProperty]
    private bool _baseImageOnly;

    /// <summary>Glatte Skalierung (sonst pixelig).</summary>
    [ObservableProperty]
    private bool _smoothScaling = true;

    [JsonIgnore]
    public RectD TargetRect
    {
        get
        {
            var s = SourceRect.Normalize();
            var w = s.Width * Zoom;
            var h = s.Height * Zoom;
            return new RectD(TargetCenter.X - (w / 2), TargetCenter.Y - (h / 2), w, h);
        }
    }

    [JsonIgnore]
    public override RectD Bounds => SourceRect.Normalize().Union(TargetRect);

    [JsonIgnore]
    public override double VisualMargin => Border.IsVisible ? (Border.Thickness / 2) + 1 : 1;

    public override void MapPoints(Func<PointD, PointD> map)
    {
        SourceRect = RectD.FromPoints(map(SourceRect.TopLeft), map(SourceRect.BottomRight));
        TargetCenter = map(TargetCenter);
    }

    public override bool HitTest(PointD point, double tolerance)
    {
        var target = TargetRect.Inflate(tolerance, tolerance);
        if (target.Contains(point) && (Shape == MagnifierShape.Rectangle || HitTesting.EllipseNorm(point, target) <= 1))
        {
            return true;
        }

        if (!ShowSourceFrame)
        {
            return false;
        }

        var s = SourceRect.Normalize();
        var outer = s.Inflate(tolerance, tolerance);
        var inner = s.Inflate(-tolerance, -tolerance);
        return outer.Contains(point) && (inner.IsEmpty || !inner.Contains(point));
    }
}

/// <summary>Geometrie der Lupe: Verbinder (Außentangenten) zwischen Quell- und Zielkreis.</summary>
public static class MagnifierGeometry
{
    /// <summary>
    /// Die beiden äußeren Tangenten zwischen zwei Kreisen; <c>null</c>, wenn ein Kreis den anderen umschließt.
    /// </summary>
    public static ((PointD From, PointD To) First, (PointD From, PointD To) Second)? ConnectorTangents(PointD c1, double r1, PointD c2, double r2)
    {
        var dx = c2.X - c1.X;
        var dy = c2.Y - c1.Y;
        var d = Math.Sqrt((dx * dx) + (dy * dy));
        if (d <= Math.Abs(r1 - r2) + 1e-9)
        {
            return null;
        }

        var ux = dx / d;
        var uy = dy / d;
        var cos = (r1 - r2) / d;
        var sin = Math.Sqrt(Math.Max(0, 1 - (cos * cos)));

        (PointD, PointD) Side(double s)
        {
            var nx = (ux * cos) - (uy * sin * s);
            var ny = (uy * cos) + (ux * sin * s);
            return (new PointD(c1.X + (nx * r1), c1.Y + (ny * r1)), new PointD(c2.X + (nx * r2), c2.Y + (ny * r2)));
        }

        return (Side(1), Side(-1));
    }

    /// <summary>Radius des Kreises, der in das Rechteck passt.</summary>
    public static double InscribedRadius(RectD rect)
    {
        var r = rect.Normalize();
        return Math.Min(r.Width, r.Height) / 2;
    }

    /// <summary>Anfangsposition der Lupe: rechts unterhalb der Quelle, im Bild geklemmt.</summary>
    public static PointD DefaultTargetCenter(RectD source, double zoom, double imageWidth, double imageHeight)
    {
        var s = source.Normalize();
        var w = s.Width * zoom;
        var h = s.Height * zoom;
        var x = s.Right + (w / 2) + 12;
        var y = s.Bottom + (h / 2) + 12;
        x = Math.Clamp(x, w / 2, Math.Max(w / 2, imageWidth - (w / 2)));
        y = Math.Clamp(y, h / 2, Math.Max(h / 2, imageHeight - (h / 2)));
        return new PointD(x, y);
    }
}
