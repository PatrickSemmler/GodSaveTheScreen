using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.Rendering;

/// <summary>Gerasterte, einfarbige und weichgezeichnete Ebene (Schatten, weiche Kontur) mit ihrer Lage in Bildpixeln.</summary>
public sealed record SoftLayer(BitmapSource Bitmap, Rect Rect)
{
    /// <summary>
    /// Rastert die Deckung von <paramref name="drawing"/> mit <paramref name="scale"/> Pixeln je Bildpixel,
    /// zeichnet sie um <paramref name="blur"/> Bildpixel weich und färbt sie mit <paramref name="color"/>
    /// (Deckung × Farb-Alpha). <c>null</c> bei leerer Zeichnung oder transparenter Farbe.
    /// </summary>
    public static SoftLayer? Create(Drawing drawing, ArgbColor color, double blur, double scale, int maxPixels)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        var bounds = drawing.Bounds;
        if (color.A == 0 || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        blur = Math.Max(0, blur);
        var pad = blur + 1;
        scale = Math.Max(scale, 0.01);
        var longest = Math.Max(bounds.Width, bounds.Height) + (2 * pad);
        if (longest * scale > maxPixels)
        {
            scale = maxPixels / longest;
        }

        // Am Pixelraster ausgerichtet, damit das Ergebnis im Export (Maßstab 1) nicht neu abgetastet wird.
        var left = Math.Floor((bounds.X - pad) * scale) / scale;
        var top = Math.Floor((bounds.Y - pad) * scale) / scale;
        var width = Math.Max(1, (int)Math.Ceiling((bounds.Right + pad - left) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((bounds.Bottom + pad - top) * scale));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, -left * scale, -top * scale));
            dc.DrawDrawing(drawing);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[width * height * 4];
        target.CopyPixels(pixels, width * 4, 0);

        var mask = new float[width * height];
        for (var i = 0; i < mask.Length; i++)
        {
            mask[i] = pixels[(i * 4) + 3];
        }

        // Drei Box-Durchläufe mit Radius r breiten sich um etwa 3·r aus: so entspricht der weiche Rand etwa „blur“.
        RegionEffectProcessor.BlurChannel(mask, width, height, (int)Math.Round(blur * scale / 3));

        var alphaScale = color.A / 255f;
        for (var i = 0; i < mask.Length; i++)
        {
            var o = i * 4;
            pixels[o] = color.B;
            pixels[o + 1] = color.G;
            pixels[o + 2] = color.R;
            pixels[o + 3] = (byte)Math.Clamp((int)Math.Round(mask[i] * alphaScale), 0, 255);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return new SoftLayer(bitmap, new Rect(left, top, width / scale, height / scale));
    }

    /// <summary>
    /// Kontur um die deckenden Teile von <paramref name="drawing"/> (Bildobjekte): Die Deckung wird um
    /// <paramref name="thickness"/> Bildpixel geweitet (Abstandstransformation, Kante geglättet), um <paramref name="blur"/>
    /// weichgezeichnet und mit <paramref name="color"/> gefärbt. Die Ebene wird unter das Bild gezeichnet.
    /// </summary>
    public static SoftLayer? CreateOutline(Drawing drawing, ArgbColor color, double thickness, double blur, double scale, int maxPixels)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        var bounds = drawing.Bounds;
        if (color.A == 0 || thickness <= 0 || bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        blur = Math.Max(0, blur);
        var pad = thickness + blur + 1;
        scale = Math.Max(scale, 0.01);
        var longest = Math.Max(bounds.Width, bounds.Height) + (2 * pad);
        if (longest * scale > maxPixels)
        {
            scale = maxPixels / longest;
        }

        var left = Math.Floor((bounds.X - pad) * scale) / scale;
        var top = Math.Floor((bounds.Y - pad) * scale) / scale;
        var width = Math.Max(1, (int)Math.Ceiling((bounds.Right + pad - left) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((bounds.Bottom + pad - top) * scale));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, -left * scale, -top * scale));
            dc.DrawDrawing(drawing);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[width * height * 4];
        target.CopyPixels(pixels, width * 4, 0);

        // Abstand jedes Pixels zum nächsten (überwiegend) deckenden Pixel; die Kontur deckt alles bis zur Stärke.
        var distance = DistanceTransform.SquaredDistanceToInside(pixels, width, height, OutlineAlphaThreshold);
        var radius = thickness * scale;
        var mask = new float[width * height];
        for (var i = 0; i < mask.Length; i++)
        {
            var coverage = Math.Clamp(radius - Math.Sqrt(distance[i]) + 0.5, 0, 1) * 255;
            mask[i] = (float)Math.Max(coverage, pixels[(i * 4) + 3]);
        }

