using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>Die Datei ist kein unterstütztes Bild (oder beschädigt).</summary>
public sealed class ImageImportException : Exception
{
    public ImageImportException()
    {
    }

    public ImageImportException(string message)
        : base(message)
    {
    }

    public ImageImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Ergebnis eines Imports: Bilddatei für das Objekt und Größe beim Einfügen (Bildpixel).</summary>
public sealed record ImportedImage(ImageAsset Asset, SizeD Size);

/// <summary>
/// Bereitet eine Bilddatei als Bildobjekt vor. PNG und JPEG (einbildig, ohne EXIF-Drehung) bleiben Original, SVG bleibt Text,
/// alle übrigen Formate (GIF, TIFF, WebP, HEIC …) werden für das gewählte Einzelbild als PNG gespeichert. Mehrbilddateien
/// (animiertes GIF, mehrseitiges TIFF) bieten <see cref="FrameCount"/> Einzelbilder an.
/// </summary>
public sealed class ImageImport
{
    private readonly byte[] _data;
    private readonly BitmapDecoder? _decoder;
    private readonly SvgPicture? _svg;
    private BitmapSource?[]? _composedGif;
    private RenderTargetBitmap? _gifCanvas;
    private int _gifComposed;

    private ImageImport(byte[] data, BitmapDecoder? decoder, SvgPicture? svg)
    {
        _data = data;
        _decoder = decoder;
        _svg = svg;
    }

    public bool IsSvg => _svg is not null;

    /// <summary>Anzahl wählbarer Einzelbilder (1 bei SVG und einfachen Bildern).</summary>
    public int FrameCount => _decoder?.Frames.Count ?? 1;

