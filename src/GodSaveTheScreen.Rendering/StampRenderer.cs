using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Zeichnet Stempel: Rahmen und Text werden im unverdrehten Stempel-Koordinatensystem gerastert, die Abnutzung
/// (ausgefranste Kanten, Fehlstellen, Sprenkel, Kratzer, Ausreißer am Rahmen) wird in die Deckung eingerechnet und das
/// Ergebnis gedreht gezeichnet. Das Muster hängt nur von <see cref="StampAnnotation.Seed"/> und Koordinaten relativ zur
/// Schriftgröße ab – es sieht deshalb bei jeder Zoomstufe, Größe und im Export gleich aus.
/// </summary>
public static class StampRenderer
{
    private const double PixelsPerDip = 1.0;

    /// <summary>Kleinste Rasterdichte, damit gedrehte Stempel im Export (Maßstab 1) nicht weich werden.</summary>
    private const double MinRasterScale = 2;

    private static readonly ConditionalWeakTable<StampAnnotation, CacheEntry> Cache = new();

    /// <summary>Tintenfläche des Textes (für <see cref="StampAnnotation.TextSize"/>).</summary>
    public static SizeD Measure(StampAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var bounds = TextBounds(annotation, CreateText(annotation, Brushes.Black));
        return bounds.IsEmpty
            ? new SizeD(annotation.FontSize * 0.5, annotation.FontSize * 0.72)
            : new SizeD(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
    }

    /// <summary>Text zentriert, mehrzeilig, ohne Umbruch.</summary>
    public static FormattedText CreateText(StampAnnotation annotation, Brush brush)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var text = annotation.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var formatted = new FormattedText(
            text.Length == 0 ? " " : text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            TextLayout.CreateTypeface(annotation.FontFamilyName, annotation.FontWeight, annotation.FontStyle, annotation.FontStretch),
            Math.Clamp(annotation.FontSize, StampAnnotation.MinFontSize, StampAnnotation.MaxFontSize),
            brush,
            PixelsPerDip)
        {
            TextAlignment = TextAlignment.Center,
        };
        formatted.MaxTextWidth = Math.Max(1, formatted.WidthIncludingTrailingWhitespace);
        return formatted;
    }

    /// <summary>Zeichnet den Stempel (Deckkraft setzt der Aufrufer).</summary>
    public static void Draw(DrawingContext dc, StampAnnotation a, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(context);
        if (a.Color.A == 0)
        {
            return;
        }

        var layer = GetLayer(a, context);
        if (layer is null)
        {
            return;
        }

        var rect = a.Rect;
        var pushed = a.Rotation != 0;
        if (pushed)
        {
            dc.PushTransform(new RotateTransform(a.Rotation, a.Center.X, a.Center.Y));
        }

        dc.DrawImage(layer.Value.Bitmap, new Rect(rect.X + layer.Value.Offset.X, rect.Y + layer.Value.Offset.Y, layer.Value.Size.Width, layer.Value.Size.Height));
        if (pushed)
        {
            dc.Pop();
        }
    }

