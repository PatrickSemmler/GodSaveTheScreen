using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>Berechnet Unschärfe, Verpixelung und Schwärzung auf einem <see cref="PixelBuffer"/>. Quelle ist immer das Basisbild.</summary>
public static class RegionEffectProcessor
{
    /// <summary>Ab dieser Fläche wird zeilenweise parallel gerechnet.</summary>
    private const int ParallelThreshold = 256 * 256;

    /// <summary>Ganzzahliger Bereich im Bild, den der Effekt eines Objekts belegt (leer = nichts zu zeichnen).</summary>
    public static PixelRect RegionOf(BlurAnnotation a, int imageWidth, int imageHeight)
    {
        var r = a.Rect.Normalize();
        var hard = a.Shape == RegionShape.Rectangle && a.FeatherEdge <= 0;
        var region = hard
            ? PixelRect.FromLtrb((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom))
            : PixelRect.FromLtrb((int)Math.Floor(r.X), (int)Math.Floor(r.Y), (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom));
        return region.Intersect(new PixelRect(0, 0, imageWidth, imageHeight));
    }

    /// <summary>
    /// Liefert die Pixel des Bereichs <paramref name="region"/> mit Effekt (und Formmaske im Alphakanal).
    /// <paramref name="shapeRect"/> ist die exakte Form (für Ellipse und weichen Rand).
    /// </summary>
    public static PixelBuffer Process(
        PixelBuffer source, PixelRect region, BlurMode mode, double strength, RegionShape shape, double feather, ArgbColor redactColor, RectD shapeRect)
    {
        ArgumentNullException.ThrowIfNull(source);
        region = region.Intersect(source.Bounds);
        var result = new PixelBuffer(region.Width, region.Height);
        if (region.IsEmpty)
        {
            return result;
        }

        var amount = (int)Math.Round(Math.Clamp(strength, BlurAnnotation.MinStrength, BlurAnnotation.MaxStrength));
        switch (mode)
        {
            case BlurMode.GaussianBlur:
                GaussianBlur(source, region, amount, result);
                break;
            case BlurMode.Pixelate:
                Pixelate(source, region, amount, result);
                break;
            default:
                Fill(result, redactColor);
                break;
        }

        ApplyMask(result, region, shape, feather, shapeRect);
        return result;
    }

    /// <summary>Drei Box-Blur-Durchläufe (horizontal und vertikal) nähern einen Gauß an; Ränder werden an den Bildgrenzen geklemmt.</summary>
    public static void GaussianBlur(PixelBuffer source, PixelRect region, int radius, PixelBuffer destination)
    {
        var margin = radius * 3;
        var expanded = PixelRect.FromLtrb(region.X - margin, region.Y - margin, region.Right + margin, region.Bottom + margin).Intersect(source.Bounds);
        var w = expanded.Width;
        var h = expanded.Height;
        var channels = PixelBuffer.BytesPerPixel;
        var a = new float[w * h * channels];
        var b = new float[a.Length];
        for (var y = 0; y < h; y++)
        {
            var src = ((expanded.Y + y) * source.Stride) + (expanded.X * channels);
            for (var i = 0; i < w * channels; i++)
            {
                a[(y * w * channels) + i] = source.Pixels[src + i];
            }
        }

        var parallel = w * h > ParallelThreshold;
        for (var pass = 0; pass < 3; pass++)
        {
            BoxPass(a, b, w, h, radius, horizontal: true, parallel);
            BoxPass(b, a, w, h, radius, horizontal: false, parallel);
        }

        for (var y = 0; y < region.Height; y++)
        {
            var from = (((region.Y - expanded.Y + y) * w) + (region.X - expanded.X)) * channels;
            var to = y * destination.Stride;
            for (var i = 0; i < region.Width * channels; i++)
            {
                destination.Pixels[to + i] = (byte)Math.Clamp((int)Math.Round(a[from + i]), 0, 255);
            }
        }
    }

