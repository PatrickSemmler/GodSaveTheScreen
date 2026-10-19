namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Erkennt Bilder, die beim Bearbeiten sehr viel Speicher brauchen (Warnung vor dem Öffnen).</summary>
public static class LargeImageGuard
{
    /// <summary>Ab dieser Pixelzahl wird gewarnt (≈ 10 000 × 10 000; ein 16 384 × 16 384-Bild hat 268 Mio. und belegt etwa 1 GB).</summary>
    public const long WarningPixels = 100_000_000;

    /// <summary>Benötigter Arbeitsspeicher für das Bild selbst in Byte (BGRA32); beim Rendern wird er mehrfach gebraucht.</summary>
    public static long BytesFor(int width, int height) => (long)width * height * 4;

    public static bool IsLarge(int width, int height) => (long)width * height > WarningPixels;
}
