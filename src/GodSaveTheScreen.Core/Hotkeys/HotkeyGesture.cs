using System.Globalization;

namespace GodSaveTheScreen.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// Tastenkombination: Modifikatoren plus Taste. <see cref="KeyName"/> ist der Name des WPF-<c>Key</c>
/// (z. B. <c>S</c>, <c>D1</c>, <c>Snapshot</c>), damit Core ohne WPF auskommt.
/// Serialisierte Form (invariant): <c>Ctrl+Shift+S</c>.
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string KeyName)
{
    private static readonly (HotkeyModifiers Modifier, string Token)[] ModifierTokens =
    [
        (HotkeyModifiers.Control, "Ctrl"),
        (HotkeyModifiers.Alt, "Alt"),
        (HotkeyModifiers.Shift, "Shift"),
        (HotkeyModifiers.Windows, "Win"),
    ];

    private static readonly Dictionary<string, string> InvariantDisplayKeys = new(StringComparer.Ordinal)
    {
        ["Snapshot"] = "PrtSc",
        ["Return"] = "Enter",
        ["Next"] = "PageDown",
        ["Prior"] = "PageUp",
        ["OemPlus"] = "+",
        ["OemMinus"] = "-",
        ["OemComma"] = ",",
        ["OemPeriod"] = ".",
    };

    private static readonly Dictionary<string, string> GermanDisplayKeys = new(StringComparer.Ordinal)
    {
        ["Snapshot"] = "Druck",
        ["Return"] = "Eingabe",
        ["Next"] = "Bild ab",
        ["Prior"] = "Bild auf",
        ["Delete"] = "Entf",
        ["Insert"] = "Einfg",
        ["Escape"] = "Esc",
        ["Space"] = "Leertaste",
        ["Back"] = "Rücktaste",
    };

    /// <summary>Gültig, wenn ein Modifikator gesetzt ist oder die Taste allein zulässig ist (F-Tasten, Druck).</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(KeyName) && (Modifiers != HotkeyModifiers.None || IsStandalone(KeyName));

    public static bool IsStandalone(string keyName) =>
        keyName == "Snapshot"
        || (keyName.Length is 2 or 3
            && keyName[0] == 'F'
            && int.TryParse(keyName.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n is >= 1 and <= 24);

    public static HotkeyGesture? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var modifier = ModifierTokens.FirstOrDefault(t => string.Equals(t.Token, raw, StringComparison.OrdinalIgnoreCase));
            if (modifier.Token is not null)
            {
                modifiers |= modifier.Modifier;
            }
            else if (key is null)
            {
                // „1“ → „D1“: Der WPF-Key-Name einer Zifferntaste ist D0–D9; eine nackte Zahl würde
                // Enum.TryParse<Key> als Enum-Wert lesen ((Key)1 = Cancel).
                key = raw.Length == 1 && char.IsAsciiDigit(raw[0]) ? "D" + raw : raw;
            }
            else
            {
                return null;
            }
        }

        if (key is null)
        {
            return null;
        }

        var gesture = new HotkeyGesture(modifiers, key);
        return gesture.IsValid ? gesture : null;
    }

    /// <summary>Invariante, speicherbare Form.</summary>
    public override string ToString()
    {
        var parts = ModifierTokens.Where(t => Modifiers.HasFlag(t.Modifier)).Select(t => t.Token).Append(KeyName);
        return string.Join('+', parts);
    }

    /// <summary>Anzeigetext in der angegebenen Sprache (Deutsch: Strg, Umschalt, Druck …).</summary>
    public string ToDisplayString(CultureInfo? culture = null)
    {
        var german = (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "de";
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add(german ? "Strg" : "Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add(german ? "Umschalt" : "Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(DisplayKey(KeyName, german));
        return string.Join('+', parts);
    }

    private static string DisplayKey(string key, bool german)
    {
        if (german && GermanDisplayKeys.TryGetValue(key, out var de))
        {
            return de;
        }

        if (InvariantDisplayKeys.TryGetValue(key, out var invariant))
        {
            return invariant;
        }

        // D0–D9 → 0–9
        return key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]) ? key[1].ToString() : key;
    }
}
