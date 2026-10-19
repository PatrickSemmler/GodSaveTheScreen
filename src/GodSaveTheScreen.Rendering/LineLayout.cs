using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Rendering;

/// <summary>Geometrie einer Linienspitze. <see cref="Points"/> bei Pfeil/Quadrat; <see cref="Radius"/> &gt; 0 bei Kreis.</summary>
public sealed record CapGeometry(LineEndStyle Style, IReadOnlyList<PointD> Points, PointD Center, double Radius);

/// <summary>Ergebnis der Berechnung: auf die Spitzenbasis gekürzte Linie plus Spitzen.</summary>
public sealed record LineLayoutResult(PointD LineStart, PointD LineEnd, CapGeometry? StartCap, CapGeometry? EndCap);

/// <summary>Berechnet Linie und Spitzen; die Linie endet an der Spitzenbasis (kein Überstand bei dicken Linien).</summary>
public static class LineLayout
{
    public static double CapLength(double thickness, double capSize) => Math.Max(capSize * thickness, 6);

    public static LineLayoutResult Compute(PointD start, PointD end, double thickness, LineEndStyle startCap, LineEndStyle endCap, double capSize)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-6)
        {
            return new LineLayoutResult(start, end, null, null);
        }

        var ux = dx / length;
        var uy = dy / length;
        var capLength = CapLength(thickness, capSize);

        var startGeometry = Build(startCap, start, new VectorD(-ux, -uy), capLength);
        var endGeometry = Build(endCap, end, new VectorD(ux, uy), capLength);

        var trimStart = Math.Min(Trim(startCap, capLength), length * 0.45);
        var trimEnd = Math.Min(Trim(endCap, capLength), length * 0.45);
        return new LineLayoutResult(
            new PointD(start.X + (ux * trimStart), start.Y + (uy * trimStart)),
            new PointD(end.X - (ux * trimEnd), end.Y - (uy * trimEnd)),
            startGeometry,
            endGeometry);
    }

    private static double Trim(LineEndStyle style, double capLength) => style switch
    {
        LineEndStyle.Arrow => capLength,
        LineEndStyle.Circle => capLength * 0.4,
        LineEndStyle.Square => capLength * 0.35,
        _ => 0,
    };

    /// <summary>Spitze am Punkt <paramref name="tip"/>; <paramref name="outward"/> zeigt vom Linienende weg.</summary>
    private static CapGeometry? Build(LineEndStyle style, PointD tip, VectorD outward, double capLength)
    {
        if (style == LineEndStyle.None)
        {
            return null;
        }

        // Normale zur Linienrichtung.
        var nx = -outward.Y;
        var ny = outward.X;
        var halfWidth = capLength * 0.45;
        var baseCenter = new PointD(tip.X - (outward.X * capLength), tip.Y - (outward.Y * capLength));
        var left = new PointD(baseCenter.X + (nx * halfWidth), baseCenter.Y + (ny * halfWidth));
        var right = new PointD(baseCenter.X - (nx * halfWidth), baseCenter.Y - (ny * halfWidth));

        switch (style)
        {
            case LineEndStyle.Arrow:
                return new CapGeometry(style, [tip, left, right], tip, 0);
            case LineEndStyle.OpenArrow:
                return new CapGeometry(style, [left, tip, right], tip, 0);
            case LineEndStyle.Circle:
                return new CapGeometry(style, [], tip, capLength * 0.4);
            default:
                var half = capLength * 0.35;
                var points = new PointD[]
                {
                    new(tip.X + (nx * half) + (outward.X * half), tip.Y + (ny * half) + (outward.Y * half)),
                    new(tip.X - (nx * half) + (outward.X * half), tip.Y - (ny * half) + (outward.Y * half)),
                    new(tip.X - (nx * half) - (outward.X * half), tip.Y - (ny * half) - (outward.Y * half)),
                    new(tip.X + (nx * half) - (outward.X * half), tip.Y + (ny * half) - (outward.Y * half)),
                };
                return new CapGeometry(style, points, tip, 0);
        }
    }
}
