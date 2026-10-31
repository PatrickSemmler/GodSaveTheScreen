using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Ocr;

/// <summary>Ein senkrechter Streifen des Bilds (Pixelzeilen <see cref="Top"/> bis <see cref="Bottom"/>).</summary>
public readonly record struct OcrTile(int Top, int Height)
{
    public int Bottom => Top + Height;
}

/// <summary>
/// Teilt lange Bilder für die Erkennung in überlappende Streifen (die Engine verarbeitet höchstens 10.000 Pixel je Seite)
/// und fügt die Ergebnisse wieder zusammen (rein, ohne Windows): Zeilen im Überlappungsbereich werden nur einmal übernommen.
/// </summary>
public static class OcrTiling
{
    /// <summary>Größte Seitenlänge, die die Erkennung verarbeitet (<c>OcrEngine.MaxImageDimension</c>).</summary>
    public const int MaxSide = 10000;

    /// <summary>Überlappung zweier Streifen in Pixeln (mehrere Zeilen, damit eine Zeile am Schnitt in einem Streifen ganz liegt).</summary>
    public const int DefaultOverlap = 400;

    /// <summary>Streifen für ein Bild der Höhe <paramref name="height"/>; ein Streifen, wenn es in <paramref name="maxSide"/> passt.</summary>
    public static IReadOnlyList<OcrTile> Plan(int height, int maxSide = MaxSide, int overlap = DefaultOverlap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSide, 2);
        overlap = Math.Clamp(overlap, 0, (maxSide / 2) - 1);
        if (height <= maxSide)
        {
            return [new OcrTile(0, height)];
        }

        var tiles = new List<OcrTile>();
        var step = maxSide - overlap;
        for (var top = 0; ; top += step)
        {
            var bottom = Math.Min(height, top + maxSide);
            tiles.Add(new OcrTile(top, bottom - top));
            if (bottom >= height)
            {
                break;
            }
        }

        return tiles;
    }

    /// <summary>
    /// Fügt die Zeilen der Streifen (Koordinaten je Streifen, <paramref name="linesPerTile"/> in Reihenfolge von
    /// <paramref name="tiles"/>) zu Zeilen in Bildkoordinaten zusammen. Am Schnitt zwischen zwei Streifen – in der Mitte ihrer
    /// Überlappung – gilt jede Zeile nach ihrer Mitte genau einem Streifen.
    /// </summary>
    public static IReadOnlyList<OcrLine> Merge(IReadOnlyList<OcrTile> tiles, IReadOnlyList<IReadOnlyList<OcrLine>> linesPerTile)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(linesPerTile);
        if (tiles.Count != linesPerTile.Count)
        {
            throw new ArgumentException("Zu jedem Streifen gehört eine Zeilenliste.", nameof(linesPerTile));
        }

        var result = new List<OcrLine>();
        for (var i = 0; i < tiles.Count; i++)
        {
            // Der Streifen gilt für Mitten zwischen dem Schnitt zum Vorgänger und dem Schnitt zum Nachfolger.
            var from = i == 0 ? double.NegativeInfinity : Cut(tiles[i - 1], tiles[i]);
            var to = i == tiles.Count - 1 ? double.PositiveInfinity : Cut(tiles[i], tiles[i + 1]);
            foreach (var line in linesPerTile[i])
            {
                var global = Shift(line, tiles[i].Top);
                var center = global.Bounds.Y + (global.Bounds.Height / 2);
                if (center >= from && center < to)
                {
                    result.Add(global);
                }
            }
        }

        return result;
    }

    /// <summary>Schnitt zwischen zwei Streifen: Mitte ihres Überlappungsbereichs (Bildkoordinate).</summary>
    public static double Cut(OcrTile upper, OcrTile lower) => (lower.Top + upper.Bottom) / 2.0;

    /// <summary>Verschiebt eine Zeile (samt Wörtern) um <paramref name="dy"/> nach unten.</summary>
    public static OcrLine Shift(OcrLine line, double dy)
    {
        ArgumentNullException.ThrowIfNull(line);
        return dy == 0
            ? line
            : new OcrLine(line.Text, Move(line.Bounds, dy), [.. line.Words.Select(w => w with { Bounds = Move(w.Bounds, dy) })]);
    }

    private static RectD Move(RectD rect, double dy) => new(rect.X, rect.Y + dy, rect.Width, rect.Height);
}
