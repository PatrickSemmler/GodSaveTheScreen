using System.Windows.Media.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// LRU-Cache gerasterter Emojis. Schlüssel = Sequenz + Pixelgröße (auf 25-%-Stufen gerundet), Speichergrenze in Bytes.
/// </summary>
public sealed class EmojiCache
{
    public const long DefaultCapacityBytes = 48L * 1024 * 1024;

    private static readonly int[] Steps = BuildSteps();

    private readonly object _gate = new();
    private readonly Dictionary<(string Sequence, int Size), LinkedListNode<Entry>> _map = [];
    private readonly LinkedList<Entry> _order = new();
    private readonly long _capacity;
    private long _bytes;

    public EmojiCache(long capacityBytes = DefaultCapacityBytes) => _capacity = capacityBytes;

    public static EmojiCache Shared { get; } = new();

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

    /// <summary>Anzahl der Rasterungen (für Tests).</summary>
    public int Misses { get; private set; }

    /// <summary>Rundet eine gewünschte Pixelgröße auf die nächste Stufe (Faktor ≈ 1,25) und begrenzt sie; Stufen bleiben unter sich stabil.</summary>
    public static int Quantize(double pixels, int maximum = EmojiRasterizer.MaxPixelSize)
    {
        var limit = Math.Min(maximum, EmojiRasterizer.MaxPixelSize);
        foreach (var step in Steps)
        {
            if (step >= pixels || step >= limit)
            {
                return Math.Min(step, limit);
            }
        }

        return limit;
    }

    /// <summary>Bitmap der Sequenz in der (bereits quantisierten) Pixelgröße; <c>null</c>, wenn nicht darstellbar.</summary>
    public BitmapSource? Get(string sequence, int pixelSize)
    {
        var key = (sequence, pixelSize);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Bitmap;
            }
        }

        var bitmap = EmojiRasterizer.RenderBitmap(sequence, pixelSize);
        if (bitmap is null)
        {
            return null;
        }

        var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        lock (_gate)
        {
            Misses++;
            if (_map.TryGetValue(key, out var existing))
            {
                return existing.Value.Bitmap;
            }

            _map[key] = _order.AddFirst(new Entry(key, bitmap, bytes));
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

    private static int[] BuildSteps()
    {
        var steps = new List<int>();
        for (var size = (double)EmojiRasterizer.MinPixelSize; ; size *= 1.25)
        {
            var rounded = (int)Math.Round(size);
            if (steps.Count == 0 || rounded > steps[^1])
            {
                steps.Add(rounded);
            }

            if (rounded >= EmojiRasterizer.MaxPixelSize)
            {
                return [.. steps];
            }
        }
    }

    private sealed record Entry((string Sequence, int Size) Key, BitmapSource Bitmap, long Bytes);
}
