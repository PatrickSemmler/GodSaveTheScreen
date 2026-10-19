using System.Text.Json.Serialization;

namespace GodSaveTheScreen.Core.Geometry;

public readonly record struct VectorD(double X, double Y)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y));
}

[JsonConverter(typeof(PointDJsonConverter))]
public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD p, VectorD v) => new(p.X + v.X, p.Y + v.Y);

    public static PointD operator -(PointD p, VectorD v) => new(p.X - v.X, p.Y - v.Y);

    public static VectorD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);

    public double DistanceTo(PointD other) => (this - other).Length;
}

public readonly record struct SizeD(double Width, double Height)
{
    public static SizeD Empty => new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

[JsonConverter(typeof(RectDJsonConverter))]
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public static RectD Empty => new(0, 0, 0, 0);

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static RectD FromPoints(PointD a, PointD b) => new RectD(a.X, a.Y, b.X - a.X, b.Y - a.Y).Normalize();

    /// <summary>Liefert ein Rechteck mit nicht-negativer Breite/Höhe.</summary>
    public RectD Normalize()
    {
        var x = Width < 0 ? X + Width : X;
        var y = Height < 0 ? Y + Height : Y;
        return new RectD(x, y, Math.Abs(Width), Math.Abs(Height));
    }

    public RectD Intersect(RectD other)
    {
        var a = Normalize();
        var b = other.Normalize();
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        return right <= left || bottom <= top ? Empty : new RectD(left, top, right - left, bottom - top);
    }

    /// <summary>Punkt liegt im Rechteck (links/oben inklusive, rechts/unten exklusive).</summary>
    public bool Contains(PointD p)
    {
        var r = Normalize();
        return p.X >= r.X && p.X < r.Right && p.Y >= r.Y && p.Y < r.Bottom;
    }

    public RectD Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + (2 * dx), Height + (2 * dy));

    public PointD Center => new(X + (Width / 2), Y + (Height / 2));

    public PointD TopLeft => new(X, Y);

    public PointD BottomRight => new(Right, Bottom);

    public RectD Union(RectD other)
    {
        var a = Normalize();
        var b = other.Normalize();
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new RectD(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }

    /// <summary>Das andere Rechteck liegt vollständig in diesem (Ränder inklusive).</summary>
    public bool ContainsRect(RectD other)
    {
        var a = Normalize();
        var b = other.Normalize();
        return b.X >= a.X && b.Y >= a.Y && b.Right <= a.Right && b.Bottom <= a.Bottom;
    }

    public bool IntersectsWith(RectD other)
    {
        var a = Normalize();
        var b = other.Normalize();
        return a.X <= b.Right && b.X <= a.Right && a.Y <= b.Bottom && b.Y <= a.Bottom;
    }

    public static RectD BoundingBox(IEnumerable<PointD> points)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        var any = false;
        foreach (var p in points)
        {
            any = true;
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        return any ? new RectD(minX, minY, maxX - minX, maxY - minY) : Empty;
    }
}

[JsonConverter(typeof(PixelRectJsonConverter))]
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Empty => new(0, 0, 0, 0);

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromLtrb(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top ? Empty : FromLtrb(left, top, right, bottom);
    }

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public PixelRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public RectD ToRectD() => new(X, Y, Width, Height);
}
