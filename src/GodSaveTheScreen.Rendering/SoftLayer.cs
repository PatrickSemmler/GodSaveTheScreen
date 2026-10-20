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
