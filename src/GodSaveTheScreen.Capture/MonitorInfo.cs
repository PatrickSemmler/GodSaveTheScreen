using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Capture;

/// <summary>Monitor in physischen Pixeln des virtuellen Desktops.</summary>
public sealed record MonitorInfo(
    nint Handle,
    PixelRect Bounds,
    PixelRect WorkArea,
    uint DpiX,
    uint DpiY,
    bool IsPrimary,
    string DeviceName)
{
    /// <summary>Skalierungsfaktor gegenüber 96 DPI (z. B. 1.5 bei 150 %).</summary>
    public double Scale => DpiX / 96.0;
}
