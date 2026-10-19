using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GodSaveTheScreen.Core.Naming;

/// <summary>Werte, die in das Dateinamensmuster eingesetzt werden.</summary>
public sealed record FileNameContext(DateTimeOffset Timestamp, string Mode, string? WindowTitle = null);

/// <summary>
/// Dateinamensmuster mit Platzhaltern <c>{date:format}</c>, <c>{time:format}</c>, <c>{mode}</c>,
/// <c>{window}</c> und <c>{counter:000}</c>. Ungültige Zeichen werden ersetzt, Kollisionen aufgelöst.
/// </summary>
public static partial class FileNamePattern
{
    private const int MaxBaseNameLength = 120;
    private const int MaxCounter = 100_000;

    public static bool ContainsCounter(string pattern) =>
        PlaceholderRegex().Matches(pattern).Any(m => string.Equals(m.Groups[1].Value, "counter", StringComparison.OrdinalIgnoreCase));

    /// <summary>Setzt das Muster ein (ohne Erweiterung, ohne Kollisionsprüfung). <paramref name="counter"/> ersetzt <c>{counter}</c>.</summary>
    public static string Render(string pattern, FileNameContext context, int counter = 1)
    {
        var text = string.IsNullOrWhiteSpace(pattern) ? Settings.AppSettings.DefaultFileNamePattern : pattern;
        var rendered = PlaceholderRegex().Replace(text, m => Replace(m, context, counter));
        return Sanitize(rendered);
    }

    /// <summary>
    /// Liefert einen freien Dateipfad. Mit <c>{counter}</c> wird die kleinste freie Nummer gewählt,
    /// sonst wird bei Kollision ein Suffix <c> (2)</c>, <c> (3)</c> … angehängt.
    /// </summary>
    public static string BuildPath(string pattern, string directory, string extension, FileNameContext context, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var ext = extension.StartsWith('.') ? extension : "." + extension;

        string PathOf(string name) => System.IO.Path.Combine(directory, name + ext);

        if (ContainsCounter(pattern))
        {
            for (var n = 1; n <= MaxCounter; n++)
            {
                var candidate = PathOf(Render(pattern, context, n));
                if (!exists(candidate))
                {
                    return candidate;
                }
            }
        }

        var baseName = Render(pattern, context);
        var first = PathOf(baseName);
        if (!exists(first))
        {
            return first;
        }

        for (var n = 2; n <= MaxCounter; n++)
        {
            var candidate = PathOf($"{baseName} ({n})");
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Es konnte kein freier Dateiname gefunden werden.");
    }

    /// <summary>Ersetzt ungültige Dateinamenzeichen und kürzt überlange Namen.</summary>
    public static string Sanitize(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(char.IsControl(c) || Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        if (result.Length > MaxBaseNameLength)
        {
            result = result[..MaxBaseNameLength].TrimEnd();
        }

        return result.Length == 0 ? "Screenshot" : result;
    }

    private static string Replace(Match match, FileNameContext context, int counter)
    {
        var name = match.Groups[1].Value.ToLowerInvariant();
        var format = match.Groups[2].Success ? match.Groups[2].Value : null;
        try
        {
            return name switch
            {
                "date" => context.Timestamp.ToString(format ?? "yyyy-MM-dd", CultureInfo.InvariantCulture),
                "time" => context.Timestamp.ToString(format ?? "HH-mm-ss", CultureInfo.InvariantCulture),
                "mode" => context.Mode,
                "window" => string.IsNullOrWhiteSpace(context.WindowTitle) ? string.Empty : context.WindowTitle,
                "counter" => counter.ToString(format ?? "0", CultureInfo.InvariantCulture),
                _ => match.Value,
            };
        }
        catch (FormatException)
        {
            return match.Value;
        }
    }

    [GeneratedRegex(@"\{(\w+)(?::([^}]*))?\}")]
    private static partial Regex PlaceholderRegex();
}
