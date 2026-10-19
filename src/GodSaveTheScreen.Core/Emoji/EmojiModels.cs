using System.Collections.Immutable;
using System.Globalization;

namespace GodSaveTheScreen.Core.Emoji;

/// <summary>Hautton eines Emojis (Fitzpatrick-Skala). <see cref="None"/> = Standard (gelb).</summary>
public enum SkinTone
{
    None,
    Light,
    MediumLight,
    Medium,
    MediumDark,
    Dark,
}

/// <summary>Oberkategorie aus Unicode <c>emoji-test.txt</c> (ohne „Component“).</summary>
public sealed record EmojiGroup(int Index, string Name, string Key);

/// <summary>Ein Emoji des Katalogs (ohne Hautton) mit Namen und Suchbegriffen in Deutsch und Englisch.</summary>
public sealed record EmojiInfo
{
    public required string Sequence { get; init; }

    public required int GroupIndex { get; init; }

    public string Subgroup { get; init; } = string.Empty;

    /// <summary>Position in der Unicode-Reihenfolge.</summary>
    public int Order { get; init; }

    public string UnicodeVersion { get; init; } = string.Empty;

    public required string NameDe { get; init; }

    public required string NameEn { get; init; }

    public ImmutableArray<string> KeywordsDe { get; init; } = [];

    public ImmutableArray<string> KeywordsEn { get; init; } = [];

    /// <summary>Zusätzliche deutsche Synonyme (aus <c>symbols.csv</c>).</summary>
    public ImmutableArray<string> Synonyms { get; init; } = [];

    /// <summary>Varianten für die Hauttöne <see cref="SkinTone.Light"/> bis <see cref="SkinTone.Dark"/> (null = nicht vorhanden); leer, wenn es keine gibt.</summary>
    public ImmutableArray<string?> SkinVariants { get; init; } = [];

    public bool HasSkinTones => !SkinVariants.IsDefaultOrEmpty;

    /// <summary>Name in der Sprache der Kultur (Deutsch, sonst Englisch).</summary>
    public string GetName(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "de" ? NameDe : NameEn;
}
