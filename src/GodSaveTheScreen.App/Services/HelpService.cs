using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Öffnet die Benutzeranleitung (HTML neben der EXE) im Standardbrowser, optional an einem Abschnitt.</summary>
public interface IHelpService
{
    /// <summary>Öffnet die Hilfe in der Programmsprache; <paramref name="topic"/> = Abschnitts-ID (<see cref="HelpTopics"/>), <c>null</c> = Anfang.</summary>
    void Show(string? topic = null);
}

/// <summary>Abschnitts-IDs der Hilfe. Müssen in beiden Sprachfassungen (<c>docs/help/*/index.html</c>) existieren (<c>HelpContentTests</c>).</summary>
public static class HelpTopics
{
    public const string StartWindow = "start-window";
    public const string Editor = "editor";
    public const string Layers = "layers";
    public const string Colors = "colors";
    public const string Shortcuts = "shortcuts";
    public const string SettingsGeneral = "settings-general";
    public const string SettingsCapture = "settings-capture";
    public const string SettingsPostCapture = "settings-postcapture";
    public const string SettingsHotkeys = "settings-hotkeys";
    public const string SettingsAbout = "settings-about";

    /// <summary>Abschnitt eines Editor-Werkzeugs (<c>tool-rectangle</c> …); die Pipette steht unter „Farben“.</summary>
    public static string ForTool(string toolId) =>
        toolId == PipetteTool.ToolId ? Colors : "tool-" + toolId;

    /// <summary>Abschnitt zu ausgewählten Objekten, wenn alle zum selben Werkzeug gehören; sonst <c>null</c>.</summary>
    public static string? ForSelection(IReadOnlyList<Core.Annotations.Annotation> items)
    {
        var tools = items.Select(ToolOf).Distinct().ToList();
        return tools is [{ } tool] ? ForTool(tool) : null;
    }

    private static string? ToolOf(Core.Annotations.Annotation annotation) => annotation switch
    {
        Core.Annotations.BoxAnnotation => RectangleTool.ToolId,
        Core.Annotations.LineAnnotation => ArrowTool.ToolId,
        Core.Annotations.PolygonAnnotation => PolygonTool.ToolId,
        Core.Annotations.StrokeAnnotation => PenTool.ToolId,
        Core.Annotations.TextAnnotation => TextTool.ToolId,
        Core.Annotations.MarkerAnnotation => MarkerTool.ToolId,
        Core.Annotations.BlurAnnotation => BlurTool.ToolId,
        Core.Annotations.MagnifierAnnotation => MagnifierTool.ToolId,
        Core.Annotations.EmojiAnnotation => EmojiTool.ToolId,
        Core.Annotations.StampAnnotation => StampTool.ToolId,
        Core.Annotations.CalloutAnnotation => CalloutTool.ToolId,
        Core.Annotations.ImageAnnotation => ImageTool.ToolId,
        _ => null,
    };
}

/// <summary>Dateinamen und Sprungziel der Hilfe; ohne WPF, damit testbar.</summary>
public static class HelpFiles
{
    public const string German = "GodSaveTheScreen-Hilfe.html";
    public const string English = "GodSaveTheScreen-Help.html";

    /// <summary>Hilfedatei zur Sprache (Deutsch bei <c>de</c>, sonst Englisch); fehlt sie, die andere Sprachfassung, sonst <c>null</c>.</summary>
    public static string? Resolve(string directory, CultureInfo culture)
    {
        var preferred = culture.TwoLetterISOLanguageName == "de" ? German : English;
        var fallback = preferred == German ? English : German;
        foreach (var name in new[] { preferred, fallback })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>Abschnitts-IDs bestehen nur aus Kleinbuchstaben, Ziffern und Bindestrich.</summary>
    public static bool IsValidTopic(string? topic) =>
        !string.IsNullOrEmpty(topic) && topic.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    /// <summary>
    /// Weiterleitungsseite auf <c>Datei#Abschnitt</c>: Windows verwirft beim Öffnen einer <c>file:</c>-URL über die
    /// Dateizuordnung den Anker, eine lokale HTML-Datei, die selbst weiterleitet, behält ihn.
    /// </summary>
    public static string BuildRedirect(string helpFile, string topic)
    {
        var url = new Uri(helpFile).AbsoluteUri + "#" + topic;
        var attribute = WebUtility.HtmlEncode(url);
        var script = url.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
        return $"""
            <!doctype html>
            <html><head><meta charset="utf-8"><meta http-equiv="refresh" content="0;url={attribute}">
            <script>location.replace('{script}');</script></head>
            <body><a href="{attribute}">{attribute}</a></body></html>
            """;
    }
}

public sealed class HelpService : IHelpService
{
    private readonly IDialogService _dialogs;
    private readonly ILogger<HelpService> _logger;

    public HelpService(IDialogService dialogs, ILogger<HelpService> logger)
    {
        _dialogs = dialogs;
        _logger = logger;
    }

    public void Show(string? topic = null)
    {
        var file = HelpFiles.Resolve(AppContext.BaseDirectory, CultureInfo.CurrentUICulture);
        if (file is null)
        {
            _dialogs.ShowError(Strings.Help_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Help_NotFound, AppContext.BaseDirectory));
            return;
        }

        try
        {
            var target = file;
            if (HelpFiles.IsValidTopic(topic))
            {
                Directory.CreateDirectory(DragTempFiles.Directory);
                target = Path.Combine(DragTempFiles.Directory, "help.html");
                File.WriteAllText(target, HelpFiles.BuildRedirect(file, topic!));
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
            _logger.LogInformation("Hilfe geöffnet: {Topic}", topic ?? "(Anfang)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Hilfe konnte nicht geöffnet werden");
            _dialogs.ShowError(Strings.Help_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_OpenFailed, file, ex.Message));
        }
    }
}
