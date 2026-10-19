using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;

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

    /// <summary>
    /// Kodiert im gewählten Format. Formate ohne Alpha (JPEG, BMP, GIF) verrechnen Transparenz auf Weiß.
    /// </summary>
    public static byte[] Encode(this PixelBuffer buffer, ImageFormatSetting format, double dpiX = 96, double dpiY = 96, int jpegQuality = 92) =>
        new ImageCodecService().Encode(buffer, dpiX, dpiY, new ImageSaveOptions(format, jpegQuality));

    public static string FileExtension(this ImageFormatSetting format) => ImageFormats.Extensions(format)[0];

    /// <summary>Kopie, bei der Transparenz über der Hintergrundfarbe zu undurchsichtigen Pixeln verrechnet ist.</summary>
    public static PixelBuffer FlattenOnBackground(this PixelBuffer buffer, byte red, byte green, byte blue)
    {
        var result = buffer.Clone();
        var data = result.Pixels;
        for (var i = 0; i < result.Width * result.Height * PixelBuffer.BytesPerPixel; i += PixelBuffer.BytesPerPixel)
        {
            var alpha = data[i + 3];
            if (alpha == 255)
            {
                continue;
            }

            data[i] = (byte)(((data[i] * alpha) + (blue * (255 - alpha))) / 255);
            data[i + 1] = (byte)(((data[i + 1] * alpha) + (green * (255 - alpha))) / 255);
            data[i + 2] = (byte)(((data[i + 2] * alpha) + (red * (255 - alpha))) / 255);
            data[i + 3] = 255;
        }

        return result;
    }
}
