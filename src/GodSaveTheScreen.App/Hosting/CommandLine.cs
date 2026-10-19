using System.Globalization;
using GodSaveTheScreen.Core.Capture;

namespace GodSaveTheScreen.App.Hosting;

public sealed record CommandLineOptions(
    CaptureMode? Capture,
    double? DelaySeconds,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Errors,
    bool Tray = false)
{
    public static CommandLineOptions Empty { get; } = new(null, null, [], []);
}

/// <summary>Einfacher Kommandozeilen-Parser: <c>--capture &lt;rect|window|full|freeform&gt;</c>, <c>--delay &lt;s&gt;</c>, <c>--tray</c>, Dateipfade.</summary>
public static class CommandLineParser
{
    public static CommandLineOptions Parse(IEnumerable<string> args)
    {
        CaptureMode? capture = null;
        double? delay = null;
        var files = new List<string>();
        var errors = new List<string>();
        var tray = false;

        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            switch (arg.ToLowerInvariant())
            {
                case "--capture":
                    if (!enumerator.MoveNext())
                    {
                        errors.Add("--capture erwartet einen Wert (rect|window|full|freeform).");
                        break;
                    }

                    capture = ParseMode(enumerator.Current);
                    if (capture is null)
                    {
                        errors.Add($"Unbekannter Aufnahmemodus: {enumerator.Current}");
                    }

                    break;

                case "--delay":
                    if (!enumerator.MoveNext())
                    {
                        errors.Add("--delay erwartet eine Zahl in Sekunden.");
                        break;
                    }

                    if (double.TryParse(enumerator.Current, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
                    {
                        delay = seconds;
                    }
                    else
                    {
                        errors.Add($"Ungültige Verzögerung: {enumerator.Current}");
                    }

                    break;

                case "--tray":
                    tray = true;
                    break;

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        errors.Add($"Unbekannte Option: {arg}");
                    }
                    else
                    {
                        files.Add(arg);
                    }

                    break;
            }
        }

        return new CommandLineOptions(capture, delay, files, errors, tray);
    }

    private static CaptureMode? ParseMode(string value) => value.ToLowerInvariant() switch
    {
        "rect" => CaptureMode.Rectangle,
        "window" => CaptureMode.Window,
        "full" => CaptureMode.FullScreen,
        "freeform" => CaptureMode.Freeform,
        _ => null,
    };
}
