using System.Collections.Frozen;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;

namespace GodSaveTheScreen.Core.Emoji;

/// <summary>
/// Eingebetteter Emoji-Katalog (Unicode <c>emoji-test.txt</c> + CLDR-Namen, siehe <c>build/generate-emoji-data.ps1</c>).
/// Wird beim ersten Zugriff einmalig und threadsicher geladen. Kein WPF.
/// </summary>
public sealed class EmojiCatalog
{
    private const string ResourceName = "GodSaveTheScreen.Core.Emoji.emoji-data.json.gz";
    private const string VariationSelector = "️";

    private static readonly Lazy<EmojiCatalog> SharedInstance = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly FrozenDictionary<string, EmojiInfo> _byKey;
    private readonly FrozenDictionary<string, (EmojiInfo Info, SkinTone Tone)> _variants;

    private EmojiCatalog(ImmutableArray<EmojiGroup> groups, ImmutableArray<EmojiInfo> emojis, string unicodeVersion)
    {
        Groups = groups;
        Emojis = emojis;
        UnicodeVersion = unicodeVersion;
        var byKey = new Dictionary<string, EmojiInfo>(StringComparer.Ordinal);
        var variants = new Dictionary<string, (EmojiInfo, SkinTone)>(StringComparer.Ordinal);
        foreach (var emoji in emojis)
        {
            byKey[Normalize(emoji.Sequence)] = emoji;
            for (var i = 0; i < emoji.SkinVariants.Length; i++)
            {
                if (emoji.SkinVariants[i] is { } variant)
                {
                    variants[Normalize(variant)] = (emoji, (SkinTone)(i + 1));
                }
            }
        }

        _byKey = byKey.ToFrozenDictionary(StringComparer.Ordinal);
        _variants = variants.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static EmojiCatalog Shared => SharedInstance.Value;

    public ImmutableArray<EmojiGroup> Groups { get; }

    /// <summary>Alle Emojis in Unicode-Reihenfolge (ohne Hautton-Varianten).</summary>
    public ImmutableArray<EmojiInfo> Emojis { get; }

    public string UnicodeVersion { get; }

    /// <summary>Lädt einen Katalog aus dem JSON-Format des Generators (unkomprimiert).</summary>
    public static EmojiCatalog Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var groups = ImmutableArray.CreateBuilder<EmojiGroup>();
        var index = 0;
        foreach (var group in root.GetProperty("groups").EnumerateArray())
        {
            var name = group.GetString() ?? string.Empty;
            groups.Add(new EmojiGroup(index++, name, GroupKey(name)));
        }

        var emojis = ImmutableArray.CreateBuilder<EmojiInfo>();
        var order = 0;
        foreach (var item in root.GetProperty("emojis").EnumerateArray())
        {
            var names = item.GetProperty("n");
            var english = names.GetProperty("en").GetString() ?? string.Empty;
            emojis.Add(new EmojiInfo
            {
                Sequence = item.GetProperty("s").GetString() ?? string.Empty,
                GroupIndex = item.GetProperty("g").GetInt32(),
                Subgroup = item.GetProperty("sg").GetString() ?? string.Empty,
                UnicodeVersion = item.GetProperty("v").GetString() ?? string.Empty,
                Order = order++,
                NameEn = english,
                NameDe = names.TryGetProperty("de", out var de) ? de.GetString() ?? english : english,
                KeywordsDe = ReadStrings(item, "k", "de"),
                KeywordsEn = ReadStrings(item, "k", "en"),
                Synonyms = item.TryGetProperty("y", out var synonyms) ? [.. synonyms.EnumerateArray().Select(s => s.GetString() ?? string.Empty)] : [],
                SkinVariants = item.TryGetProperty("t", out var tones) ? [.. tones.EnumerateArray().Select(t => t.ValueKind == JsonValueKind.String ? t.GetString() : null)] : [],
            });
        }

        var version = root.TryGetProperty("source", out var source) && source.TryGetProperty("unicodeEmojiVersion", out var v) ? v.GetString() ?? string.Empty : string.Empty;
        return new EmojiCatalog(groups.ToImmutable(), emojis.ToImmutable(), version);
    }

    /// <summary>Katalogeintrag zu einer Sequenz (mit oder ohne Hautton, mit oder ohne <c>U+FE0F</c>).</summary>
    public EmojiInfo? Find(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
        {
            return null;
        }

        var key = Normalize(sequence);
        if (_byKey.TryGetValue(key, out var info))
        {
            return info;
        }

        return _variants.TryGetValue(key, out var variant) ? variant.Info : null;
    }

    /// <summary>Das Emoji ohne Hautton; unbekannte Sequenzen bleiben unverändert.</summary>
    public string GetBaseSequence(string sequence) => Find(sequence)?.Sequence ?? sequence;

    /// <summary>Hautton der Sequenz (<see cref="SkinTone.None"/> für Basis-Emojis und unbekannte Sequenzen).</summary>
    public SkinTone GetSkinTone(string sequence) =>
        !string.IsNullOrEmpty(sequence) && _variants.TryGetValue(Normalize(sequence), out var variant) ? variant.Tone : SkinTone.None;

    /// <summary>Variante mit dem Hautton; ohne passende Variante (oder bei <see cref="SkinTone.None"/>) das Basis-Emoji.</summary>
    public string ApplySkinTone(string sequence, SkinTone tone)
    {
        var info = Find(sequence);
        if (info is null)
        {
            return sequence;
        }

        if (tone == SkinTone.None || !info.HasSkinTones)
        {
            return info.Sequence;
        }

        var index = (int)tone - 1;
        return index < info.SkinVariants.Length && info.SkinVariants[index] is { } variant ? variant : info.Sequence;
    }

    private static string Normalize(string sequence) => sequence.Replace(VariationSelector, string.Empty, StringComparison.Ordinal);

    private static ImmutableArray<string> ReadStrings(JsonElement item, string property, string language)
    {
        if (item.TryGetProperty(property, out var container) && container.TryGetProperty(language, out var list))
        {
            return [.. list.EnumerateArray().Select(e => e.GetString() ?? string.Empty)];
        }

        return [];
    }

    private static string GroupKey(string name) => name switch
    {
        "Smileys & Emotion" => "smileys",
        "People & Body" => "people",
        "Animals & Nature" => "nature",
        "Food & Drink" => "food",
        "Travel & Places" => "travel",
        "Activities" => "activities",
        "Objects" => "objects",
        "Symbols" => "symbols",
        "Flags" => "flags",
        _ => name.ToLowerInvariant(),
    };

    private static EmojiCatalog LoadEmbedded()
    {
        using var resource = typeof(EmojiCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Emoji-Daten fehlen in der Assembly.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        return Parse(gzip);
    }
}