        RegionEffectProcessor.BlurChannel(mask, width, height, (int)Math.Round(blur * scale / 3));

        var alphaScale = color.A / 255f;
        for (var i = 0; i < mask.Length; i++)
        {
            var o = i * 4;
            pixels[o] = color.B;
            pixels[o + 1] = color.G;
            pixels[o + 2] = color.R;
            pixels[o + 3] = (byte)Math.Clamp((int)Math.Round(mask[i] * alphaScale), 0, 255);
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return new SoftLayer(bitmap, new Rect(left, top, width / scale, height / scale));
    }

    /// <summary>Ab dieser Deckung (0–255) gilt ein Pixel als Teil des Bildes, um den die Kontur läuft.</summary>
    private const byte OutlineAlphaThreshold = 64;
}

/// <summary>Euklidische Abstandstransformation (Felzenszwalb/Huttenlocher), linear in der Pixelzahl.</summary>
public static class DistanceTransform
{
    /// <summary>
    /// Quadrierter Abstand (in Pixeln) jedes Pixels zum nächsten Pixel, dessen Alpha (BGRA bzw. PBGRA, 4 Byte je Pixel)
    /// mindestens <paramref name="threshold"/> ist. Ohne solches Pixel ist der Abstand sehr groß.
    /// </summary>
    public static double[] SquaredDistanceToInside(byte[] pixels, int width, int height, byte threshold)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        const double Far = 1e20;
        var grid = new double[width * height];
        for (var i = 0; i < grid.Length; i++)
        {
            grid[i] = pixels[(i * 4) + 3] >= threshold ? 0 : Far;
        }

        var length = Math.Max(width, height);
        var f = new double[length];
        var d = new double[length];
        var v = new int[length];
        var z = new double[length + 1];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                f[y] = grid[(y * width) + x];
            }

            Transform1D(f, d, v, z, height);
            for (var y = 0; y < height; y++)
            {
                grid[(y * width) + x] = d[y];
            }
        }

        for (var y = 0; y < height; y++)
        {
            Array.Copy(grid, y * width, f, 0, width);
            Transform1D(f, d, v, z, width);
            Array.Copy(d, 0, grid, y * width, width);
        }

        return grid;
    }

    private static void Transform1D(double[] f, double[] d, int[] v, double[] z, int n)
    {
        var k = 0;
        v[0] = 0;
        z[0] = double.NegativeInfinity;
        z[1] = double.PositiveInfinity;
        for (var q = 1; q < n; q++)
        {
            var s = Intersection(f, v[k], q);
            while (s <= z[k])
            {
                k--;
                s = Intersection(f, v[k], q);
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = double.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < n; q++)
        {
            while (z[k + 1] < q)
            {
                k++;
            }

            var delta = q - v[k];
            d[q] = (delta * delta) + f[v[k]];
        }
    }

    private static double Intersection(double[] f, int p, int q) =>
        ((f[q] + (q * q)) - (f[p] + (p * p))) / (2.0 * (q - p));
}

/// <summary>
/// Weiche Ebenen je Objekt und Art (<c>"shadow"</c>, <c>"outline"</c>) für den zuletzt verwendeten Maßstab.
/// Jede Eigenschaftsänderung des Objekts verwirft seine Ebenen; der Eintrag lebt so lange wie das Objekt.
/// </summary>
public sealed class SoftLayerCache
{
    private static readonly ConditionalWeakTable<Annotation, SoftLayerCache> Entries = new();

    private readonly Dictionary<string, (double Scale, SoftLayer? Layer)> _layers = [];

    private SoftLayerCache(Annotation annotation) => annotation.PropertyChanged += (_, _) =>
    {
        lock (_layers)
        {
            _layers.Clear();
        }
    };

    /// <summary>Anzahl gespeicherter Ebenen eines Objekts (für Tests).</summary>
    public static int CountFor(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (!Entries.TryGetValue(annotation, out var cache))
        {
            return 0;
        }

        lock (cache._layers)
        {
            return cache._layers.Count;
        }
    }

    public static SoftLayer? Get(Annotation annotation, string kind, double scale, Func<SoftLayer?> create)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(create);
        var cache = Entries.GetValue(annotation, static a => new SoftLayerCache(a));
        lock (cache._layers)
        {
            if (cache._layers.TryGetValue(kind, out var entry) && entry.Scale.Equals(scale))
            {
                return entry.Layer;
            }
        }

        var layer = create();
        lock (cache._layers)
        {
            cache._layers[kind] = (scale, layer);
        }

        return layer;
    }
}