    /// <summary>Gerasterte Deckung (0–1) eines Stempels im Stempel-Koordinatensystem (für Tests).</summary>
    public static (float[] Coverage, int Width, int Height) RasterizeCoverage(StampAnnotation annotation, double scale)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var mask = BuildMask(annotation, scale, int.MaxValue);
        return (mask.Coverage, mask.Width, mask.Height);
    }

    private static Rect TextBounds(StampAnnotation annotation, FormattedText formatted) =>
        annotation.Text.Trim().Length == 0 ? Rect.Empty : formatted.BuildGeometry(new Point(0, 0)).Bounds;

    private static (BitmapSource Bitmap, Point Offset, Size Size)? GetLayer(StampAnnotation a, RenderContext context)
    {
        var maxPixels = Math.Max(16, context.EmojiMaxPixels);
        var scale = Math.Max(Math.Max(context.EmojiPixelScale, 0.01), MinRasterScale);
        var maskKey = MaskKey.Of(a, scale, maxPixels);
        var basePixels = a.InkBlend ? context.BasePixels : null;
        var colorKey = new ColorKey(
            maskKey,
            a.Color,
            basePixels is not null,
            basePixels is null ? default : a.Center,
            basePixels is null ? 0 : a.Rotation,
            basePixels is null ? 0 : BaseImageIdentity.Get(basePixels));

        var entry = Cache.GetValue(a, static _ => new CacheEntry());
        lock (entry)
        {
            if (entry.ColorKey == colorKey && entry.Layer is { } cached)
            {
                return cached;
            }

            if (entry.MaskKey != maskKey || entry.Mask is null)
            {
                entry.Mask = BuildMask(a, scale, maxPixels);
                entry.MaskKey = maskKey;
            }

            var mask = entry.Mask;
            if (mask.Width == 0)
            {
                entry.Layer = null;
                entry.ColorKey = colorKey;
                return null;
            }

            var bitmap = Colorize(mask, a, basePixels);
            entry.Layer = (bitmap, new Point(-mask.Pad, -mask.Pad), new Size(mask.Width / mask.Scale, mask.Height / mask.Scale));
            entry.ColorKey = colorKey;
            return entry.Layer;
        }
    }

    private static Mask BuildMask(StampAnnotation a, double scale, int maxPixels)
    {
        var metrics = a.Metrics;
        var size = metrics.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return Mask.Empty;
        }

        var pad = Math.Max(2, a.FontSize * 0.05);
        var longest = Math.Max(size.Width, size.Height) + (2 * pad);
        if (longest * scale > maxPixels)
        {
            scale = maxPixels / longest;
        }

        var width = Math.Max(1, (int)Math.Ceiling((size.Width + (2 * pad)) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((size.Height + (2 * pad)) * scale));
        var transform = new MatrixTransform(scale, 0, 0, scale, pad * scale, pad * scale);
        transform.Freeze();

        var coverage = RenderAlpha(width, height, transform, dc => DrawInk(dc, a, metrics));
        var wear = Math.Clamp(a.Wear, 0, StampAnnotation.MaxWear) / StampAnnotation.MaxWear;
        if (wear > 0)
        {
            var erase = RenderAlpha(width, height, transform, dc => DrawWearMarks(dc, a, metrics, wear));
            ApplyWear(coverage, erase, width, height, scale, pad, a.FontSize, a.Seed, wear);
        }

        return new Mask(coverage, width, height, scale, pad);
    }

    /// <summary>Rahmen und Text deckend im Stempel-Koordinatensystem (Ursprung = linke obere Außenecke).</summary>
    private static void DrawInk(DrawingContext dc, StampAnnotation a, StampMetrics m)
    {
        var w = m.Size.Width;
        var h = m.Size.Height;
        var t = Math.Max(0, a.FrameThickness);
        if (t > 0)
        {
            var pen = new Pen(Brushes.Black, t) { LineJoin = a.FrameShape == StampFrameShape.Rectangle ? PenLineJoin.Miter : PenLineJoin.Round };
            pen.Freeze();
            var inset = m.FrameInset;
            var outer = new Rect(inset, inset, Math.Max(0, w - (2 * inset)), Math.Max(0, h - (2 * inset)));
            switch (a.FrameShape)
            {
                case StampFrameShape.Oval:
                    dc.DrawEllipse(null, pen, new Point(w / 2, h / 2), outer.Width / 2, outer.Height / 2);
                    break;
                case StampFrameShape.Rectangle:
                    dc.DrawRectangle(null, pen, outer);
                    break;
                default:
                    dc.DrawRoundedRectangle(null, pen, outer, m.CornerRadius, m.CornerRadius);
                    break;
            }

            if (m.InnerThickness > 0)
            {
                var innerPen = new Pen(Brushes.Black, m.InnerThickness) { LineJoin = PenLineJoin.Round };
                innerPen.Freeze();
                var i = m.InnerInset;
                var inner = new Rect(i, i, Math.Max(0, w - (2 * i)), Math.Max(0, h - (2 * i)));
                var radius = Math.Max(0, m.CornerRadius - (i - inset));
                dc.DrawRoundedRectangle(null, innerPen, inner, radius, radius);
            }
        }

        if (a.Text.Trim().Length == 0)
        {
            return;
        }

        var formatted = CreateText(a, Brushes.Black);
        var bounds = TextBounds(a, formatted);
        if (bounds.IsEmpty)
        {
            return;
        }

        // Die Tintenfläche der Zeichen (nicht die Zeilenhöhe) sitzt in der Mitte – Großbuchstaben wirken so zentriert.
        var origin = new Point((w / 2) - (bounds.X + (bounds.Width / 2)), (h / 2) - (bounds.Y + (bounds.Height / 2)));
        dc.DrawText(formatted, origin);
    }

    /// <summary>Kratzer quer über den Stempel und Ausreißer am Rahmen (deckend = hier fehlt Farbe).</summary>
    private static void DrawWearMarks(DrawingContext dc, StampAnnotation a, StampMetrics m, double wear)
    {
        var random = new Random(a.Seed);
        var w = m.Size.Width;
        var h = m.Size.Height;
        var unit = Math.Max(1, a.FontSize);

        var scratches = (int)Math.Round(wear * 7);
        for (var s = 0; s < scratches; s++)
        {
            var start = new Point(random.NextDouble() * w, random.NextDouble() * h);
            var angle = (random.NextDouble() - 0.5) * Math.PI;
            var length = unit * (0.6 + (random.NextDouble() * 2.2));
            var thickness = unit * (0.015 + (random.NextDouble() * 0.035)) * (0.6 + (0.6 * wear));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, isFilled: false, isClosed: false);
                var segments = 4;
                var point = start;
                for (var i = 1; i <= segments; i++)
                {
                    var wobble = (random.NextDouble() - 0.5) * 0.25;
                    var step = length / segments;
                    point = new Point(point.X + (Math.Cos(angle + wobble) * step), point.Y + (Math.Sin(angle + wobble) * step));
                    ctx.LineTo(point, isStroked: true, isSmoothJoin: true);
                }
            }

            geometry.Freeze();
            var pen = new Pen(Brushes.Black, thickness) { StartLineCap = PenLineCap.Triangle, EndLineCap = PenLineCap.Triangle, LineJoin = PenLineJoin.Round };
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }

        if (a.FrameThickness <= 0)
        {
            return;
        }

        // Ausreißer: Stücke am Rahmen, die beim Abdruck fehlen.
        var chips = (int)Math.Round(wear * 12);
        var inset = m.FrameInset;
        var rx = (w / 2) - inset;
        var ry = (h / 2) - inset;
        for (var c = 0; c < chips; c++)
        {
            Point center;
            if (a.FrameShape == StampFrameShape.Oval)
            {
                var phi = random.NextDouble() * Math.PI * 2;
                center = new Point((w / 2) + (Math.Cos(phi) * rx), (h / 2) + (Math.Sin(phi) * ry));
            }
            else
            {
                // Gleichmäßig entlang der Mittellinie: oben, unten, links, rechts.
                var pos = random.NextDouble() * 4 * (rx + ry);
                center = pos switch
                {
                    _ when pos < 2 * rx => new Point(inset + pos, inset),
                    _ when pos < 4 * rx => new Point(inset + pos - (2 * rx), h - inset),
                    _ when pos < (4 * rx) + (2 * ry) => new Point(inset, inset + pos - (4 * rx)),
                    _ => new Point(w - inset, inset + pos - (4 * rx) - (2 * ry)),
                };
            }

            var size = Math.Max(a.FrameThickness, unit * 0.06) * (0.5 + (random.NextDouble() * 1.1)) * (0.6 + (0.5 * wear));
            var stretch = 0.5 + random.NextDouble();
            dc.PushTransform(new RotateTransform(random.NextDouble() * 180, center.X, center.Y));
            dc.DrawEllipse(Brushes.Black, null, center, size * stretch, size / stretch);
            dc.Pop();
        }
    }

    private static float[] RenderAlpha(int width, int height, Transform transform, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(transform);
            draw(dc);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[width * height * 4];
        target.CopyPixels(pixels, width * 4, 0);
        var alpha = new float[width * height];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = pixels[(i * 4) + 3] / 255f;
        }

        return alpha;
    }

    /// <summary>
    /// Rechnet die Abnutzung in die Deckung ein: ausgefranste Kanten (weichgezeichnete Deckung mit Rauschen neu
    /// geschwellt), großflächige Fehlstellen, feine Sprenkel, ungleichmäßige Farbdichte, Kratzer und Ausreißer.
    /// Rauschkoordinaten sind relativ zur Schriftgröße, damit das Muster maßstabsunabhängig ist.
    /// </summary>
    private static void ApplyWear(float[] coverage, float[] erase, int width, int height, double scale, double pad, double fontSize, int seed, double wear)
    {
        var unit = Math.Max(1, fontSize);
        var blurRadius = (int)Math.Round(unit * 0.03 * wear * scale);
        float[] blurred;
        if (blurRadius > 0)
        {
            blurred = (float[])coverage.Clone();
            RegionEffectProcessor.BlurChannel(blurred, width, height, blurRadius);
        }
        else
        {
            blurred = coverage;
        }

        // Breite des Übergangs: etwa ein Pixel an der Kante der weichgezeichneten Deckung.
        var soft = (float)Math.Max(0.06, 0.5 / (blurRadius + 1));
        var edgeAmount = (float)(0.85 * wear);
        var voidThreshold = (float)(wear * 0.36);
        var speckThreshold = (float)(wear * 0.2);
        var density = (float)(wear * 0.45);
        var grain = (float)(wear * 0.3);

        Parallel.For(0, height, y =>
        {
            var v = (((y + 0.5) / scale) - pad) / unit;
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                if (blurred[i] <= 0.001f && coverage[i] <= 0.001f)
                {
                    coverage[i] = 0;
                    continue;
                }

                var u = (((x + 0.5) / scale) - pad) / unit;
                var edge = Noise.Fbm(u * 7, v * 7, seed, 2);
                var value = blurred[i] + ((edge - 0.5f) * edgeAmount);
                var c = blurRadius > 0 ? SmoothStep(0.5f - soft, 0.5f + soft, value) : Math.Clamp(coverage[i] + ((edge - 0.5f) * edgeAmount * 0.5f), 0, 1);

                var voids = Noise.Fbm((u * 2.2) + 17.3, (v * 2.2) - 4.1, seed ^ 0x5bd1e995, 3);
                c *= SmoothStep(voidThreshold - 0.05f, voidThreshold + 0.05f, voids);

                var speck = Noise.Value((u * 16) + 3.7, (v * 16) + 9.2, seed ^ 0x27d4eb2d);
                c *= SmoothStep(speckThreshold - 0.04f, speckThreshold + 0.04f, speck);

                var low = Noise.Fbm((u * 0.7) - 11.0, (v * 0.7) + 5.0, seed ^ 0x165667b1, 2);
                c *= 1 - (density * low);

                var fine = Noise.Value((u * 40) + 1.3, (v * 40) - 2.9, seed ^ 0x61c88647);
                c *= 1 - (grain * fine);

                c *= 1 - erase[i];
                coverage[i] = Math.Clamp(c, 0, 1);
            }
        });
    }

    private static BitmapSource Colorize(Mask mask, StampAnnotation a, PixelBuffer? basePixels)
    {
        var width = mask.Width;
        var height = mask.Height;
        var pixels = new byte[width * height * 4];
        var color = a.Color;
        var alphaScale = color.A;

        // Tinten-Look: Lage jedes Rasterpunkts im Bild (Drehung um die Mitte), dort Farbe × Bildfarbe.
        var rect = a.Rect;
        var radians = a.Rotation * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var center = a.Center;

        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var o = i * 4;
                var cov = mask.Coverage[i];
                if (cov <= 0)
                {
                    continue;
                }

                int r = color.R, g = color.G, b = color.B;
                if (basePixels is not null)
                {
                    var lx = rect.X + ((x + 0.5) / mask.Scale) - mask.Pad - center.X;
                    var ly = rect.Y + ((y + 0.5) / mask.Scale) - mask.Pad - center.Y;
                    var px = (int)Math.Floor(center.X + (lx * cos) - (ly * sin));
                    var py = (int)Math.Floor(center.Y + (lx * sin) + (ly * cos));
                    if (px >= 0 && py >= 0 && px < basePixels.Width && py < basePixels.Height)
                    {
                        var bo = ((py * basePixels.Width) + px) * 4;
                        var src = basePixels.Pixels;
                        var ba = src[bo + 3];

                        // Bildfarbe über Weiß (transparente Bereiche wirken wie Papier).
                        var bb = ((src[bo] * ba) + (255 * (255 - ba))) / 255;
                        var bg = ((src[bo + 1] * ba) + (255 * (255 - ba))) / 255;
                        var br = ((src[bo + 2] * ba) + (255 * (255 - ba))) / 255;
                        r = r * br / 255;
                        g = g * bg / 255;
                        b = b * bb / 255;
                    }
                }

                pixels[o] = (byte)b;
                pixels[o + 1] = (byte)g;
                pixels[o + 2] = (byte)r;
                pixels[o + 3] = (byte)Math.Clamp((int)Math.Round(cov * alphaScale), 0, 255);
            }
        });

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3 - (2 * t));
    }

    private sealed record Mask(float[] Coverage, int Width, int Height, double Scale, double Pad)
    {
        public static Mask Empty { get; } = new([], 0, 0, 1, 0);
    }

    private readonly record struct MaskKey(
        string Text, string Font, int Weight, FontStyleKind Style, int Stretch, double FontSize,
        StampFrameShape Shape, double FrameThickness, double Wear, int Seed, double Scale, int MaxPixels)
    {
        public static MaskKey Of(StampAnnotation a, double scale, int maxPixels) => new(
            a.Text, a.FontFamilyName, a.FontWeight, a.FontStyle, a.FontStretch, a.FontSize,
            a.FrameShape, a.FrameThickness, a.Wear, a.Seed, scale, maxPixels);
    }

    private readonly record struct ColorKey(MaskKey Mask, ArgbColor Color, bool Multiply, PointD Center, double Rotation, long BaseId);

    private sealed class CacheEntry
    {
        public MaskKey MaskKey { get; set; }

        public Mask? Mask { get; set; }

        public ColorKey ColorKey { get; set; }

        public (BitmapSource Bitmap, Point Offset, Size Size)? Layer { get; set; }
    }

    /// <summary>Deterministisches Wert-Rauschen (0–1) mit glatter Interpolation.</summary>
    private static class Noise
    {
        public static float Value(double x, double y, int seed)
        {
            var xf = Math.Floor(x);
            var yf = Math.Floor(y);
            var xi = (int)xf;
            var yi = (int)yf;
            var tx = (float)(x - xf);
            var ty = (float)(y - yf);
            tx = tx * tx * (3 - (2 * tx));
            ty = ty * ty * (3 - (2 * ty));
            var a = Hash(xi, yi, seed);
            var b = Hash(xi + 1, yi, seed);
            var c = Hash(xi, yi + 1, seed);
            var d = Hash(xi + 1, yi + 1, seed);
            var top = a + ((b - a) * tx);
            var bottom = c + ((d - c) * tx);
            return top + ((bottom - top) * ty);
        }

        public static float Fbm(double x, double y, int seed, int octaves)
        {
            float sum = 0;
            float amplitude = 0.5f;
            float total = 0;
            for (var o = 0; o < octaves; o++)
            {
                sum += Value(x, y, seed + (o * 1013)) * amplitude;
                total += amplitude;
                x = (x * 2.03) + 0.37;
                y = (y * 2.03) - 0.61;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)((x * 374761393) + (y * 668265263) + (seed * 1442695041));
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
