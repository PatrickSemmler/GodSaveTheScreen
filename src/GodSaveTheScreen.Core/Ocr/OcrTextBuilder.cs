using System.Text;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Ocr;

/// <summary>
/// Setzt den erkannten Text aus Zeilen zusammen (rein, ohne Windows): Reihenfolge von oben nach unten, Leerzeile bei
/// großem Zeilenabstand und auf Wunsch Tabulatoren zwischen Spalten.
/// </summary>
public static class OcrTextBuilder
{
    /// <summary>Zeilenabstand (Lücke zwischen zwei Zeilen) als Vielfaches der Zeilenhöhe, ab dem eine Leerzeile folgt.</summary>
    public const double ParagraphGap = 0.75;

    /// <summary>Wortabstand als Vielfaches der Zeilenhöhe, ab dem bei „Spalten mit Tabulator“ ein Tabulator steht.</summary>
    public const double ColumnGap = 1.5;

    /// <summary>
    /// Baut den Text. Ohne <paramref name="tabSeparatedColumns"/> bleibt jede erkannte Zeile eine Zeile (Zeilen auf gleicher
    /// Höhe von links nach rechts). Mit Tabulatoren werden Zeilen auf gleicher Höhe zu einer Zeile zusammengefasst und
    /// durch Tabulatoren getrennt, wenn der Abstand groß ist (Tabellen, Dateilisten), sonst durch ein Leerzeichen.
    /// </summary>
    public static string Build(IReadOnlyList<OcrLine> lines, bool tabSeparatedColumns = false)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var visible = lines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
        if (visible.Count == 0)
        {
            return string.Empty;
        }

        var typical = TypicalHeight(visible);
        var rows = GroupRows(visible);
        var builder = new StringBuilder();
        RectD? previous = null;
        foreach (var row in rows)
        {
            var bounds = Union(row);
            if (previous is { } above && bounds.Y - above.Bottom > ParagraphGap * typical)
            {
                builder.Append('\n');
            }

            previous = bounds;
            if (tabSeparatedColumns)
            {
                builder.Append(JoinRow(row, typical)).Append('\n');
            }
            else
            {
                foreach (var line in row)
                {
                    builder.Append(line.Text.Trim()).Append('\n');
                }
            }
        }

        return builder.ToString().TrimEnd('\n');
    }

    /// <summary>Mittlere Höhe (Median) der Zeilen; Grundlage der Abstandsgrenzen.</summary>
    public static double TypicalHeight(IReadOnlyList<OcrLine> lines)
    {
        var heights = lines.Select(l => l.Bounds.Height).Where(h => h > 0).Order().ToList();
        return heights.Count == 0 ? 1 : heights[heights.Count / 2];
    }

    /// <summary>Fasst Zeilen auf gleicher Höhe (mindestens halbe Überdeckung) zu Reihen zusammen; Reihen von oben nach unten, Zeilen links nach rechts.</summary>
    public static List<List<OcrLine>> GroupRows(IReadOnlyList<OcrLine> lines)
    {
        var ordered = lines.OrderBy(l => l.Bounds.Y + (l.Bounds.Height / 2)).ThenBy(l => l.Bounds.X).ToList();
        var rows = new List<List<OcrLine>>();
        RectD current = default;
        foreach (var line in ordered)
        {
            if (rows.Count > 0 && Overlap(current, line.Bounds) >= 0.5 * Math.Min(current.Height, line.Bounds.Height))
            {
                rows[^1].Add(line);
                current = Union(current, line.Bounds);
            }
            else
            {
                rows.Add([line]);
                current = line.Bounds;
            }
        }

        foreach (var row in rows)
        {
            row.Sort((a, b) => a.Bounds.X.CompareTo(b.Bounds.X));
        }

        return rows;
    }

    private static string JoinRow(List<OcrLine> row, double typical)
    {
        var builder = new StringBuilder();
        RectD? last = null;
        foreach (var line in row)
        {
            var words = line.Words.Count > 0 ? line.Words : [new OcrWord(line.Text.Trim(), line.Bounds)];
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                if (last is { } previous)
                {
                    builder.Append(word.Bounds.X - previous.Right > ColumnGap * typical ? '\t' : ' ');
                }

                builder.Append(word.Text.Trim());
                last = word.Bounds;
            }
        }

        return builder.ToString();
    }

    private static double Overlap(RectD a, RectD b) => Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);

    private static RectD Union(IEnumerable<OcrLine> row) => row.Select(l => l.Bounds).Aggregate(Union);

    private static RectD Union(RectD a, RectD b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new RectD(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
    }
}
