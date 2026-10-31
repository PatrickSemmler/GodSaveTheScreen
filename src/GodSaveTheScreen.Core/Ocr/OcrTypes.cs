using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Ocr;

/// <summary>Sprache der Texterkennung: BCP-47-Kennung (z. B. <c>de-DE</c>) und Anzeigename.</summary>
public sealed record OcrLanguage(string Tag, string DisplayName);

/// <summary>Ein erkanntes Wort mit seinem Rechteck in Pixeln des übergebenen Bilds.</summary>
public sealed record OcrWord(string Text, RectD Bounds);

/// <summary>Eine erkannte Zeile: Text der Erkennung, Umriss und Wörter (Koordinaten in Pixeln des übergebenen Bilds).</summary>
public sealed record OcrLine(string Text, RectD Bounds, IReadOnlyList<OcrWord> Words);

/// <summary>Ergebnis einer Texterkennung. <see cref="LanguageTag"/> ist die tatsächlich verwendete Sprache.</summary>
public sealed record OcrResult(string? LanguageTag, IReadOnlyList<OcrLine> Lines)
{
    /// <summary>Kein einziges sichtbares Zeichen erkannt.</summary>
    public bool IsEmpty => Lines.All(l => string.IsNullOrWhiteSpace(l.Text));
}

/// <summary>Es ist keine (passende) Erkennungssprache installiert.</summary>
public sealed class OcrUnavailableException : Exception
{
    public OcrUnavailableException()
        : base("Es ist keine Sprache für die Texterkennung installiert.")
    {
    }

    public OcrUnavailableException(string message)
        : base(message)
    {
    }

    public OcrUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Lokale Texterkennung. Die Koordinaten des Ergebnisses beziehen sich auf das übergebene Bild.</summary>
public interface IOcrService
{
    /// <summary>Installierte Erkennungssprachen (leer, wenn keine installiert ist).</summary>
    IReadOnlyList<OcrLanguage> AvailableLanguages { get; }

    /// <summary>
    /// Erkennt den Text. <paramref name="languageTag"/> <c>null</c> oder leer = automatisch (Profilsprachen von Windows);
    /// eine nicht (mehr) installierte Sprache fällt ebenfalls auf die Automatik zurück.
    /// </summary>
    /// <exception cref="OcrUnavailableException">Keine Sprache installiert.</exception>
    Task<OcrResult> RecognizeAsync(PixelBuffer image, string? languageTag, CancellationToken cancellationToken = default);
}
