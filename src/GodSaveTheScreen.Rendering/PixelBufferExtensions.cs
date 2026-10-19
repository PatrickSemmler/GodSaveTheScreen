using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

public static class PixelBufferExtensions
{
    /// <summary>Erzeugt eine eingefrorene <see cref="BitmapSource"/> (Bgra32, nicht vormultipliziert).</summary>
    public static BitmapSource ToBitmapSource(this PixelBuffer buffer, double dpiX = 96, double dpiY = 96)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Width == 0 || buffer.Height == 0)
        {
            throw new ArgumentException("Ein leerer Puffer kann nicht in ein Bild umgewandelt werden.", nameof(buffer));
        }

        var bitmap = BitmapSource.Create(
            buffer.Width, buffer.Height, dpiX, dpiY, PixelFormats.Bgra32, null, buffer.Pixels, buffer.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static PixelBuffer FromBitmapSource(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        BitmapSource converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var buffer = new PixelBuffer(converted.PixelWidth, converted.PixelHeight);
        converted.CopyPixels(buffer.Pixels, buffer.Stride, 0);
        return buffer;
    }

    public static byte[] EncodePng(this PixelBuffer buffer, double dpiX = 96, double dpiY = 96)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(buffer.ToBitmapSource(dpiX, dpiY)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
