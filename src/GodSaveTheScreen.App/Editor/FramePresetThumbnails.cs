using System.Windows.Media;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Editor;

/// <summary>
/// Vorschaubilder der Rahmenvorlagen: ein kleines Musterfenster, mit dem Rahmen über denselben <see cref="FrameRenderer"/>
/// gerendert wie beim Export. Jede Vorschau entsteht einmal pro Sitzung (Cache nach Rahmenwerten); nur im UI-Thread aufrufen.
/// </summary>
public static class FramePresetThumbnails
{
    /// <summary>Größe des Musterbilds in Pixeln.</summary>
    public const int SampleWidth = 240;

    public const int SampleHeight = 150;

    private static readonly Dictionary<FrameStyle, ImageSource?> Cache = [];

    private static PixelBuffer? _sample;

    /// <summary>Vorschau der Vorlage oder <c>null</c>, wenn sie sich nicht rendern lässt.</summary>
    public static ImageSource? Get(FrameStyle frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var key = frame.WithoutImage();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ImageSource? thumbnail = null;
        try
        {
            var sample = Sample();
            var result = FrameRenderer.Compose(sample, sample.Bounds, key.Clamped());
            var source = result.ToBitmapSource();
            source.Freeze();
            thumbnail = source;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OutOfMemoryException or System.Runtime.InteropServices.COMException)
        {
            thumbnail = null;
        }

        Cache[key] = thumbnail;
        return thumbnail;
    }

    /// <summary>Musterfenster: heller Grund, blaue Titelleiste und einige graue Zeilen.</summary>
    public static PixelBuffer Sample()
    {
        if (_sample is not null)
        {
            return _sample;
        }

        var buffer = new PixelBuffer(SampleWidth, SampleHeight);
        Fill(buffer, 0, 0, SampleWidth, SampleHeight, 0xF7, 0xF8, 0xFA);
        Fill(buffer, 0, 0, SampleWidth, 22, 0x3B, 0x82, 0xF6);
        Fill(buffer, 12, 6, 40, 10, 0xFF, 0xFF, 0xFF);
        for (var line = 0; line < 5; line++)
        {
            Fill(buffer, 16, 40 + (line * 20), line % 2 == 0 ? 170 : 120, 8, 0xC4, 0xCA, 0xD4);
        }

        Fill(buffer, 16, 128, 56, 14, 0x3B, 0x82, 0xF6);
        _sample = buffer;
        return buffer;
    }

    private static void Fill(PixelBuffer buffer, int x, int y, int width, int height, byte r, byte g, byte b)
    {
        for (var row = y; row < Math.Min(buffer.Height, y + height); row++)
        {
            for (var column = x; column < Math.Min(buffer.Width, x + width); column++)
            {
                var offset = (row * buffer.Stride) + (column * PixelBuffer.BytesPerPixel);
                buffer.Pixels[offset] = b;
                buffer.Pixels[offset + 1] = g;
                buffer.Pixels[offset + 2] = r;
                buffer.Pixels[offset + 3] = 0xFF;
            }
        }
    }
}
