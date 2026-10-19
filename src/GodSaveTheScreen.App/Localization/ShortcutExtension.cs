using System.ComponentModel;
using System.Globalization;
using System.Windows.Markup;

namespace GodSaveTheScreen.App.Localization;

/// <summary>Übersetzt Tastenkürzel-Texte wie <c>Ctrl+Shift+S</c> in die Anzeigesprache (Deutsch: <c>Strg+Umschalt+S</c>).</summary>
public static class ShortcutText
{
    private static readonly Dictionary<string, string> German = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = "Strg",
        ["Shift"] = "Umschalt",
        ["Del"] = "Entf",
        ["Delete"] = "Entf",
        ["Enter"] = "Eingabe",
        ["Space"] = "Leertaste",
        ["Backspace"] = "Rücktaste",
        ["PageUp"] = "Bild auf",
        ["PageDown"] = "Bild ab",
        ["PrtSc"] = "Druck",
        ["Insert"] = "Einfg",
    };

    /// <summary>Übersetzt jedes durch <c>+</c> getrennte Element; das letzte Element darf selbst <c>+</c> sein (<c>Ctrl++</c>).</summary>
    public static string Localize(string shortcut, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if ((culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName != "de")
        {
            return shortcut;
        }

        // "Ctrl++" und "Ctrl+-": das letzte Zeichen gehört zur Taste.
        var trailingPlus = shortcut.EndsWith("++", StringComparison.Ordinal);
        var body = trailingPlus ? shortcut[..^1] : shortcut;
        var parts = body.Split('+');
        for (var i = 0; i < parts.Length; i++)
        {
            if (German.TryGetValue(parts[i], out var translated))
            {
                parts[i] = translated;
            }
        }

        return string.Join('+', parts) + (trailingPlus ? "+" : string.Empty);
    }
}

/// <summary>XAML-Zugriff: <c>ToolTip="{l:Shortcut Ctrl+S}"</c> bzw. <c>InputGestureText="{l:Shortcut Ctrl+X}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class ShortcutExtension : MarkupExtension
{
    public ShortcutExtension()
    {
    }

    public ShortcutExtension(string text) => Text = text;

    [ConstructorArgument("text")]
    public string Text { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        LicenseManager.UsageMode == LicenseUsageMode.Designtime ? Text : ShortcutText.Localize(Text);
}
