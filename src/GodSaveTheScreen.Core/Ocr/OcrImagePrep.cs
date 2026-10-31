using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Ocr;

/// <summary>
/// Aufbereitung des Bilds für die Erkennung (rein, ohne WPF): transparente Pixel auf Weiß verrechnen, hochwertig
/// skalieren (Catmull-Rom; beim Verkleinern mit angepasster Filterbreite) und die Skalierungsregel für kleine Schrift.
/// </summary>
public static class OcrImagePrep
{
    /// <summary>Ist die mittlere Wortrechteck-Höhe kleiner, folgt ein zweiter Lauf mit <see cref="ScaleFactor"/>.</summary>
    public const double SmallTextHeight = 10;

    /// <summary>Unter dieser mittleren Höhe (Schrift von etwa 8 bis 10 px) folgt ein zweiter Lauf mit <see cref="TinyScaleFactor"/>.</summary>
    public const double TinyTextHeight = 7.5;

    /// <summary>Vergrößerung des zweiten Laufs bei kleiner Schrift.</summary>
    public const int ScaleFactor = 2;

    /// <summary>Vergrößerung des zweiten Laufs bei sehr kleiner Schrift.</summary>
    public const int TinyScaleFactor = 3;

    /// <summary>Kopie, in der alle Pixel deckend sind: Transparenz wird auf Weiß verrechnet (sonst wären transparente Pixel schwarz).</summary>
    public static PixelBuffer FlattenOnWhite(PixelBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new PixelBuffer(source.Width, source.Height);
        var from = source.Pixels;
        var to = result.Pixels;
        var count = source.Width * source.Height;
        for (var i = 0; i < count; i++)
        {
            var o = i * 4;
            var a = from[o + 3];
            if (a == 255)
            {
                to[o] = from[o];
                to[o + 1] = from[o + 1];
                to[o + 2] = from[o + 2];
            }
            else
            {
                var inverse = 255 - a;
                to[o] = (byte)(((from[o] * a) + (255 * inverse) + 127) / 255);
                to[o + 1] = (byte)(((from[o + 1] * a) + (255 * inverse) + 127) / 255);
                to[o + 2] = (byte)(((from[o + 2] * a) + (255 * inverse) + 127) / 255);
            }

            to[o + 3] = 255;
        }

        return result;
    }

    /// <summary>Skaliert auf die Zielgröße (Catmull-Rom, getrennt waagerecht und senkrecht). Das Bild muss deckend sein.</summary>
    public static PixelBuffer Resize(PixelBuffer source, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (width == source.Width && height == source.Height)
        {
            return source.Clone();
        }

        // Waagerecht: source.Width -> width (Zeilen bleiben), danach senkrecht.
        var horizontal = ResizeAxis(source.Pixels, source.Width, source.Height, width, horizontalAxis: true);
        var vertical = ResizeAxis(horizontal, width, source.Height, height, horizontalAxis: false);
        return new PixelBuffer(width, height, vertical);
    }

