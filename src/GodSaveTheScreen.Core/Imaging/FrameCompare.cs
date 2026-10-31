namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Vergleich zweier gleich großer Bilder mit Toleranz (rein, ohne Bildschirm).</summary>
public static class FrameCompare
{
    /// <summary>
    /// Wahr, wenn mindestens <paramref name="share"/> der geprüften Pixel (jede zweite Zeile, jedes vierte Pixel) bis auf
    /// <paramref name="tolerance"/> je Farbkanal gleich sind. Bilder unterschiedlicher Größe sind nie gleich.
    /// </summary>
    public static bool NearlyEqual(PixelBuffer a, PixelBuffer b, int tolerance = 10, double share = 0.998)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        long same = 0;
        long total = 0;
        for (var row = 0; row < a.Height; row += 2)
        {
            for (var x = 0; x < a.Width; x += 4, total++)
            {
                var i = (row * a.Stride) + (x * PixelBuffer.BytesPerPixel);
                if (Math.Abs(a.Pixels[i] - b.Pixels[i]) <= tolerance
                    && Math.Abs(a.Pixels[i + 1] - b.Pixels[i + 1]) <= tolerance
                    && Math.Abs(a.Pixels[i + 2] - b.Pixels[i + 2]) <= tolerance)
                {
                    same++;
                }
            }
        }

        return total == 0 || same >= total * share;
    }
}
