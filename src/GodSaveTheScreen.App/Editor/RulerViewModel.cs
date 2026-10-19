using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Editor;

/// <summary>
/// UI-Zustand des virtuellen Lineals je Editorfenster. Gehört nicht zum Dokument und wird nie exportiert;
/// bleibt beim Werkzeugwechsel erhalten.
/// </summary>
public sealed partial class RulerViewModel : ObservableObject
{
    /// <summary>Bandbreite in DIPs (zoom-unabhängig).</summary>
    public const double BandWidthDip = 80;

    /// <summary>Abstand in DIPs, innerhalb dessen ein Strich an der Lineal-Kante einrastet.</summary>
    public const double SnapDistanceDip = 24;

    [ObservableProperty]
    private PointD _center;

    /// <summary>Drehwinkel in Grad (0 = waagerecht, im Uhrzeigersinn positiv, weil die y-Achse nach unten zeigt).</summary>
    [ObservableProperty]
    private double _angle;

    /// <summary>Länge in Bildpixeln.</summary>
    [ObservableProperty]
    private double _length = 600;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>Lage in Bildpixeln für den aktuellen Maßstab (DIPs je Bildpixel).</summary>
    public RulerPlacement Placement(double scale) => new(Center, Angle, Length, BandWidthDip / Math.Max(scale, 0.01));

    /// <summary>Dreht um <paramref name="degrees"/> und normalisiert auf (−180, 180].</summary>
    public void Rotate(double degrees) => Angle = Normalize(Angle + degrees);

    public static double Normalize(double degrees)
    {
        var value = degrees % 360;
        if (value > 180)
        {
            value -= 360;
        }
        else if (value <= -180)
        {
            value += 360;
        }

        return Math.Round(value, 4);
    }
}
