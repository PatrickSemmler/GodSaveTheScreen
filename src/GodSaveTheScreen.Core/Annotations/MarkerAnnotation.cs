using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

public enum MarkerShape
{
    Circle,
    Square,
    Rounded,
}

/// <summary>Nummerierter Schritt-Marker, optional mit Spitze zu einem Zielpunkt (Sprechblase).</summary>
public sealed partial class MarkerAnnotation : Annotation
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD _center;

    [ObservableProperty]
    private int _number = 1;

    [ObservableProperty]
    private MarkerShape _shape;

    /// <summary>Durchmesser bzw. Kantenlänge in Bildpixeln.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _size = 32;

    [ObservableProperty]
    private ArgbColor _fill = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    [ObservableProperty]
    private StrokeStyle _stroke = new(ArgbColor.White, 2);

    [ObservableProperty]
    private ArgbColor _textColor = ArgbColor.White;

    [ObservableProperty]
    private string _fontFamilyName = "Segoe UI";

    /// <summary>Spitze der Sprechblase; <c>null</c> = keine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD? _pointerTarget;

    /// <summary>Erstellzeitpunkt (UTC-Ticks) für die Neunummerierung nach Erstellreihenfolge.</summary>
    [ObservableProperty]
    private long _created = DateTime.UtcNow.Ticks;

    [JsonIgnore]
    public double Radius => Size / 2;

    [JsonIgnore]
    public override RectD Bounds
    {
        get
        {
            var body = new RectD(Center.X - Radius, Center.Y - Radius, Size, Size);
            return PointerTarget is { } target
                ? body.Union(new RectD(target.X, target.Y, 0, 0))
                : body;
        }
    }

    [JsonIgnore]
    public override double VisualMargin => (Stroke.IsVisible ? Stroke.Thickness / 2 : 0) + 1;

    public override void MapPoints(Func<PointD, PointD> map)
    {
        Center = map(Center);
        if (PointerTarget is { } target)
        {
            PointerTarget = map(target);
        }
    }

    public override void ScaleSizes(double factor) => Size *= factor;

    public override bool HitTest(PointD point, double tolerance)
    {
        var inBody = Shape == MarkerShape.Circle
            ? point.DistanceTo(Center) <= Radius + tolerance
            : new RectD(Center.X - Radius, Center.Y - Radius, Size, Size).Inflate(tolerance, tolerance).Contains(point);
        if (inBody)
        {
            return true;
        }

        return PointerTarget is { } target
               && HitTesting.DistanceToSegment(point, Center, target) <= (Size * 0.3) + tolerance;
    }
}

public enum MarkerOrder
{
    /// <summary>Reihenfolge in der Ebenenliste (unten nach oben).</summary>
    ZOrder,

    /// <summary>Reihenfolge der Erstellung.</summary>
    Creation,
}

/// <summary>Nummerierungslogik der Marker.</summary>
public static class MarkerNumbering
{
    /// <summary>Nächste freie Nummer: größte vorhandene + 1, sonst <paramref name="start"/>.</summary>
    public static int NextNumber(IEnumerable<Annotation> annotations, int start = 1)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var numbers = annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ToList();
        return numbers.Count == 0 ? start : numbers.Max() + 1;
    }

    /// <summary>Berechnet lückenlose Nummern ab <paramref name="start"/> in der gewünschten Reihenfolge (ohne etwas zu ändern).</summary>
    public static IReadOnlyList<(MarkerAnnotation Marker, int Number)> Renumber(IEnumerable<Annotation> annotations, MarkerOrder order, int start = 1)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var markers = annotations.OfType<MarkerAnnotation>().ToList();
        var ordered = order == MarkerOrder.Creation
            ? markers.Select((m, i) => (m, i)).OrderBy(x => x.m.Created).ThenBy(x => x.i).Select(x => x.m).ToList()
            : markers;
        return [.. ordered.Select((m, i) => (m, start + i))];
    }
}
