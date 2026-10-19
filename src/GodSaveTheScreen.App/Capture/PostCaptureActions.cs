using System.IO;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Naming;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Kopiert das Bild in die Zwischenablage (Einstellung „In Zwischenablage kopieren“).</summary>
public sealed class CopyToClipboardAction : IPostCaptureAction
{
    private readonly ISettingsService _settings;
    private readonly IClipboardService _clipboard;

    public CopyToClipboardAction(ISettingsService settings, IClipboardService clipboard)
    {
        _settings = settings;
        _clipboard = clipboard;
    }

    public Task ExecuteAsync(CaptureResultContext context)
    {
        if (_settings.Current.CopyToClipboard)
        {
            _clipboard.SetImage(context.Document.BaseImage);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Speichert das Bild automatisch nach Dateinamensmuster (Einstellung „Automatisch speichern“).</summary>
public sealed class AutoSaveAction : IPostCaptureAction
{
    private readonly ISettingsService _settings;
    private readonly INotificationService _notifications;
    private readonly ILogger<AutoSaveAction> _logger;

    public AutoSaveAction(ISettingsService settings, INotificationService notifications, ILogger<AutoSaveAction> logger)
    {
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Standardzielordner: <c>%USERPROFILE%\Pictures\Screenshots\GodSaveTheScreen</c>.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots", "GodSaveTheScreen");

    public static string ResolveDirectory(AppSettings settings) =>
        string.IsNullOrWhiteSpace(settings.AutoSaveDirectory) ? DefaultDirectory : settings.AutoSaveDirectory;

    public async Task ExecuteAsync(CaptureResultContext context)
    {
        var settings = _settings.Current;
        if (!settings.AutoSave)
        {
            return;
        }

        var directory = ResolveDirectory(settings);
        Directory.CreateDirectory(directory);

        var fileContext = new FileNameContext(context.Timestamp, CaptureModeNames.Get(context.Mode), context.WindowTitle);
        var path = FileNamePattern.BuildPath(settings.FileNamePattern, directory, settings.AutoSaveFormat.FileExtension(), fileContext, File.Exists);
        var data = context.Document.BaseImage.Encode(settings.AutoSaveFormat, context.Document.DpiX, context.Document.DpiY);
        await File.WriteAllBytesAsync(path, data);

        _logger.LogInformation("Automatisch gespeichert: {Path}", path);
        _notifications.ShowSaved(path);
    }
}

/// <summary>Öffnet den Editor (immer, letzte Aktion der Kette).</summary>
public sealed class OpenEditorAction : IPostCaptureAction
{
    private readonly IWindowFactory _windows;

    public OpenEditorAction(IWindowFactory windows) => _windows = windows;

    public Task ExecuteAsync(CaptureResultContext context)
    {
        var editor = _windows.Create<EditorWindow>(context.Document);
        editor.Show();
        editor.Activate();
        return Task.CompletedTask;
    }
}
