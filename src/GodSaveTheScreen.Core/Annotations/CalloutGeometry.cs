using System.Collections.Immutable;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Kreis der Spitze einer Gedankenblase.</summary>
public readonly record struct CalloutBubble(PointD Center, double Radius);

/// <summary>
/// Spitze einer Sprechblase: Dreieck von <see cref="BaseStart"/> über <see cref="Tip"/> nach <see cref="BaseEnd"/>
/// (gebogen, wenn <see cref="ControlStart"/>/<see cref="ControlEnd"/> gesetzt sind: quadratische Kurven) oder bei der
/// Gedankenblase eine Kette kleiner werdender Kreise (<see cref="Bubbles"/>).
/// </summary>
public sealed record CalloutTail(PointD BaseStart, PointD BaseEnd, PointD Tip, PointD? ControlStart, PointD? ControlEnd, ImmutableArray<CalloutBubble> Bubbles)
{
    public bool IsBubbles => !Bubbles.IsDefaultOrEmpty;

    /// <summary>Umriss des Dreiecks als Polygon (Kurven abgetastet); bei Kreisen leer.</summary>
    public ImmutableArray<PointD> Outline(int samples = 12)
    {
        if (IsBubbles)
        {
            return [];
        }

        var points = ImmutableArray.CreateBuilder<PointD>();
        points.Add(BaseStart);
        AddSide(points, BaseStart, ControlStart, Tip, samples);
        AddSide(points, Tip, ControlEnd, BaseEnd, samples);
        return points.ToImmutable();
    }

    private static void AddSide(ImmutableArray<PointD>.Builder points, PointD from, PointD? control, PointD to, int samples)
    {
        if (control is not { } c)
        {
            points.Add(to);
            return;
        }

        for (var i = 1; i <= samples; i++)
        {
            points.Add(CalloutGeometry.Quadratic(from, c, to, (double)i / samples));
        }
    }
}

/// <summary>Geometrie der Sprechblase (reine Mathematik, von Rendering und Trefferprüfung gemeinsam genutzt).</summary>
public static class CalloutGeometry
{
    /// <summary>Abstand, um den der Ansatz der Spitze in den Körper hineinragt (nahtlose Vereinigung).</summary>
    private const double BaseInset = 1;

    /// <summary>Anteil der Ellipsenachsen, den die Textfläche nutzt (einbeschriebenes Rechteck wäre 1/√2 ≈ 0,71).</summary>
    private const double EllipseTextFactor = 0.8;

    /// <summary>Stärke der Biegung einer gebogenen Spitze relativ zu ihrer Länge.</summary>
    private const double BendFactor = 0.3;

    public static PointD Quadratic(PointD a, PointD control, PointD b, double t)
    {
        var u = 1 - t;
        return new PointD(
            (u * u * a.X) + (2 * u * t * control.X) + (t * t * b.X),
            (u * u * a.Y) + (2 * u * t * control.Y) + (t * t * b.Y));
    }

    /// <summary>Wirksamer Eckenradius (höchstens die halbe kürzere Seite).</summary>
    public static double EffectiveCornerRadius(RectD rect, double cornerRadius)
    {
        var r = rect.Normalize();
        return Math.Clamp(cornerRadius, 0, Math.Min(r.Width, r.Height) / 2);
    }

    /// <summary>Höhe der Wolkenbögen (Gedankenblase) für ein Rechteck.</summary>
    public static double CloudBumpSize(RectD rect)
    {
        var r = rect.Normalize();
        return Math.Clamp(Math.Min(r.Width, r.Height) * 0.16, 2, 80);
    }

    /// <summary>
    /// Stützpunkte der Wolke: Punkte auf einer inneren Ellipse, zwischen denen nach außen gewölbte Bögen mit
    /// <paramref name="arcRadius"/> × Sehnenlänge liegen. Die Bögen bleiben innerhalb von <paramref name="rect"/>.
    /// </summary>
    public static ImmutableArray<PointD> CloudPoints(RectD rect, out double arcRadius)
    {
        arcRadius = 0.55;
        var r = rect.Normalize();
        var bump = CloudBumpSize(r);
        var (ia, ib) = CloudInnerAxes(r, bump);
        var c = r.Center;
        var perimeter = Math.PI * ((3 * (ia + ib)) - Math.Sqrt(((3 * ia) + ib) * (ia + (3 * ib))));
        var count = Math.Clamp((int)Math.Round(perimeter / (bump * 1.9)), 7, 48);
        var points = ImmutableArray.CreateBuilder<PointD>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = (2 * Math.PI * i / count) - (Math.PI / 2);
            points.Add(new PointD(c.X + (ia * Math.Cos(angle)), c.Y + (ib * Math.Sin(angle))));
        }

