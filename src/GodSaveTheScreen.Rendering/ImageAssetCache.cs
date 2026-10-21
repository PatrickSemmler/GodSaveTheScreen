using System.IO;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Dekodierte Bilddateien der Bildobjekte: Rasterbilder einmal in voller Auflösung, SVGs je Rastergröße
/// (bei jeder Zoomstufe scharf). LRU mit Obergrenze in Bytes; nicht darstellbare Dateien werden als <c>null</c> gemerkt.
/// </summary>
public sealed class ImageAssetCache
{
    public const long DefaultCapacityBytes = 512L * 1024 * 1024;

    /// <summary>Höchstzahl gleichzeitig geladener SVG-Dokumente (Skia-Zeichnungen).</summary>
    private const int MaxPictures = 16;

    private readonly object _gate = new();
    private readonly Dictionary<(string Hash, ImageAssetFormat Format, int Width, int Height), LinkedListNode<Entry>> _map = [];
    private readonly LinkedList<Entry> _order = new();
    private readonly Dictionary<string, LinkedListNode<(string Hash, SvgPicture? Picture)>> _pictureMap = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Hash, SvgPicture? Picture)> _pictureOrder = new();
    private readonly long _capacity;
    private long _bytes;

    public ImageAssetCache(long capacityBytes = DefaultCapacityBytes) => _capacity = capacityBytes;

    public static ImageAssetCache Shared { get; } = new();

    /// <summary>Anzahl der Dekodierungen bzw. Rasterungen (für Tests).</summary>
    public int Misses { get; private set; }

    public long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>
    /// Bitmap der Datei: Rasterbilder in voller Auflösung (<paramref name="pixelWidth"/>/<paramref name="pixelHeight"/> werden
    /// ignoriert), SVGs in genau dieser Pixelgröße. <c>null</c>, wenn die Datei nicht darstellbar ist.
    /// </summary>
    public BitmapSource? Get(ImageAsset asset, int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var isSvg = asset.Format == ImageAssetFormat.Svg;
        var key = isSvg
            ? (asset.Hash, asset.Format, Math.Clamp(pixelWidth, 1, SvgRasterizer.MaxPixelSize), Math.Clamp(pixelHeight, 1, SvgRasterizer.MaxPixelSize))
            : (asset.Hash, asset.Format, 0, 0);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        var bitmap = isSvg ? RenderSvg(asset, key.Item3, key.Item4) : DecodeRaster(asset);
        var bytes = bitmap is null ? 64 : (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        lock (_gate)
        {
            Misses++;
            if (_map.TryGetValue(key, out var existing))
            {
                return existing.Value.Bitmap;
            }

            var node = _order.AddFirst(new Entry(key, bitmap, bytes));
            _map[key] = node;
            _bytes += bytes;
            while (_bytes > _capacity && _order.Count > 1)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                _bytes -= last.Value.Bytes;
            }
        }

        return bitmap;
    }

    /// <summary>Größe der Zeichnung eines SVG in SVG-Einheiten; <c>null</c>, wenn es nicht darstellbar ist.</summary>
    public (double Width, double Height)? SvgSize(ImageAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        lock (_gate)
        {
            return Picture(asset) is { } picture ? (picture.Width, picture.Height) : null;
        }
    }

    private static BitmapSource? DecodeRaster(ImageAsset asset)
    {
        try
        {
            var decoder = BitmapDecoder.Create(new MemoryStream(asset.Data, writable: false), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            // Pixel in eine eigenständige, eingefrorene Bitmap kopieren: Dekoder-Bilder sind an ihren Thread gebunden,
            // der gemeinsame Cache wird aber auch vom Export genutzt.
            var frame = ImageCodecService.ApplyExifOrientation(decoder.Frames[0]);
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Pbgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            var bitmap = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, pixels, stride);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or IOException or OverflowException)
        {
            return null;
        }
    }

    private BitmapSource? RenderSvg(ImageAsset asset, int width, int height)
    {
        // Skia-Zeichnungen sind nicht threadsicher: Rastern unter derselben Sperre wie der Zugriff auf die Liste.
        lock (_gate)
        {
            return Picture(asset) is { } picture ? SvgRasterizer.Render(picture, width, height) : null;
        }
    }

    /// <summary>Geladenes SVG (LRU, unter <see cref="_gate"/> aufrufen).</summary>
    private SvgPicture? Picture(ImageAsset asset)
    {
        if (_pictureMap.TryGetValue(asset.Hash, out var node))
        {
            _pictureOrder.Remove(node);
            _pictureOrder.AddFirst(node);
            return node.Value.Picture;
        }

        var picture = SvgRasterizer.Load(asset.Data);
        _pictureMap[asset.Hash] = _pictureOrder.AddFirst((asset.Hash, picture));
        while (_pictureOrder.Count > MaxPictures)
        {
            var last = _pictureOrder.Last!;
            _pictureOrder.RemoveLast();
            _pictureMap.Remove(last.Value.Hash);
            last.Value.Picture?.Dispose();
        }

        return picture;
    }

    private sealed record Entry((string Hash, ImageAssetFormat Format, int Width, int Height) Key, BitmapSource? Bitmap, long Bytes);
}
