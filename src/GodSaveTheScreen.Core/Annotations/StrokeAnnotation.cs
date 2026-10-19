using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Stützpunkt eines Freihandstrichs in Bildpixeln; <paramref name="Pressure"/> 0–1 (Maus: 1).</summary>
[JsonConverter(typeof(StrokePointJsonConverter))]
public readonly record struct StrokePoint(double X, double Y, double Pressure = 1)
{
    [JsonIgnore]
    public PointD Point => new(X, Y);
}

/// <summary>
/// Freihandstrich (Stift oder Textmarker). Farbe und Dicke liegen wie bei den Formen in <see cref="ShapeAnnotation.Stroke"/>;
/// die Füllung wird nicht verwendet.
/// </summary>
public sealed partial class StrokeAnnotation : ShapeAnnotation
{
    /// <summary>Toleranz der Punktreduktion beim Abschluss in Bildpixeln.</summary>
    public const double SimplifyTolerance = 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private ImmutableArray<StrokePoint> _points = ImmutableArray<StrokePoint>.Empty;

    /// <summary>Textmarker: breiter, transparenter Stift mit flachen Kappen, wird (optional) hinter den übrigen Objekten gezeichnet.</summary>
    [ObservableProperty]
    private bool _isHighlighter;

    /// <summary>Glättung 0 (Polygonzug) bis 1 (weiche Kurve).</summary>
    [ObservableProperty]
    private double _smoothing = 0.5;

    /// <summary>Strichbreite folgt dem Stiftdruck (nur wirksam, wenn die Punkte Druckwerte unter 1 enthalten).</summary>
    [ObservableProperty]
    private bool _usePressure = true;

    [JsonIgnore]
    public bool HasPressure => UsePressure && Points.Any(p => p.Pressure < 0.999);

    [JsonIgnore]
    public override RectD Bounds => RectD.BoundingBox(Points.Select(p => p.Point));

    [JsonIgnore]
    public override double VisualMargin => (Stroke.Thickness / 2) + 1;

    public override void MapPoints(Func<PointD, PointD> map) =>
        Points = [.. Points.Select(p =>
        {
            var m = map(p.Point);
            return p with { X = m.X, Y = m.Y };
        })];

    public override void ScaleSizes(double factor) => Stroke = Stroke with { Thickness = Math.Max(0.5, Stroke.Thickness * factor) };

    public override bool HitTest(PointD point, double tolerance)
    {
        if (Points.IsDefaultOrEmpty)
        {
            return false;
        }

        var reach = (Stroke.Thickness / 2) + tolerance;
        if (Points.Length == 1)
        {
            return point.DistanceTo(Points[0].Point) <= reach;
        }

        for (var i = 0; i < Points.Length - 1; i++)
        {
            if (HitTesting.DistanceToSegment(point, Points[i].Point, Points[i + 1].Point) <= reach)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Ramer-Douglas-Peucker-Reduktion für Freihandpunkte.</summary>
public static class StrokeSimplifier
{
    public static ImmutableArray<StrokePoint> Reduce(IReadOnlyList<StrokePoint> points, double tolerance = StrokeAnnotation.SimplifyTolerance)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count <= 2)
        {
            return [.. points];
        }

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            var maxDistance = 0.0;
            var index = -1;
            for (var i = first + 1; i < last; i++)
            {
                var d = HitTesting.DistanceToSegment(points[i].Point, points[first].Point, points[last].Point);
                if (d > maxDistance)
                {
                    maxDistance = d;
                    index = i;
                }
            }

            if (index >= 0 && maxDistance > tolerance)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }

        var result = ImmutableArray.CreateBuilder<StrokePoint>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result.ToImmutable();
    }
}
