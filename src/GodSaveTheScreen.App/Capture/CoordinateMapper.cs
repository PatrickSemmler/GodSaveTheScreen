using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Einzige Stelle für Koordinatenumrechnungen der Aufnahme:
/// virtueller Desktop (physische Pixel, ggf. negativ) ↔ Snapshot-Pixel ↔ Overlay-lokale DIPs eines Monitors.
/// </summary>
public readonly record struct CoordinateMapper(PixelRect MonitorBounds, double Scale)
{
    public PointD VirtualToLocalDips(PointD virtualPoint) =>
        new((virtualPoint.X - MonitorBounds.X) / Scale, (virtualPoint.Y - MonitorBounds.Y) / Scale);

    public PointD LocalDipsToVirtual(PointD dips) =>
        new((dips.X * Scale) + MonitorBounds.X, (dips.Y * Scale) + MonitorBounds.Y);

    public RectD VirtualToLocalDips(RectD virtualRect)
    {
        var topLeft = VirtualToLocalDips(new PointD(virtualRect.X, virtualRect.Y));
        return new RectD(topLeft.X, topLeft.Y, virtualRect.Width / Scale, virtualRect.Height / Scale);
    }

    /// <summary>Größe des Monitors in lokalen DIPs.</summary>
    public SizeD LocalSizeDips => new(MonitorBounds.Width / Scale, MonitorBounds.Height / Scale);

    /// <summary>Virtuelle Koordinate → Pixelposition im Snapshot.</summary>
    public static PointD VirtualToSnapshot(PointD virtualPoint, int virtualLeft, int virtualTop) =>
        new(virtualPoint.X - virtualLeft, virtualPoint.Y - virtualTop);
}
