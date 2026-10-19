using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace GodSaveTheScreen.Core.Emoji;

/// <summary>
/// Suche im Katalog: deutsch und englisch zugleich, Wortpräfixe, mehrere Suchwörter mit UND.
/// Rangfolge: exakter Name &gt; Namenspräfix &gt; Stichwort &gt; Synonym &gt; Teiltreffer, bei Gleichstand Unicode-Reihenfolge.
/// </summary>
public sealed class EmojiSearch
{
    private readonly EmojiCatalog _catalog;
    private readonly Lazy<ImmutableArray<Entry>> _entries;

    public EmojiSearch(EmojiCatalog catalog)
    {
        _catalog = catalog;
        _entries = new Lazy<ImmutableArray<Entry>>(BuildEntries, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Kleinschreibung, Bindestriche als Leerzeichen, <c>ä ö ü ß</c> als <c>ae oe ue ss</c>, Akzente entfernt.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            switch (c)
            {
                case 'ä':
                    builder.Append("ae");
                    break;
                case 'ö':
                    builder.Append("oe");
                    break;
                case 'ü':
                    builder.Append("ue");
                    break;
                case 'ß':
                    builder.Append("ss");
                    break;
                case '-' or '_' or '‑' or '–':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(c);
            }
        }

        return string.Join(' ', plain.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public IReadOnlyList<EmojiInfo> Find(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _catalog.Emojis;
        }

        var trimmed = query.Trim();

        // Das Emoji selbst eingegeben (mit oder ohne Hautton).
        if (trimmed.Any(c => c > 127) && _catalog.Find(trimmed) is { } exact)
        {
            return [exact];
        }

        var terms = Normalize(trimmed).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return _catalog.Emojis;
        }

        var phrase = string.Join(' ', terms);
        var results = new List<(int Rank, EmojiInfo Info)>();
        foreach (var entry in _entries.Value)
        {
            var total = 0;
            var matched = true;
            foreach (var term in terms)
            {
                var rank = entry.Rank(term, phrase);
                if (rank < 0)
                {
                    matched = false;
                    break;
                }

                total += rank;
            }

            if (matched)
            {
                results.Add((total, entry.Info));
            }
        }

        return [.. results.OrderBy(r => r.Rank).ThenBy(r => r.Info.Order).Select(r => r.Info)];
    }

    private ImmutableArray<Entry> BuildEntries() =>
        [.. _catalog.Emojis.Select(e => new Entry(
            e,
            [Normalize(e.NameDe), Normalize(e.NameEn)],
            [.. e.KeywordsDe.Concat(e.KeywordsEn).Select(Normalize)],
            [.. e.Synonyms.Select(Normalize)]))];

    private sealed record Entry(EmojiInfo Info, string[] Names, string[] Keywords, string[] Synonyms)
    {
        /// <summary>Rang eines Suchworts (kleiner = besser), -1 = kein Treffer.</summary>
        public int Rank(string term, string phrase)
        {
            if (Names.Any(n => n == phrase || n == term))
            {
                return 0;
            }

            if (Names.Any(n => n.StartsWith(term, StringComparison.Ordinal)))
            {
                return 1;
            }

            if (Names.Any(n => HasWordPrefix(n, term)))
            {
                return 2;
            }

            if (Keywords.Any(k => k == term))
            {
                return 3;
            }

            if (Keywords.Any(k => HasWordPrefix(k, term)))
            {
                return 4;
            }

            if (Synonyms.Any(s => HasWordPrefix(s, term)))
            {
                return 5;
            }

            // Teiltreffer mitten im Wort (nur ab drei Zeichen, damit kurze Eingaben nicht alles treffen).
            if (term.Length >= 3 && (Names.Any(n => n.Contains(term, StringComparison.Ordinal)) || Keywords.Any(k => k.Contains(term, StringComparison.Ordinal))))
            {
                return 6;
            }

            return -1;
        }

        private static bool HasWordPrefix(string text, string term)
        {
            var index = 0;
            while (index < text.Length)
            {
                var hit = text.IndexOf(term, index, StringComparison.Ordinal);
                if (hit < 0)
                {
                    return false;
                }

                if (hit == 0 || text[hit - 1] == ' ')
                {
                    return true;
                }

                index = hit + 1;
            }

            return false;
        }
    }
}
