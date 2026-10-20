using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Form des Bereichsauswahl-Werkzeugs.</summary>
public enum AreaSelectShape
{
    Rectangle,
    Ellipse,
    Lasso,
    MagicWand,
}

/// <summary>Wie ein neuer Bereich mit der bestehenden Auswahl verrechnet wird.</summary>
public enum SelectionCombine
{
    Replace,
    Add,
    Subtract,
}

/// <summary>
/// Pixelauswahl im Basisbild (je Pixel Deckung 0–255) für das Entfernen von Bildbereichen.
/// Reiner Bearbeitungszustand: nicht Teil des Dokuments und nicht im Undo-Verlauf.
/// </summary>
public sealed class AreaSelection
{
    private const int EllipseSegments = 128;

    private PixelRect? _bounds;

    public AreaSelection(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
        Values = new byte[checked(width * height)];
        _bounds = PixelRect.Empty;
    }

    public int Width { get; }

    public int Height { get; }

    public PixelRect Area => new(0, 0, Width, Height);

    /// <summary>Deckung zeilenweise (Index <c>y * Width + x</c>); nur über die Methoden ändern.</summary>
    public byte[] Values { get; }

    /// <summary>Wird bei jeder Änderung erhöht (Cache-Schlüssel der Anzeige).</summary>
    public int Version { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Umschließendes Rechteck aller ausgewählten Pixel (leer, wenn nichts ausgewählt ist).</summary>
    public PixelRect Bounds => _bounds ??= ComputeBounds();

    public bool IsEmpty => Bounds.IsEmpty;

    // ---- Formen rastern ----

    /// <summary>Rechteck, auf ganze Pixel gerundet.</summary>
    public static (PixelRect Box, byte[] Coverage) Rectangle(RectD rect, PixelRect clip)
    {
        var r = rect.Normalize();
        var box = PixelRect.FromLtrb(
            (int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom)).Intersect(clip);
        if (box.IsEmpty)
        {
            return (PixelRect.Empty, []);
        }

        var coverage = new byte[box.Width * box.Height];
        Array.Fill(coverage, (byte)255);
        return (box, coverage);
    }

    /// <summary>Ellipse im Rechteck, Kante geglättet.</summary>
    public static (PixelRect Box, byte[] Coverage) Ellipse(RectD rect, PixelRect clip)
    {
        var r = rect.Normalize();
        if (r.Width < 1 || r.Height < 1)
        {
            return (PixelRect.Empty, []);
        }

        var center = r.Center;
        var points = new PointD[EllipseSegments];
        for (var i = 0; i < EllipseSegments; i++)
        {
            var angle = 2 * Math.PI * i / EllipseSegments;
            points[i] = new PointD(center.X + (Math.Cos(angle) * r.Width / 2), center.Y + (Math.Sin(angle) * r.Height / 2));
        }

        return PolygonMask.Coverage(points, clip);
    }

    /// <summary>Freihand-Umriss (geschlossen), Kante geglättet.</summary>
    public static (PixelRect Box, byte[] Coverage) Lasso(IReadOnlyList<PointD> points, PixelRect clip) =>
        PolygonMask.Coverage(points, clip);

    /// <summary>
    /// Zauberstab: zusammenhängende Fläche (4er-Nachbarschaft) ab <paramref name="seed"/>, deren Farbe je Kanal (inkl. Alpha)
    /// höchstens <paramref name="tolerancePercent"/> % von der Startfarbe abweicht.
    /// </summary>
    public static (PixelRect Box, byte[] Coverage) MagicWand(PixelBuffer image, PointD seed, double tolerancePercent)
    {
        ArgumentNullException.ThrowIfNull(image);
        var sx = (int)Math.Floor(seed.X);
        var sy = (int)Math.Floor(seed.Y);
        if (!image.Bounds.Contains(sx, sy))
        {
            return (PixelRect.Empty, []);
        }

        var width = image.Width;
        var height = image.Height;
        var pixels = image.Pixels;
        var limit = (int)Math.Round(Math.Clamp(tolerancePercent, 0, 100) * 255 / 100);
        var start = ((sy * width) + sx) * PixelBuffer.BytesPerPixel;
        int b0 = pixels[start], g0 = pixels[start + 1], r0 = pixels[start + 2], a0 = pixels[start + 3];

        bool Matches(int index)
        {
            var i = index * PixelBuffer.BytesPerPixel;
            // Vollständig transparente Pixel gelten untereinander als gleich (ihre Farbe ist bedeutungslos).
            if (a0 == 0 && pixels[i + 3] == 0)
            {
                return true;
            }

            return Math.Abs(pixels[i] - b0) <= limit
                && Math.Abs(pixels[i + 1] - g0) <= limit
                && Math.Abs(pixels[i + 2] - r0) <= limit
                && Math.Abs(pixels[i + 3] - a0) <= limit;
        }

        var filled = new bool[width * height];
        int left = sx, right = sx, top = sy, bottom = sy;
        var stack = new Stack<(int X, int Y)>();
        stack.Push((sx, sy));
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            var row = y * width;
            if (filled[row + x] || !Matches(row + x))
            {
                continue;
            }

            // Zeile nach links und rechts füllen, darüber/darunter je Abschnitt einen Startpunkt merken.
            var x0 = x;
            while (x0 > 0 && !filled[row + x0 - 1] && Matches(row + x0 - 1))
            {
                x0--;
            }

            var x1 = x;
            while (x1 < width - 1 && !filled[row + x1 + 1] && Matches(row + x1 + 1))
            {
                x1++;
            }

            for (var i = x0; i <= x1; i++)
            {
                filled[row + i] = true;
            }

            left = Math.Min(left, x0);
            right = Math.Max(right, x1);
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
            PushSpans(y - 1);
            PushSpans(y + 1);

            void PushSpans(int ny)
            {
                if (ny < 0 || ny >= height)
                {
                    return;
                }

                var nrow = ny * width;
                var inside = false;
                for (var i = x0; i <= x1; i++)
                {
                    var candidate = !filled[nrow + i] && Matches(nrow + i);
                    if (candidate && !inside)
                    {
                        stack.Push((i, ny));
                    }

                    inside = candidate;
                }
            }
        }

        var box = PixelRect.FromLtrb(left, top, right + 1, bottom + 1);
        var coverage = new byte[box.Width * box.Height];
        for (var y = 0; y < box.Height; y++)
        {
            var source = ((box.Y + y) * width) + box.X;
            for (var x = 0; x < box.Width; x++)
            {
                if (filled[source + x])
                {
                    coverage[(y * box.Width) + x] = 255;
                }
            }
        }

        return (box, coverage);
    }

