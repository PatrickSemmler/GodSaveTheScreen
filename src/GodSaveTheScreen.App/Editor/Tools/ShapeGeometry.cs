using System.Windows.Input;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Einschränkungen beim Aufziehen: <c>Umschalt</c> = Quadrat/15°-Raster, <c>Alt</c> = vom Mittelpunkt.</summary>
public static class ShapeGeometry
{
    public const double AngleStepDegrees = 15;

    public static RectD ConstrainBox(PointD start, PointD current, ModifierKeys modifiers)
    {
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
            dx = (dx < 0 ? -1 : 1) * size;
            dy = (dy < 0 ? -1 : 1) * size;
        }

        return (modifiers & ModifierKeys.Alt) != 0
            ? RectD.FromPoints(new PointD(start.X - dx, start.Y - dy), new PointD(start.X + dx, start.Y + dy))
            : RectD.FromPoints(start, new PointD(start.X + dx, start.Y + dy));
    }

    /// <summary>Endpunkte einer Linie; <c>Umschalt</c> rastet den Winkel auf 15° ein, <c>Alt</c> zeichnet vom Mittelpunkt.</summary>
    public static (PointD Start, PointD End) ConstrainLine(PointD start, PointD current, ModifierKeys modifiers)
    {
        var end = (modifiers & ModifierKeys.Shift) != 0 ? SnapAngle(start, current) : current;
        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            return (new PointD((2 * start.X) - end.X, (2 * start.Y) - end.Y), end);
        }

        return (start, end);
    }

    public static PointD SnapAngle(PointD origin, PointD point)
    {
        var dx = point.X - origin.X;
        var dy = point.Y - origin.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-9)
        {
            return point;
        }

        var step = AngleStepDegrees * Math.PI / 180;
        var angle = Math.Round(Math.Atan2(dy, dx) / step) * step;
        return new PointD(origin.X + (Math.Cos(angle) * length), origin.Y + (Math.Sin(angle) * length));
    }
}