        return points.MoveToImmutable();
    }

    /// <summary>Liegt der Punkt im Körper der Blase (ohne Spitze)?</summary>
    public static bool BodyContains(CalloutShape shape, RectD rect, double cornerRadius, PointD point, double tolerance = 0)
    {
        var r = rect.Normalize().Inflate(tolerance, tolerance);
        if (shape != CalloutShape.Rounded)
        {
            return HitTesting.EllipseNorm(point, r) <= 1;
        }

        if (point.X < r.X || point.X > r.Right || point.Y < r.Y || point.Y > r.Bottom)
        {
            return false;
        }

        var radius = EffectiveCornerRadius(r, cornerRadius + tolerance);
        if (radius <= 0)
        {
            return true;
        }

        // Ecken: Abstand zum Mittelpunkt des Eckbogens.
        var cx = Math.Clamp(point.X, r.X + radius, r.Right - radius);
        var cy = Math.Clamp(point.Y, r.Y + radius, r.Bottom - radius);
        return new PointD(cx, cy).DistanceTo(point) <= radius;
    }

    /// <summary>
    /// Fläche für den Text: Körper abzüglich Innenabstand (der Rahmen liegt außen); bei Ellipse und Wolke ein Rechteck
    /// in der (inneren) Ellipse, etwas größer als das einbeschriebene (Text füllt die Ecken selten ganz aus).
    /// </summary>
    public static RectD TextArea(CalloutShape shape, RectD rect, double padding)
    {
        var r = rect.Normalize();
        var inset = Math.Max(0, padding);
        var c = r.Center;
        double width;
        double height;
        switch (shape)
        {
            case CalloutShape.Ellipse:
                width = r.Width * EllipseTextFactor;
                height = r.Height * EllipseTextFactor;
                break;
            case CalloutShape.Cloud:
                var (ia, ib) = CloudInnerAxes(r, CloudBumpSize(r));
                width = 2 * ia * EllipseTextFactor;
                height = 2 * ib * EllipseTextFactor;
                break;
            default:
                width = r.Width;
                height = r.Height;
                break;
        }

        width = Math.Max(1, width - (2 * inset));
        height = Math.Max(1, height - (2 * inset));
        return new RectD(c.X - (width / 2), c.Y - (height / 2), width, height);
    }

    /// <summary>
    /// Spitze zum Punkt <paramref name="tip"/>; <c>null</c>, wenn der Punkt im Körper liegt. Der Ansatz sitzt dort, wo die
    /// Linie von der Mitte zur Spitze den Körper verlässt – beim Rechteck auf der getroffenen Seite (ohne die Rundung),
    /// sodass die Spitze beim Ziehen um eine Ecke auf die Nachbarseite wandert. <paramref name="reversed"/> biegt eine
    /// gebogene Spitze in die andere Richtung.
    /// </summary>
    public static CalloutTail? ComputeTail(CalloutShape shape, RectD rect, double cornerRadius, PointD tip, double width, bool curved, bool reversed = false)
    {
        var r = rect.Normalize();
        if (r.Width <= 0 || r.Height <= 0 || BodyContains(shape, r, cornerRadius, tip))
        {
            return null;
        }

        width = Math.Max(1, width);
        var bend = curved ? (reversed ? -BendFactor : BendFactor) : 0;
        return shape switch
        {
            CalloutShape.Cloud => CloudTail(r, tip, width, bend),
            CalloutShape.Ellipse => EllipseTail(r, tip, width, bend),
            _ => RoundedTail(r, cornerRadius, tip, width, bend),
        };
    }

    private static (double A, double B) CloudInnerAxes(RectD r, double bump)
    {
        var a = r.Width / 2;
        var b = r.Height / 2;
        return (Math.Max(a - (bump * 0.62), a * 0.4), Math.Max(b - (bump * 0.62), b * 0.4));
    }

    private static CalloutTail RoundedTail(RectD r, double cornerRadius, PointD tip, double width, double bend)
    {
        var c = r.Center;
        var dx = tip.X - c.X;
        var dy = tip.Y - c.Y;
        var hw = r.Width / 2;
        var hh = r.Height / 2;
        var radius = EffectiveCornerRadius(r, cornerRadius);
        var horizontalEdge = Math.Abs(dy) * hw >= Math.Abs(dx) * hh;

        // Längs der getroffenen Seite: Ausgangspunkt, nutzbarer gerader Abschnitt.
        double along;
        double low;
        double high;
        if (horizontalEdge)
        {
            along = c.X + (dx * hh / Math.Max(Math.Abs(dy), 1e-9));
            low = r.X + radius;
            high = r.Right - radius;
        }
        else
        {
            along = c.Y + (dy * hw / Math.Max(Math.Abs(dx), 1e-9));
            low = r.Y + radius;
            high = r.Bottom - radius;
        }

        var inset = BaseInset;
        var w = Math.Min(width, high - low);
        if (w < Math.Min(width, 4))
        {
            // Seite fast vollständig gerundet (Pille/Kreis): Ansatz mittig, entsprechend tiefer im Körper.
            var length = horizontalEdge ? r.Width : r.Height;
            var mid = horizontalEdge ? c.X : c.Y;
            w = Math.Min(width, length * 0.6);
            low = mid - (w / 2);
            high = mid + (w / 2);
            inset = Math.Max(BaseInset, radius - Math.Sqrt(Math.Max(0, (radius * radius) - (w * w / 4))) + BaseInset);
        }

        var center = Math.Clamp(along, low + (w / 2), high - (w / 2));
        PointD a;
        PointD b;
        if (horizontalEdge)
        {
            var y = dy > 0 ? r.Bottom - inset : r.Y + inset;
            a = new PointD(center - (w / 2), y);
            b = new PointD(center + (w / 2), y);
        }
        else
        {
            var x = dx > 0 ? r.Right - inset : r.X + inset;
            a = new PointD(x, center - (w / 2));
            b = new PointD(x, center + (w / 2));
        }

        return Triangle(a, b, tip, bend);
    }

    private static CalloutTail EllipseTail(RectD r, PointD tip, double width, double bend)
    {
        var c = r.Center;
        var ea = r.Width / 2;
        var eb = r.Height / 2;
        var theta = Math.Atan2((tip.Y - c.Y) / eb, (tip.X - c.X) / ea);

        // Halbe Ansatzbreite als Winkel über die Bogenlänge an dieser Stelle.
        var speed = Math.Sqrt((ea * ea * Math.Sin(theta) * Math.Sin(theta)) + (eb * eb * Math.Cos(theta) * Math.Cos(theta)));
        var delta = Math.Min(width / 2 / Math.Max(speed, 1e-9), Math.PI / 3);
        PointD OnEllipse(double angle)
        {
            var p = new PointD(c.X + (ea * Math.Cos(angle)), c.Y + (eb * Math.Sin(angle)));
            var toCenter = c - p;
            var length = Math.Max(toCenter.Length, 1e-9);
            return new PointD(p.X + (toCenter.X / length * BaseInset), p.Y + (toCenter.Y / length * BaseInset));
        }

        return Triangle(OnEllipse(theta - delta), OnEllipse(theta + delta), tip, bend);
    }

    /// <param name="bend">Biegung relativ zur Länge (0 = gerade, negativ = andere Richtung).</param>
    private static CalloutTail Triangle(PointD a, PointD b, PointD tip, double bend)
    {
        if (bend == 0)
        {
            return new CalloutTail(a, b, tip, null, null, []);
        }

        var baseMid = new PointD((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var direction = tip - baseMid;
        var offset = new VectorD(-direction.Y * bend, direction.X * bend);
        PointD Control(PointD from) => new PointD((from.X + tip.X) / 2, (from.Y + tip.Y) / 2) + offset;
        return new CalloutTail(a, b, tip, Control(a), Control(b), []);
    }

    /// <summary>Gedankenblase: drei kleiner werdende Kreise vom Wolkenrand zur Spitze (bei kurzem Abstand weniger).</summary>
    private static CalloutTail CloudTail(RectD r, PointD tip, double width, double bend)
    {
        var c = r.Center;
        var ea = r.Width / 2;
        var eb = r.Height / 2;
        var dx = tip.X - c.X;
        var dy = tip.Y - c.Y;
        var scale = 1 / Math.Sqrt(((dx / ea) * (dx / ea)) + ((dy / eb) * (dy / eb)));
        var start = new PointD(c.X + (dx * scale), c.Y + (dy * scale));
        var direction = tip - start;
        var length = direction.Length;
        var unit = length > 1e-9 ? new VectorD(direction.X / length, direction.Y / length) : new VectorD(0, 1);
        var control = new PointD((start.X + tip.X) / 2, (start.Y + tip.Y) / 2) + new VectorD(-unit.Y * length * bend, unit.X * length * bend);

        var r1 = width / 2;
        var r3 = Math.Max(width * 0.18, 1);
        var r2 = (r1 + r3) / 2;
        var bubbles = ImmutableArray.CreateBuilder<CalloutBubble>();
        if (length <= (2 * r3) + 1)
        {
            bubbles.Add(new CalloutBubble(Quadratic(start, control, tip, 0.5), r3));
        }
        else
        {
            var t1 = Math.Min(r1 * 1.15 / length, 0.5);
            var t3 = 1 - (r3 / length);
            var p1 = Quadratic(start, control, tip, t1);
            var p3 = Quadratic(start, control, tip, t3);
            if (p1.DistanceTo(p3) >= r1 + r3)
            {
                bubbles.Add(new CalloutBubble(p1, Math.Min(r1, length * 0.45)));
            }

            var p2 = Quadratic(start, control, tip, (t1 + t3) / 2);
            if (p1.DistanceTo(p3) >= r1 + (2 * r2) + r3)
            {
                bubbles.Add(new CalloutBubble(p2, r2));
            }

            bubbles.Add(new CalloutBubble(p3, r3));
        }

        return new CalloutTail(start, start, tip, null, null, bubbles.ToImmutable());
    }
}
