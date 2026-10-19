namespace GodSaveTheScreen.Core.Geometry;

public enum RulerSide
{
    /// <summary>Kante in Richtung der negativen Normalen (bei 0° die obere Kante).</summary>
    Top,

    /// <summary>Kante in Richtung der positiven Normalen (bei 0° die untere Kante).</summary>
    Bottom,
}

/// <summary>Lage des virtuellen Lineals in Bildpixeln; <paramref name="Width"/> ist die Bandbreite (quer zur Achse).</summary>
public readonly record struct RulerPlacement(PointD Center, double AngleDegrees, double Length, double Width);

/// <summary>Reine Geometrie des virtuellen Lineals: Einrasten von Freihandpunkten an der Kante.</summary>
public static class RulerGeometry
{
    public static VectorD Axis(RulerPlacement ruler)
    {
        var a = ruler.AngleDegrees * Math.PI / 180;
        return new VectorD(Math.Cos(a), Math.Sin(a));
    }

    public static VectorD Normal(RulerPlacement ruler)
    {
        var axis = Axis(ruler);
        return new VectorD(-axis.Y, axis.X);
    }

    /// <summary>Lotrechter Abstand (vorzeichenbehaftet, positiv = Seite <see cref="RulerSide.Bottom"/>) zur Mittelachse.</summary>
    public static double SignedDistance(PointD point, RulerPlacement ruler)
    {
        var n = Normal(ruler);
        return ((point.X - ruler.Center.X) * n.X) + ((point.Y - ruler.Center.Y) * n.Y);
    }

    /// <summary>Position entlang der Achse relativ zur Mitte.</summary>
    public static double AlongAxis(PointD point, RulerPlacement ruler)
    {
        var a = Axis(ruler);
        return ((point.X - ruler.Center.X) * a.X) + ((point.Y - ruler.Center.Y) * a.Y);
    }

    /// <summary>Liegt der Punkt im Band des Lineals?</summary>
    public static bool Contains(PointD point, RulerPlacement ruler) =>
        Math.Abs(SignedDistance(point, ruler)) <= ruler.Width / 2 && Math.Abs(AlongAxis(point, ruler)) <= ruler.Length / 2;

    /// <summary>Nächste Kante innerhalb von <paramref name="maxDistance"/> (Abstand zur Kantenlinie, auch außerhalb des Bands), sonst <c>null</c>.</summary>
    public static RulerSide? NearestEdge(PointD point, RulerPlacement ruler, double maxDistance)
    {
        if (Math.Abs(AlongAxis(point, ruler)) > (ruler.Length / 2) + maxDistance)
        {
            return null;
        }

        var d = SignedDistance(point, ruler);
        var top = Math.Abs(d + (ruler.Width / 2));
        var bottom = Math.Abs(d - (ruler.Width / 2));
        if (Math.Min(top, bottom) > maxDistance)
        {
            return null;
        }

        return top <= bottom ? RulerSide.Top : RulerSide.Bottom;
    }

    /// <summary>
    /// Projiziert den Punkt auf die Gerade der Kante, um die halbe Strichbreite nach außen versetzt
    /// (der Strich liegt neben dem Lineal, nicht darunter).
    /// </summary>
    public static PointD Snap(PointD point, RulerPlacement ruler, RulerSide side, double strokeThickness)
    {
        var n = Normal(ruler);
        var sign = side == RulerSide.Bottom ? 1.0 : -1.0;
        var target = sign * ((ruler.Width / 2) + (strokeThickness / 2));
        var delta = target - SignedDistance(point, ruler);
        return new PointD(point.X + (n.X * delta), point.Y + (n.Y * delta));
    }
}