    private static void BoxPass(float[] input, float[] output, int w, int h, int radius, bool horizontal, bool parallel)
    {
        const int channels = PixelBuffer.BytesPerPixel;
        var lines = horizontal ? h : w;
        var length = horizontal ? w : h;
        var step = horizontal ? channels : w * channels;
        var lineStep = horizontal ? w * channels : channels;
        var scale = 1f / ((2 * radius) + 1);

        void Line(int line)
        {
            for (var c = 0; c < channels; c++)
            {
                var offset = (line * lineStep) + c;
                double sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += input[offset + (Math.Clamp(k, 0, length - 1) * step)];
                }

                for (var i = 0; i < length; i++)
                {
                    output[offset + (i * step)] = (float)(sum * scale);
                    var add = Math.Clamp(i + radius + 1, 0, length - 1);
                    var remove = Math.Clamp(i - radius, 0, length - 1);
                    sum += input[offset + (add * step)] - input[offset + (remove * step)];
                }
            }
        }

        if (parallel)
        {
            Parallel.For(0, lines, Line);
        }
        else
        {
            for (var line = 0; line < lines; line++)
            {
                Line(line);
            }
        }
    }

    /// <summary>Blockmittelwert; das Raster ist am Bildursprung ausgerichtet (stabil beim Verschieben).</summary>
    public static void Pixelate(PixelBuffer source, PixelRect region, int block, PixelBuffer destination)
    {
        block = Math.Max(1, block);
        const int channels = PixelBuffer.BytesPerPixel;
        var firstBlockX = region.X / block;
        var lastBlockX = (region.Right - 1) / block;
        var firstBlockY = region.Y / block;
        var lastBlockY = (region.Bottom - 1) / block;
        var blockRows = lastBlockY - firstBlockY + 1;

        void BlockRow(int rowIndex)
        {
            var by = firstBlockY + rowIndex;
            var y0 = by * block;
            var y1 = Math.Min(y0 + block, source.Height);
            Span<long> sums = stackalloc long[channels];
            for (var bx = firstBlockX; bx <= lastBlockX; bx++)
            {
                var x0 = bx * block;
                var x1 = Math.Min(x0 + block, source.Width);
                sums.Clear();
                for (var y = y0; y < y1; y++)
                {
                    var i = (y * source.Stride) + (x0 * channels);
                    for (var x = x0; x < x1; x++, i += channels)
                    {
                        sums[0] += source.Pixels[i];
                        sums[1] += source.Pixels[i + 1];
                        sums[2] += source.Pixels[i + 2];
                        sums[3] += source.Pixels[i + 3];
                    }
                }

                var count = (long)(x1 - x0) * (y1 - y0);
                var b = (byte)((sums[0] + (count / 2)) / count);
                var g = (byte)((sums[1] + (count / 2)) / count);
                var r = (byte)((sums[2] + (count / 2)) / count);
                var a = (byte)((sums[3] + (count / 2)) / count);

                var dx0 = Math.Max(x0, region.X);
                var dx1 = Math.Min(x0 + block, region.Right);
                var dy0 = Math.Max(y0, region.Y);
                var dy1 = Math.Min(y0 + block, region.Bottom);
                for (var y = dy0; y < dy1; y++)
                {
                    var o = ((y - region.Y) * destination.Stride) + ((dx0 - region.X) * channels);
                    for (var x = dx0; x < dx1; x++, o += channels)
                    {
                        destination.Pixels[o] = b;
                        destination.Pixels[o + 1] = g;
                        destination.Pixels[o + 2] = r;
                        destination.Pixels[o + 3] = a;
                    }
                }
            }
        }

        if (region.Width * region.Height > ParallelThreshold)
        {
            Parallel.For(0, blockRows, BlockRow);
        }
        else
        {
            for (var i = 0; i < blockRows; i++)
            {
                BlockRow(i);
            }
        }
    }

    private static void Fill(PixelBuffer destination, ArgbColor color)
    {
        var pixels = destination.Pixels;
        for (var i = 0; i + 3 < destination.Width * destination.Height * PixelBuffer.BytesPerPixel; i += PixelBuffer.BytesPerPixel)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = color.A;
        }
    }

    /// <summary>Formmaske (Ellipse, weicher Rand) im Alphakanal; ein hartes, ganzzahliges Rechteck braucht keine.</summary>
    private static void ApplyMask(PixelBuffer buffer, PixelRect region, RegionShape shape, double feather, RectD shapeRect)
    {
        if (shape == RegionShape.Rectangle && feather <= 0)
        {
            return;
        }

        var rect = shapeRect.Normalize();
        var cx = rect.X + (rect.Width / 2);
        var cy = rect.Y + (rect.Height / 2);
        var a = Math.Max(rect.Width / 2, 1e-6);
        var b = Math.Max(rect.Height / 2, 1e-6);
        var softness = feather > 0 ? feather : 1;
        var rawOffset = feather > 0 ? 0 : 0.5;

        void Row(int y)
        {
            var py = region.Y + y + 0.5;
            var i = y * buffer.Stride;
            for (var x = 0; x < region.Width; x++, i += PixelBuffer.BytesPerPixel)
            {
                var px = region.X + x + 0.5;
                double distance;
                if (shape == RegionShape.Ellipse)
                {
                    var dx = (px - cx) / a;
                    var dy = (py - cy) / b;
                    distance = (1 - Math.Sqrt((dx * dx) + (dy * dy))) * Math.Min(a, b);
                }
                else
                {
                    distance = Math.Min(Math.Min(px - rect.X, rect.Right - px), Math.Min(py - rect.Y, rect.Bottom - py));
                }

                var alpha = Math.Clamp((distance / softness) + rawOffset, 0, 1);
                buffer.Pixels[i + 3] = (byte)Math.Round(buffer.Pixels[i + 3] * alpha);
            }
        }

        if (region.Width * region.Height > ParallelThreshold)
        {
            Parallel.For(0, region.Height, Row);
        }
        else
        {
            for (var y = 0; y < region.Height; y++)
            {
                Row(y);
            }
        }
    }
}