    /// <summary>Wortrechtecke eines skalierten Laufs zurück in die Koordinaten des Originals (Teilen durch <paramref name="scaleX"/>/<paramref name="scaleY"/>).</summary>
    public static OcrLine Unscale(OcrLine line, double scaleX, double scaleY)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new OcrLine(line.Text, Unscale(line.Bounds, scaleX, scaleY), [.. line.Words.Select(w => w with { Bounds = Unscale(w.Bounds, scaleX, scaleY) })]);
    }

    /// <summary>Mittlere Höhe der Wortrechtecke (0 ohne Wörter).</summary>
    public static double MeanWordHeight(IReadOnlyList<OcrLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        double sum = 0;
        var count = 0;
        foreach (var word in lines.SelectMany(l => l.Words))
        {
            if (!string.IsNullOrWhiteSpace(word.Text))
            {
                sum += word.Bounds.Height;
                count++;
            }
        }

        return count == 0 ? 0 : sum / count;
    }

    /// <summary>Die Schrift ist so klein, dass ein vergrößerter zweiter Lauf lohnt.</summary>
    public static bool ShouldRetryScaled(IReadOnlyList<OcrLine> lines) => MeanWordHeight(lines) is > 0 and < SmallTextHeight;

    /// <summary>
    /// Vergrößerung des zweiten Laufs nach dem ersten Ergebnis: 3 bei sehr kleiner Schrift (mittlere Wortrechteck-Höhe unter
    /// <see cref="TinyTextHeight"/>), 2 bis <see cref="SmallTextHeight"/>, sonst 0 (kein zweiter Lauf). Gemessen an Segoe UI,
    /// Consolas und Arial von 8 bis 16 px (hell und dunkel): ab 13 px genügt der erste Lauf, darunter hilft 2×, unter etwa 10 px 3×.
    /// </summary>
    public static int RetryFactor(IReadOnlyList<OcrLine> lines)
    {
        var height = MeanWordHeight(lines);
        if (height <= 0 || height >= SmallTextHeight)
        {
            return 0;
        }

        return height < TinyTextHeight ? TinyScaleFactor : ScaleFactor;
    }

    /// <summary>Anzahl sichtbarer Zeichen (Maß für „mehr erkannt“).</summary>
    public static int CountCharacters(IReadOnlyList<OcrLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Sum(l => l.Text.Count(c => !char.IsWhiteSpace(c)));
    }

    /// <summary>Der vergrößerte Lauf ersetzt den ersten nur, wenn er mindestens so viele Zeichen liefert (Gleichstand: das Original).</summary>
    public static bool PreferScaled(IReadOnlyList<OcrLine> original, IReadOnlyList<OcrLine> scaled) =>
        CountCharacters(scaled) > CountCharacters(original);

    private static RectD Unscale(RectD rect, double scaleX, double scaleY) =>
        new(rect.X / scaleX, rect.Y / scaleY, rect.Width / scaleX, rect.Height / scaleY);

    private static byte[] ResizeAxis(byte[] source, int sourceWidth, int sourceHeight, int target, bool horizontalAxis)
    {
        var inLength = horizontalAxis ? sourceWidth : sourceHeight;
        var weights = Weights(inLength, target);
        var outWidth = horizontalAxis ? target : sourceWidth;
        var outHeight = horizontalAxis ? sourceHeight : target;
        var result = new byte[outWidth * outHeight * 4];
        var lines = horizontalAxis ? sourceHeight : target;
        Parallel.For(0, lines, line =>
        {
            for (var pos = 0; pos < (horizontalAxis ? target : sourceWidth); pos++)
            {
                // horizontal: line = Zeile, pos = Zielspalte; vertikal: line = Zielzeile, pos = Spalte.
                var (start, w) = horizontalAxis ? weights[pos] : weights[line];
                double b = 0, g = 0, r = 0;
                for (var k = 0; k < w.Length; k++)
                {
                    var index = horizontalAxis
                        ? ((line * sourceWidth) + start + k) * 4
                        : (((start + k) * sourceWidth) + pos) * 4;
                    b += source[index] * w[k];
                    g += source[index + 1] * w[k];
                    r += source[index + 2] * w[k];
                }

                var o = horizontalAxis ? ((line * target) + pos) * 4 : ((line * sourceWidth) + pos) * 4;
                result[o] = Clamp(b);
                result[o + 1] = Clamp(g);
                result[o + 2] = Clamp(r);
                result[o + 3] = 255;
            }
        });
        return result;
    }

    private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    /// <summary>Filtergewichte je Zielposition (Catmull-Rom; beim Verkleinern ist der Filter um das Verkleinerungsverhältnis breiter).</summary>
    private static (int Start, double[] Weights)[] Weights(int inLength, int outLength)
    {
        var scale = (double)outLength / inLength;
        var filterScale = Math.Max(1, 1 / scale);
        var support = 2 * filterScale;
        var result = new (int, double[])[outLength];
        for (var i = 0; i < outLength; i++)
        {
            var center = (i + 0.5) / scale;
            var first = Math.Max(0, (int)Math.Floor(center - support + 0.5));
            var last = Math.Min(inLength - 1, (int)Math.Floor(center + support + 0.5) - 1);
            if (last < first)
            {
                last = first;
            }

            var weights = new double[last - first + 1];
            double total = 0;
            for (var x = first; x <= last; x++)
            {
                var w = CatmullRom((x + 0.5 - center) / filterScale);
                weights[x - first] = w;
                total += w;
            }

            if (Math.Abs(total) < 1e-12)
            {
                weights = [1];
                first = Math.Clamp((int)center, 0, inLength - 1);
            }
            else
            {
                for (var k = 0; k < weights.Length; k++)
                {
                    weights[k] /= total;
                }
            }

            result[i] = (first, weights);
        }

        return result;
    }

    private static double CatmullRom(double x)
    {
        x = Math.Abs(x);
        if (x < 1)
        {
            return (1.5 * x * x * x) - (2.5 * x * x) + 1;
        }

        return x < 2 ? (-0.5 * x * x * x) + (2.5 * x * x) - (4 * x) + 2 : 0;
    }
}