    /// <summary>Liest eine Datei.</summary>
    /// <exception cref="ImageImportException">Kein unterstütztes Bild.</exception>
    public static ImageImport FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] data;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > ImageAsset.MaxBytes)
            {
                throw new ImageImportException("Die Bilddatei ist zu groß.");
            }

            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImageImportException(ex.Message, ex);
        }

        return FromBytes(data, Path.GetExtension(path));
    }

    /// <summary>Liest Bytes einer Bilddatei; <paramref name="extension"/> (z. B. „.svg“) hilft nur bei der Erkennung von SVG.</summary>
    /// <exception cref="ImageImportException">Kein unterstütztes Bild.</exception>
    public static ImageImport FromBytes(byte[] data, string? extension = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length > ImageAsset.MaxBytes)
        {
            throw new ImageImportException("Leere oder zu große Bilddatei.");
        }

        if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) || SvgRasterizer.LooksLikeSvg(data))
        {
            var svg = SvgRasterizer.Load(data) ?? throw new ImageImportException("Das SVG ist beschädigt oder leer.");
            return new ImageImport(data, null, svg);
        }

        try
        {
            var decoder = BitmapDecoder.Create(new MemoryStream(data, writable: false), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                throw new ImageImportException("Die Datei enthält kein Bild.");
            }

            return new ImageImport(data, decoder, null);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or IOException or OverflowException)
        {
            throw new ImageImportException("Das Bildformat wird nicht unterstützt.", ex);
        }
    }

    /// <summary>Bild aus der Zwischenablage (immer als PNG gespeichert).</summary>
    public static ImportedImage FromBitmap(BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return new ImportedImage(new ImageAsset(ImageAssetFormat.Png, EncodePng(bitmap)), new SizeD(bitmap.PixelWidth, bitmap.PixelHeight));
    }

    /// <summary>Vorschau des Einzelbilds <paramref name="frame"/> (längere Seite höchstens <paramref name="maxSize"/>).</summary>
    public BitmapSource Thumbnail(int frame, int maxSize)
    {
        var source = _svg is not null ? RenderSvg(maxSize) : Frame(frame);
        var scale = Math.Min(1, maxSize / (double)Math.Max(source.PixelWidth, source.PixelHeight));
        if (scale >= 1)
        {
            return source;
        }

        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }

    /// <summary>Bilddatei und Einfügegröße für das Einzelbild <paramref name="frame"/>.</summary>
    public ImportedImage Create(int frame = 0)
    {
        if (_svg is not null)
        {
            var (width, height) = SvgRasterizer.IntrinsicSize(_data, _svg);
            return new ImportedImage(new ImageAsset(ImageAssetFormat.Svg, _data), new SizeD(width, height));
        }

        var bitmap = Frame(frame);
        var size = new SizeD(bitmap.PixelWidth, bitmap.PixelHeight);
        if (FrameCount == 1 && KeepsOriginal() is { } format)
        {
            return new ImportedImage(new ImageAsset(format, _data), size);
        }

        return new ImportedImage(new ImageAsset(ImageAssetFormat.Png, EncodePng(bitmap)), size);
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>PNG und JPEG ohne EXIF-Drehung bleiben, wie sie sind (jedes Windows dekodiert sie).</summary>
    private ImageAssetFormat? KeepsOriginal()
    {
        var container = _decoder!.CodecInfo?.ContainerFormat;
        if (container == ContainerPng)
        {
            return ImageAssetFormat.Png;
        }

        if (container == ContainerJpeg && ImageCodecService.ExifOrientation(_decoder.Frames[0]) == 1)
        {
            return ImageAssetFormat.Jpeg;
        }

        return null;
    }

    private static readonly Guid ContainerPng = new("1b7cfaf4-713f-473c-bbcd-6137425faeaf");
    private static readonly Guid ContainerJpeg = new("19e4a5aa-5662-4fc5-a0c0-1758028e1057");
    private static readonly Guid ContainerGif = new("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5");

    private BitmapSource RenderSvg(int maxSize)
    {
        var scale = maxSize / Math.Max(_svg!.Width, _svg.Height);
        return SvgRasterizer.Render(_svg, Math.Max(1, (int)Math.Round(_svg.Width * scale)), Math.Max(1, (int)Math.Round(_svg.Height * scale)));
    }

    /// <summary>Einzelbild, EXIF-gedreht; animierte GIFs werden bis zu diesem Bild zusammengesetzt.</summary>
    private BitmapSource Frame(int index)
    {
        var decoder = _decoder!;
        index = Math.Clamp(index, 0, decoder.Frames.Count - 1);
        if (decoder.Frames.Count > 1 && decoder.CodecInfo?.ContainerFormat == ContainerGif)
        {
            return ComposeGif(index);
        }

        return ImageCodecService.ApplyExifOrientation(decoder.Frames[index]);
    }

    /// <summary>
    /// Setzt ein Bild eines animierten GIF aus den Teilbildern zusammen (Position, Entsorgungsart „Hintergrund“ und
    /// „vorheriges Bild“), wie es ein Betrachter anzeigt. Fortlaufend: bereits zusammengesetzte Bilder werden gemerkt.
    /// </summary>
    private BitmapSource ComposeGif(int index)
    {
        var frames = _decoder!.Frames;
        _composedGif ??= new BitmapSource?[frames.Count];
        if (_composedGif[index] is { } cached)
        {
            return cached;
        }

        var (width, height) = GifScreenSize(frames[0]);
        _gifCanvas ??= new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        while (_gifComposed <= index)
        {
            var frame = frames[_gifComposed];
            var rect = GifFrameRect(frame);
            var disposal = GifMetadata<byte>(frame, "/grctlext/Disposal");
            var previous = disposal == 3 ? Snapshot(_gifCanvas) : null;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(frame, rect);
            }

            _gifCanvas.Render(visual);
            var composed = Snapshot(_gifCanvas);
            _composedGif[_gifComposed] = composed;
            _gifComposed++;

            if (disposal == 2)
            {
                _gifCanvas = Restore(composed, width, height, clear: rect);
            }
            else if (previous is not null)
            {
                _gifCanvas = Restore(previous, width, height, clear: null);
            }
        }

        return _composedGif[index]!;
    }

    private static WriteableBitmap Snapshot(RenderTargetBitmap canvas)
    {
        var copy = new WriteableBitmap(canvas);
        copy.Freeze();
        return copy;
    }

    private static RenderTargetBitmap Restore(BitmapSource source, int width, int height, Rect? clear)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (clear is { } c)
            {
                var outside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, width, height)), new RectangleGeometry(c));
                dc.PushClip(outside);
                dc.DrawImage(source, new Rect(0, 0, width, height));
                dc.Pop();
            }
            else
            {
                dc.DrawImage(source, new Rect(0, 0, width, height));
            }
        }

        var canvas = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        canvas.Render(visual);
        return canvas;
    }

    private (int Width, int Height) GifScreenSize(BitmapFrame first)
    {
        var width = GifMetadata<ushort>(_decoder!, "/logscrdesc/Width");
        var height = GifMetadata<ushort>(_decoder!, "/logscrdesc/Height");
        return width > 0 && height > 0 ? (width, height) : (first.PixelWidth, first.PixelHeight);
    }

    private static Rect GifFrameRect(BitmapFrame frame)
    {
        var left = GifMetadata<ushort>(frame, "/imgdesc/Left");
        var top = GifMetadata<ushort>(frame, "/imgdesc/Top");
        return new Rect(left, top, frame.PixelWidth, frame.PixelHeight);
    }

    private static T GifMetadata<T>(BitmapFrame frame, string query)
        where T : struct => Query<T>(frame.Metadata as BitmapMetadata, query);

    private static T GifMetadata<T>(BitmapDecoder decoder, string query)
        where T : struct
    {
        try
        {
            return Query<T>(decoder.Metadata, query);
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    private static T Query<T>(BitmapMetadata? metadata, string query)
        where T : struct
    {
        try
        {
            return metadata?.GetQuery(query) is T value ? value : default;
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return default;
        }
    }
}