/// <summary>Vergibt je Basisbild eine stabile Kennung für den <see cref="EffectCache"/>.</summary>
public static class BaseImageIdentity
{
    private static readonly ConditionalWeakTable<PixelBuffer, object> Ids = [];
    private static long _next;

    public static long Get(PixelBuffer buffer) =>
        (long)Ids.GetValue(buffer, _ => Interlocked.Increment(ref _next));
}

/// <summary>Schlüssel eines berechneten Effekts.</summary>
public readonly record struct EffectKey(
    long BaseId, PixelRect Region, RectD ShapeRect, BlurMode Mode, int Strength, RegionShape Shape, double Feather, ArgbColor Color);

/// <summary>LRU-Cache berechneter Effekt-Bitmaps (begrenzt nach Speicherverbrauch).</summary>
public sealed class EffectCache
{
    public const long DefaultCapacityBytes = 64L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Dictionary<EffectKey, LinkedListNode<(EffectKey Key, BitmapSource Bitmap, long Bytes)>> _map = [];
    private readonly LinkedList<(EffectKey Key, BitmapSource Bitmap, long Bytes)> _order = new();
    private readonly long _capacity;
    private long _bytes;

    public EffectCache(long capacityBytes = DefaultCapacityBytes) => _capacity = capacityBytes;

    public static EffectCache Shared { get; } = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>Anzahl der Berechnungen (für Tests).</summary>
    public int Misses { get; private set; }

    public BitmapSource GetOrCreate(EffectKey key, Func<BitmapSource> create)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        var bitmap = create();
        var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        lock (_gate)
        {
            Misses++;
            if (_map.TryGetValue(key, out var existing))
            {
                return existing.Value.Bitmap;
            }

            var created = _order.AddFirst((key, bitmap, bytes));
            _map[key] = created;
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

    public bool Contains(EffectKey key)
    {
        lock (_gate)
        {
            return _map.ContainsKey(key);
        }
    }
}
