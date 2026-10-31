using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using GodSaveTheScreen.Core.Services;

namespace GodSaveTheScreen.App.Editor;

/// <summary>
/// Bildquelle der Texterkennung im Editor: das sichtbare Ergebnis ohne Rahmen (<c>RenderContent</c>, also mit Objekten und
/// ohne entfernte Bereiche; durch Unschärfe verdeckter Text wird nicht erkannt, Textobjekte werden erkannt), bei vorhandener
/// Bereichsauswahl nur deren Umgebungsrechteck – alles außerhalb der Deckung wird weiß gefüllt.
/// </summary>
public static class OcrSource
{
    /// <summary>Das Bild für die Erkennung; immer deckend (transparente Stellen sind weiß).</summary>
    public static PixelBuffer Prepare(ImageDocument document, IDocumentExporter exporter, AreaSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(exporter);
        var content = exporter.RenderContent(document);
        var bounds = document.EffectiveBounds;
        if (selection is null || selection.IsEmpty)
        {
            return OcrImagePrep.FlattenOnWhite(content);
        }

        // Auswahl in Pixeln des Basisbilds; der Inhalt beginnt bei bounds.X/Y (kann negativ sein).
        var box = selection.Bounds.Intersect(bounds);
        if (box.IsEmpty)
        {
            return OcrImagePrep.FlattenOnWhite(content);
        }

        var cropped = OcrImagePrep.FlattenOnWhite(content.Crop(new PixelRect(box.X - bounds.X, box.Y - bounds.Y, box.Width, box.Height)));
        var pixels = cropped.Pixels;
        for (var row = 0; row < box.Height; row++)
        {
            for (var column = 0; column < box.Width; column++)
            {
                var x = box.X + column;
                var y = box.Y + row;
                var coverage = x >= 0 && y >= 0 && x < selection.Width && y < selection.Height ? selection.Values[(y * selection.Width) + x] : (byte)0;
                if (coverage == 255)
                {
                    continue;
                }

                var o = ((row * box.Width) + column) * PixelBuffer.BytesPerPixel;
                for (var channel = 0; channel < 3; channel++)
                {
                    pixels[o + channel] = (byte)(((pixels[o + channel] * coverage) + (255 * (255 - coverage)) + 127) / 255);
                }
            }
        }

        return cropped;
    }
}
