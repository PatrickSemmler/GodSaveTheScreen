using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>
/// Nicht-destruktive Transparenzmaske des Basisbilds: je Pixel ein Deckungswert (255 = sichtbar, 0 = entfernt).
/// Änderungen nur über <see cref="Write"/> bzw. direkt in <see cref="Values"/> mit anschließendem <see cref="NotifyChanged"/>;
/// rückgängig machbare Änderungen laufen über <c>MaskEditAction</c>.
/// </summary>
public sealed class AlphaMask
{
    public AlphaMask(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
        Values = new byte[checked(width * height)];
        Array.Fill(Values, (byte)255);
    }

    public int Width { get; }

    public int Height { get; }

    public PixelRect Bounds => new(0, 0, Width, Height);

    /// <summary>Deckungswerte zeilenweise (Index <c>y * Width + x</c>).</summary>
    public byte[] Values { get; }

    /// <summary>Wird bei jeder Änderung erhöht (Cache-Schlüssel).</summary>
    public int Version { get; private set; }

    /// <summary>Ein Bereich hat sich geändert (Anzeige aktualisieren).</summary>
    public event EventHandler<PixelRect>? Changed;

    /// <summary>Nichts ist entfernt.</summary>
    public bool IsOpaque => !Values.AsSpan().ContainsAnyExcept((byte)255);

    /// <summary>Kopiert die Werte eines Bereichs (an die Maske geklemmt).</summary>
    public byte[] Read(PixelRect region)
    {
        var r = region.Intersect(Bounds);
        var result = new byte[r.Width * r.Height];
        for (var row = 0; row < r.Height; row++)
        {
            Buffer.BlockCopy(Values, ((r.Y + row) * Width) + r.X, result, row * r.Width, r.Width);
        }

        return result;
    }

    /// <summary>Schreibt die Werte eines Bereichs (Größe wie <paramref name="region"/>, das innerhalb der Maske liegen muss).</summary>
    public void Write(PixelRect region, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (region.Intersect(Bounds) != region || data.Length != region.Width * region.Height)
        {
            throw new ArgumentException("Bereich und Daten passen nicht zur Maske.", nameof(region));
        }

        for (var row = 0; row < region.Height; row++)
        {
            Buffer.BlockCopy(data, row * region.Width, Values, ((region.Y + row) * Width) + region.X, region.Width);
        }

        NotifyChanged(region);
    }

    /// <summary>Meldet eine direkte Änderung an <see cref="Values"/>.</summary>
    public void NotifyChanged(PixelRect region)
    {
        Version++;
        Changed?.Invoke(this, region.Intersect(Bounds));
    }

    /// <summary>Wendet die Maske auf ein Bild gleicher Größe an (neuer Puffer; vollständig entfernte Pixel werden 0,0,0,0).</summary>
    public PixelBuffer ApplyTo(PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width != Width || image.Height != Height)
        {
            throw new ArgumentException("Bild und Maske haben unterschiedliche Größen.", nameof(image));
        }

        var result = new PixelBuffer(Width, Height);
        CopyMasked(image, result.Pixels, Bounds);
        return result;
    }

    /// <summary>Schreibt den maskierten Bereich <paramref name="region"/> von <paramref name="image"/> zeilenweise nach <paramref name="target"/> (BGRA, Breite = Bereich).</summary>
    public void CopyMasked(PixelBuffer image, byte[] target, PixelRect region)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(target);
        var source = image.Pixels;
        for (var row = 0; row < region.Height; row++)
        {
            var y = region.Y + row;
            var sourceOffset = (y * image.Stride) + (region.X * PixelBuffer.BytesPerPixel);
            var targetOffset = row * region.Width * PixelBuffer.BytesPerPixel;
            var maskOffset = (y * Width) + region.X;
            for (var x = 0; x < region.Width; x++)
            {
                var m = Values[maskOffset + x];
                var s = sourceOffset + (x * PixelBuffer.BytesPerPixel);
                var t = targetOffset + (x * PixelBuffer.BytesPerPixel);
                var alpha = m == 255 ? source[s + 3] : (source[s + 3] * m) / 255;
                if (alpha == 0)
                {
                    target[t] = target[t + 1] = target[t + 2] = target[t + 3] = 0;
                    continue;
                }

                target[t] = source[s];
                target[t + 1] = source[s + 1];
                target[t + 2] = source[s + 2];
                target[t + 3] = (byte)alpha;
            }
        }
    }

    /// <summary>Umschließendes Rechteck aller Pixel mit Alpha &gt; 0; <c>null</c>, wenn alles transparent ist.</summary>
    public static PixelRect? VisibleBounds(PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(image);
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        var pixels = image.Pixels;
        for (var y = 0; y < image.Height; y++)
        {
            var offset = y * image.Stride;
            for (var x = 0; x < image.Width; x++)
            {
                if (pixels[offset + (x * PixelBuffer.BytesPerPixel) + 3] == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = y;
            }
        }

        return right < 0 ? null : PixelRect.FromLtrb(left, top, right + 1, bottom + 1);
    }
}
