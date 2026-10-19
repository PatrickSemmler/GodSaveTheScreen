using System.Collections.Immutable;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Geometrische Hilfen für Treffertests (Bildpixel).</summary>
public static class HitTesting
{
    public static double DistanceToSegment(PointD p, PointD a, PointD b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = (abx * abx) + (aby * aby);
        if (lengthSquared < 1e-12)
        {
            return p.DistanceTo(a);
        }

        var t = Math.Clamp((((p.X - a.X) * abx) + ((p.Y - a.Y) * aby)) / lengthSquared, 0, 1);
        return p.DistanceTo(new PointD(a.X + (t * abx), a.Y + (t * aby)));
    }

    /// <summary>Even-Odd-Regel.</summary>
    public static bool PointInPolygon(PointD p, IReadOnlyList<PointD> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < ((b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y)) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static double DistanceToPolyline(PointD p, IReadOnlyList<PointD> points, bool closed)
    {
        if (points.Count == 0)
        {
            return double.MaxValue;
        }

        if (points.Count == 1)
        {
            return p.DistanceTo(points[0]);
        }

        var best = double.MaxValue;
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
        {
            best = Math.Min(best, DistanceToSegment(p, points[i], points[(i + 1) % points.Count]));
        }

        return best;
    }

    /// <summary>Normierter Abstand zum Ellipsenmittelpunkt: &lt;1 innen, 1 auf dem Rand; <see cref="double.MaxValue"/> bei entarteter Ellipse.</summary>
    public static double EllipseNorm(PointD p, RectD rect)
    {
        var r = rect.Normalize();
        var a = r.Width / 2;
        var b = r.Height / 2;
        if (a < 1e-9 || b < 1e-9)
        {
            return double.MaxValue;
        }

        var dx = (p.X - r.Center.X) / a;
        var dy = (p.Y - r.Center.Y) / b;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public static ImmutableArray<PointD> TrianglePoints(RectD rect, TriangleKind kind, bool flipped)
    {
        var r = rect.Normalize();
        var top = flipped ? r.Bottom : r.Y;
        var bottom = flipped ? r.Y : r.Bottom;
        return kind == TriangleKind.Right
            ? [new PointD(r.X, top), new PointD(r.X, bottom), new PointD(r.Right, bottom)]
            : [new PointD(r.Center.X, top), new PointD(r.Right, bottom), new PointD(r.X, bottom)];
    }
}
