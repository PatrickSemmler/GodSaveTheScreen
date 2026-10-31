namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Umrechnung in Graustufen (Rec. 709 auf den Werten, Alpha bleibt).</summary>
public static class Grayscale
{
    // Gewichte × 65536 (Summe genau 65536, damit Weiß Weiß bleibt).
    private const int WeightRed = 13933;
    private const int WeightGreen = 46871;
    private const int WeightBlue = 4732;

    /// <summary>Wandelt BGRA-Pixel (4 Byte je Pixel) im Puffer um.</summary>
    public static void Apply(Span<byte> bgra)
    {
        for (var i = 0; i + 3 < bgra.Length; i += PixelBuffer.BytesPerPixel)
        {
            var gray = (byte)(((WeightRed * bgra[i + 2]) + (WeightGreen * bgra[i + 1]) + (WeightBlue * bgra[i]) + 32768) >> 16);
            bgra[i] = gray;
            bgra[i + 1] = gray;
            bgra[i + 2] = gray;
        }
    }

    /// <summary>Graustufenkopie des Puffers; das Original bleibt unverändert.</summary>
    public static PixelBuffer ToGrayscale(this PixelBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = source.Clone();
        Apply(result.Pixels.AsSpan(0, result.Width * result.Height * PixelBuffer.BytesPerPixel));
        return result;
    }
}
