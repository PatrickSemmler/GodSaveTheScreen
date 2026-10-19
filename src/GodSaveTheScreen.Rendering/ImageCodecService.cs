using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Rendering;

/// <summary>Laden über WIC (alle installierten Decoder), Speichern über die WPF-Encoder.</summary>
public sealed class ImageCodecService : IImageCodecService
{
    public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Load(path));
    }

    public static LoadedImage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];
        var dpiX = frame.DpiX > 0 ? frame.DpiX : 96;
        var dpiY = frame.DpiY > 0 ? frame.DpiY : 96;

        var transform = ExifOrientation(frame) switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => Combine(new RotateTransform(90), new ScaleTransform(-1, 1)),
            6 => new RotateTransform(90),
            7 => Combine(new RotateTransform(270), new ScaleTransform(-1, 1)),
            8 => new RotateTransform(270),
            _ => (Transform?)null,
        };
        if (transform is not null)
        {
            var transformed = new TransformedBitmap(frame, transform);
            transformed.Freeze();
            if (transformed.PixelWidth != frame.PixelWidth)
            {
                (dpiX, dpiY) = (dpiY, dpiX);
            }

            frame = transformed;
        }

        return new LoadedImage(PixelBufferExtensions.FromBitmapSource(frame), dpiX, dpiY, decoder.Frames.Count);
    }

    public async Task SaveAsync(
        PixelBuffer image, double dpiX, double dpiY, string path, ImageSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var data = Encode(image, dpiX, dpiY, options);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, data, cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    public byte[] Encode(PixelBuffer image, double dpiX, double dpiY, ImageSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);

        var source = image;
        if (!ImageFormats.SupportsAlpha(options.Format))
        {
            var bg = options.EffectiveBackground;
            source = image.FlattenOnBackground(bg.R, bg.G, bg.B);
        }

        BitmapEncoder encoder = options.Format switch
        {
            ImageFormatSetting.Jpeg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(options.Quality, 1, 100) },
            ImageFormatSetting.Bmp => new BmpBitmapEncoder(),
            ImageFormatSetting.Gif => new GifBitmapEncoder(),
            ImageFormatSetting.Tiff => new TiffBitmapEncoder { Compression = ToTiff(options.TiffCompression) },
            ImageFormatSetting.JpegXr => new WmpBitmapEncoder
            {
                Lossless = options.JxrLossless,
                ImageQualityLevel = Math.Clamp(options.Quality, 1, 100) / 100f,
            },
            _ => new PngBitmapEncoder { Interlace = options.PngInterlace ? PngInterlaceOption.On : PngInterlaceOption.Off },
        };

        encoder.Frames.Add(BitmapFrame.Create(source.ToBitmapSource(dpiX, dpiY)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static TiffCompressOption ToTiff(TiffCompressionSetting setting) => setting switch
    {
        TiffCompressionSetting.None => TiffCompressOption.None,
        TiffCompressionSetting.Zip => TiffCompressOption.Zip,
        TiffCompressionSetting.Rle => TiffCompressOption.Rle,
        _ => TiffCompressOption.Lzw,
    };

    private static TransformGroup Combine(Transform first, Transform second)
    {
        var group = new TransformGroup();
        group.Children.Add(first);
        group.Children.Add(second);
        return group;
    }

    private static int ExifOrientation(BitmapSource frame)
    {
        try
        {
            if (frame is BitmapFrame { Metadata: BitmapMetadata metadata }
                && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
            {
                return orientation;
            }
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // Format ohne Metadaten.
        }

        return 1;
    }
}