    // ---- Auswahl verändern ----

    /// <summary>Verrechnet einen gerasterten Bereich mit der Auswahl.</summary>
    public void Combine(PixelRect box, byte[] coverage, SelectionCombine mode)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (mode == SelectionCombine.Replace)
        {
            Array.Clear(Values);
        }

        var clipped = box.Intersect(Area);
        for (var y = clipped.Y; y < clipped.Bottom; y++)
        {
            var source = ((y - box.Y) * box.Width) - box.X;
            var target = y * Width;
            for (var x = clipped.X; x < clipped.Right; x++)
            {
                var c = coverage[source + x];
                ref var v = ref Values[target + x];
                v = mode == SelectionCombine.Subtract ? (byte)((v * (255 - c)) / 255) : Math.Max(v, c);
            }
        }

        OnChanged();
    }

    public void SelectAll()
    {
        Array.Fill(Values, (byte)255);
        OnChanged();
    }

    public void Clear()
    {
        if (IsEmpty)
        {
            return;
        }

        Array.Clear(Values);
        OnChanged();
    }

    public void Invert()
    {
        for (var i = 0; i < Values.Length; i++)
        {
            Values[i] = (byte)(255 - Values[i]);
        }

        OnChanged();
    }

    /// <summary>
    /// Deckung mit weicher Kante: der Rand läuft über etwa <paramref name="feather"/> Pixel aus (dreifacher Box-Filter,
    /// außerhalb des Bilds wird der Randwert fortgesetzt). Liefert den betroffenen Bereich; leer, wenn nichts ausgewählt ist.
    /// </summary>
    public (PixelRect Box, byte[] Coverage) Feathered(double feather)
    {
        var bounds = Bounds;
        if (bounds.IsEmpty)
        {
            return (PixelRect.Empty, []);
        }

        var radius = feather >= 1 ? Math.Max(1, (int)Math.Round(feather / 3)) : 0;
        var box = PixelRect.FromLtrb(bounds.X - (3 * radius), bounds.Y - (3 * radius), bounds.Right + (3 * radius), bounds.Bottom + (3 * radius)).Intersect(Area);
        var data = new byte[box.Width * box.Height];
        for (var y = 0; y < box.Height; y++)
        {
            Buffer.BlockCopy(Values, ((box.Y + y) * Width) + box.X, data, y * box.Width, box.Width);
        }

        if (radius > 0)
        {
            var temp = new byte[data.Length];
            for (var pass = 0; pass < 3; pass++)
            {
                BoxBlur(data, temp, box.Width, box.Height, radius, horizontal: true);
                BoxBlur(temp, data, box.Width, box.Height, radius, horizontal: false);
            }
        }

        return (box, data);
    }

    /// <summary>Gleitender Mittelwert über 2·radius+1 Pixel in einer Richtung; Randwerte werden fortgesetzt.</summary>
    private static void BoxBlur(byte[] source, byte[] target, int width, int height, int radius, bool horizontal)
    {
        var lines = horizontal ? height : width;
        var length = horizontal ? width : height;
        var step = horizontal ? 1 : width;
        var window = (2 * radius) + 1;
        for (var line = 0; line < lines; line++)
        {
            var origin = horizontal ? line * width : line;
            int At(int i) => source[origin + (Math.Clamp(i, 0, length - 1) * step)];
            var sum = 0;
            for (var i = -radius; i <= radius; i++)
            {
                sum += At(i);
            }

            for (var i = 0; i < length; i++)
            {
                target[origin + (i * step)] = (byte)((sum + (window / 2)) / window);
                sum += At(i + radius + 1) - At(i - radius);
            }
        }
    }

    private void OnChanged()
    {
        _bounds = null;
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private PixelRect ComputeBounds()
    {
        int left = Width, top = Height, right = -1, bottom = -1;
        for (var y = 0; y < Height; y++)
        {
            var row = Values.AsSpan(y * Width, Width);
            var first = row.IndexOfAnyExcept((byte)0);
            if (first < 0)
            {
                continue;
            }

            var last = row.LastIndexOfAnyExcept((byte)0);
            left = Math.Min(left, first);
            right = Math.Max(right, last);
            top = Math.Min(top, y);
            bottom = y;
        }

        return right < 0 ? PixelRect.Empty : PixelRect.FromLtrb(left, top, right + 1, bottom + 1);
    }
}
