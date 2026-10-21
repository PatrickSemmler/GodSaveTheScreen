using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Objekt mit Drehung um seine Mitte. Der unverdrehte Rahmen ist <see cref="Rect"/>.</summary>
public interface IRotatableAnnotation
{
    /// <summary>Drehung im Uhrzeigersinn in Grad, normalisiert auf [0, 360).</summary>
    double Rotation { get; set; }

    /// <summary>Drehpunkt (Mitte des unverdrehten Rahmens).</summary>
    PointD Center { get; }

    /// <summary>Unverdrehter Rahmen.</summary>
    RectD Rect { get; }
}

/// <summary>Drehung von Punkten und Rechtecken (Bildkoordinaten, y nach unten, Winkel im Uhrzeigersinn).</summary>
public static class RotationMath
{
    /// <summary>Normalisiert einen Winkel auf [0, 360).</summary>
    public static double Normalize(double degrees)
    {
        if (double.IsNaN(degrees) || double.IsInfinity(degrees))
        {
            return 0;
        }

        var result = degrees % 360;
        if (result < 0)
        {
            result += 360;
        }

        return result >= 360 ? 0 : result;
    }

    public static PointD Rotate(PointD point, PointD center, double degrees)
    {
        if (degrees == 0)
        {
            return point;
        }

        var radians = degrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return new PointD(center.X + (dx * cos) - (dy * sin), center.Y + (dx * sin) + (dy * cos));
    }

    public static PointD Unrotate(PointD point, PointD center, double degrees) => Rotate(point, center, -degrees);

    /// <summary>Die vier Ecken des gedrehten Rechtecks (links oben, rechts oben, rechts unten, links unten).</summary>
    public static PointD[] Corners(RectD rect, double degrees)
    {
        var r = rect.Normalize();
        var center = r.Center;
        return
        [
            Rotate(new PointD(r.X, r.Y), center, degrees),
            Rotate(new PointD(r.Right, r.Y), center, degrees),
            Rotate(new PointD(r.Right, r.Bottom), center, degrees),
            Rotate(new PointD(r.X, r.Bottom), center, degrees),
        ];
    }

    /// <summary>Achsparallele Hülle des gedrehten Rechtecks.</summary>
    public static RectD Hull(RectD rect, double degrees) => RectD.BoundingBox(Corners(rect, degrees));

    /// <summary>Faktor, um den ein gleichmäßiger Rand in der achsparallelen Hülle wächst (|cos| + |sin|, 1 ohne Drehung).</summary>
    public static double HullFactor(double degrees)
    {
        if (degrees == 0)
        {
            return 1;
        }

        var radians = degrees * Math.PI / 180;
        return Math.Abs(Math.Cos(radians)) + Math.Abs(Math.Sin(radians));
    }

    /// <summary>Punkt im unverdrehten Koordinatensystem des Objekts → Bildkoordinaten.</summary>
    public static PointD ToWorld(this IRotatableAnnotation annotation, PointD local)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return Rotate(local, annotation.Center, annotation.Rotation);
    }

    /// <summary>Bildkoordinaten → unverdrehtes Koordinatensystem des Objekts.</summary>
    public static PointD ToLocal(this IRotatableAnnotation annotation, PointD world)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return Unrotate(world, annotation.Center, annotation.Rotation);
    }

    /// <summary>
    /// Führt eine Änderung der unverdrehten Geometrie aus (Text wächst, Eckpunkt verschoben, Rahmen skaliert) und
    /// verschiebt das Objekt danach so, dass alles, was sich im eigenen System nicht bewegt hat, auch im Bild stehen bleibt.
    /// Ohne Drehung wird die Änderung unverändert ausgeführt.
    /// </summary>
    public static void PreserveFrame(Annotation annotation, Action change)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(change);
        if (annotation is not IRotatableAnnotation { Rotation: not 0 } rotatable)
        {
            change();
            return;
        }

        var center = rotatable.Center;
        change();
        Realign(annotation, center);
    }

    /// <summary>
    /// Gleicht eine Verschiebung des Drehpunkts aus: Hat sich die Mitte im unverdrehten System von
    /// <paramref name="previousCenter"/> wegbewegt, wird das Objekt so verschoben, dass seine unverdrehte Geometrie
    /// weiterhin mit der alten Mitte gedreht im Bild liegt.
    /// </summary>
    public static void Realign(Annotation annotation, PointD previousCenter)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation is not IRotatableAnnotation { Rotation: not 0 } rotatable)
        {
            return;
        }

        var center = rotatable.Center;
        var moved = new VectorD(center.X - previousCenter.X, center.Y - previousCenter.Y);
        if (moved.X == 0 && moved.Y == 0)
        {
            return;
        }

        var turned = Rotate(new PointD(moved.X, moved.Y), new PointD(0, 0), rotatable.Rotation);
        annotation.Translate(new VectorD(turned.X - moved.X, turned.Y - moved.Y));
    }
}

/// <summary>
/// Farbiges Emoji (Segoe UI Emoji). <see cref="Rect"/> ist der Rahmen vor der Drehung, die Drehung erfolgt um dessen Mitte;
/// <see cref="Annotation.Bounds"/> ist die achsparallele Hülle des gedrehten Rahmens.
/// </summary>
public sealed partial class EmojiAnnotation : Annotation, IRotatableAnnotation, IShadowAnnotation, IFlippableAnnotation
{
    public const double MinSize = 8;

    [ObservableProperty]
    private string _sequence = "\U0001F600";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Center))]
    private RectD _rect;

    [ObservableProperty]
    private bool _flipHorizontal;

    [ObservableProperty]
    private bool _flipVertical;

    [ObservableProperty]
    private ShadowStyle _shadow = ShadowStyle.None;

    private double _rotation;

    /// <inheritdoc />
    public double Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, RotationMath.Normalize(value)))
            {
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    [JsonIgnore]
    public PointD Center => Rect.Normalize().Center;

    [JsonIgnore]
    public override RectD Bounds => RotationMath.Hull(Rect, Rotation);

    public override void MapPoints(Func<PointD, PointD> map) =>
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));

    public override bool HitTest(PointD point, double tolerance)
    {
        var local = RotationMath.Unrotate(point, Center, Rotation);
        return Rect.Normalize().Inflate(tolerance, tolerance).Contains(local);
    }
}
