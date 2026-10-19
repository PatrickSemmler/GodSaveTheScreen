using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Eintrag der Schriftauswahl: <see cref="Key"/> = Familienname im Modell, <see cref="DisplayName"/> lokalisiert.</summary>
public sealed record FontItem(string Key, string DisplayName, FontFamily Family);

/// <summary>Schnitt einer Schriftfamilie (Gewicht, Stil, Breite) mit lokalisiertem Namen.</summary>
public sealed record TypefaceItem(string Name, int Weight, FontStyleKind Style, int Stretch);

/// <summary>Installierte Schriften und ihre Schnitte (einmal geladen, alphabetisch nach Anzeigename).</summary>
public static class FontCatalog
{
    private static readonly Lazy<IReadOnlyList<FontItem>> AllFonts = new(Load);

    public static IReadOnlyList<FontItem> All => AllFonts.Value;

    public static bool Exists(string key) =>
        All.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public static FontItem? Find(string key) =>
        All.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Schnitte der Familie, sortiert nach Gewicht und Stil; ohne Angaben ein synthetischer Satz (Standard/Fett/Kursiv/Fett Kursiv).</summary>
    public static IReadOnlyList<TypefaceItem> GetTypefaces(string familyKey)
    {
        var language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        FontFamily family;
        try
        {
            family = new FontFamily(familyKey);
        }
        catch (ArgumentException)
        {
            return Fallback();
        }

        var faces = new List<TypefaceItem>();
        foreach (var face in family.FamilyTypefaces)
        {
            var name = LocalizedName(face.AdjustedFaceNames, language) ?? Strings.Font_Regular;
            faces.Add(new TypefaceItem(
                name,
                face.Weight.ToOpenTypeWeight(),
                face.Style == FontStyles.Italic ? FontStyleKind.Italic : face.Style == FontStyles.Oblique ? FontStyleKind.Oblique : FontStyleKind.Normal,
                face.Stretch.ToOpenTypeStretch()));
        }

        return faces.Count == 0
            ? Fallback()
            : [.. faces.DistinctBy(f => (f.Weight, f.Style, f.Stretch)).OrderBy(f => f.Stretch != TextAnnotation.NormalStretch).ThenBy(f => f.Weight).ThenBy(f => f.Style)];
    }

    private static IReadOnlyList<TypefaceItem> Fallback() =>
    [
        new(Strings.Font_Regular, 400, FontStyleKind.Normal, TextAnnotation.NormalStretch),
        new(Strings.Font_Bold, 700, FontStyleKind.Normal, TextAnnotation.NormalStretch),
        new(Strings.Font_Italic, 400, FontStyleKind.Italic, TextAnnotation.NormalStretch),
        new(Strings.Font_BoldItalic, 700, FontStyleKind.Italic, TextAnnotation.NormalStretch),
    ];

    private static IReadOnlyList<FontItem> Load()
    {
        var language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        var items = new List<FontItem>();
        foreach (var family in Fonts.SystemFontFamilies)
        {
            var key = family.Source;
            items.Add(new FontItem(key, LocalizedName(family.FamilyNames, language) ?? key, family));
        }

        return [.. items.OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static string? LocalizedName(IDictionary<XmlLanguage, string> names, XmlLanguage language)
    {
        if (names.Count == 0)
        {
            return null;
        }

        if (names.TryGetValue(language, out var exact))
        {
            return exact;
        }

        var neutral = names.FirstOrDefault(n => n.Key.IetfLanguageTag.StartsWith(language.IetfLanguageTag.Split('-')[0], StringComparison.OrdinalIgnoreCase));
        if (neutral.Value is not null)
        {
            return neutral.Value;
        }

        return names.TryGetValue(XmlLanguage.GetLanguage("en-us"), out var english) ? english : names.Values.First();
    }
}
