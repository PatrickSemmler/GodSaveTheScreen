using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Capture;

/// <summary>Eingefrorener virtueller Desktop. Koordinaten der Monitore liegen in virtuellen Pixeln.</summary>
public sealed class ScreenSnapshot
{
    public ScreenSnapshot(PixelBuffer image, int virtualLeft, int virtualTop, IReadOnlyList<MonitorInfo> monitors)
    {
        Image = image;
        VirtualLeft = virtualLeft;
        VirtualTop = virtualTop;
        Monitors = monitors;
    }

    public PixelBuffer Image { get; }

    public int VirtualLeft { get; }

    public int VirtualTop { get; }

    public IReadOnlyList<MonitorInfo> Monitors { get; }

    public PixelRect VirtualBounds => new(VirtualLeft, VirtualTop, Image.Width, Image.Height);

    /// <summary>Schneidet einen Bereich in virtuellen Desktop-Koordinaten aus.</summary>
    public PixelBuffer Crop(PixelRect inVirtualCoords) =>
        Image.Crop(inVirtualCoords.Offset(-VirtualLeft, -VirtualTop));
}
