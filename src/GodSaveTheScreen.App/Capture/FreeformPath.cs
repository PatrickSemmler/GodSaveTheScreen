using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Gesammelte Lasso-Punkte in virtuellen Pixelkoordinaten (Mindestabstand, an den Desktop geklemmt).</summary>
public sealed class FreeformPath
{
    public const double MinimumPointDistance = 2;
    public const int MinimumPoints = 3;

    private readonly List<PointD> _points = [];
    private readonly PixelRect _desktop;

    public FreeformPath(PixelRect desktop) => _desktop = desktop;

    public IReadOnlyList<PointD> Points => _points;

    public bool HasPath => _points.Count > 0;

    /// <summary>Umschließendes Rechteck der Punkte.</summary>
    public PixelRect Bounds => PolygonMask.BoundingBox(_points).Intersect(_desktop);

    /// <summary>Pfad ist groß genug, um ausgeschnitten zu werden.</summary>
    public bool IsValid => _points.Count >= MinimumPoints && Bounds is { Width: > 1, Height: > 1 };

    public void Begin(PointD point)
    {
        _points.Clear();
        _points.Add(Clamp(point));
    }

    /// <summary>Fügt einen Punkt hinzu, wenn er mindestens <see cref="MinimumPointDistance"/> vom letzten entfernt ist.</summary>
    public bool Add(PointD point)
    {
        if (_points.Count == 0)
        {
            return false;
        }

        var p = Clamp(point);
        var last = _points[^1];
        if (Math.Abs(p.X - last.X) < MinimumPointDistance && Math.Abs(p.Y - last.Y) < MinimumPointDistance)
        {
            return false;
        }

        _points.Add(p);
        return true;
    }

    public void Clear() => _points.Clear();

    private PointD Clamp(PointD p) => new(
        Math.Clamp(Math.Floor(p.X), _desktop.X, _desktop.Right - 1),
        Math.Clamp(Math.Floor(p.Y), _desktop.Y, _desktop.Bottom - 1));
}
