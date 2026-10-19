using System.Globalization;
using System.IO;
using System.Windows;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Öffnet Editorfenster (je Fenster eigene Kontext-/Werkzeug-/ViewModel-Instanzen).</summary>
public interface IEditorService
{
    /// <summary>Dokumente der geöffneten Editorfenster (für die automatische Sicherung).</summary>
    IReadOnlyList<ImageDocument> OpenDocuments { get; }

    /// <summary>Ein Editorfenster wurde geschlossen (gespeichert oder verworfen).</summary>
    event EventHandler<ImageDocument>? DocumentClosed;

    /// <summary>Öffnet ein Dokument in einem neuen Editorfenster.</summary>
    EditorWindow Open(ImageDocument document, string? info = null);

    /// <summary>Lädt Bild- und Projektdateien (<c>.gsts</c>) und öffnet je einen Editor; Fehler werden angezeigt.</summary>
    Task OpenFilesAsync(IEnumerable<string> paths);

    /// <summary>Öffnet das Bild aus der Zwischenablage; <c>false</c>, wenn keines vorhanden ist.</summary>
    bool OpenFromClipboard();
}

public sealed class EditorService : IEditorService
{
    private readonly IServiceProvider _services;
    private readonly IWindowExclusionService _exclusion;
    private readonly IImageCodecService _codec;
    private readonly IProjectService _projects;
    private readonly IRecentFilesService _recents;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IMonitorService _monitors;
    private readonly ILogger<EditorService> _logger;
    private readonly List<(ImageDocument Document, EditorWindow Window)> _open = [];

    public EditorService(
        IServiceProvider services,
        IWindowExclusionService exclusion,
        IImageCodecService codec,
        IProjectService projects,
        IRecentFilesService recents,
        IClipboardService clipboard,
        IDialogService dialogs,
        IMonitorService monitors,
        ILogger<EditorService> logger)
    {
        _services = services;
        _exclusion = exclusion;
        _codec = codec;
        _projects = projects;
        _recents = recents;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _monitors = monitors;
        _logger = logger;
    }

    public event EventHandler<ImageDocument>? DocumentClosed;

    public IReadOnlyList<ImageDocument> OpenDocuments => [.. _open.Select(o => o.Document)];

    public EditorWindow Open(ImageDocument document, string? info = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = ActivatorUtilities.CreateInstance<EditorContext>(_services, document);
        var tools = ToolRegistry.Create(context);
        var viewModel = ActivatorUtilities.CreateInstance<EditorViewModel>(_services, context, tools);
        viewModel.InfoMessage = info;

        var window = ActivatorUtilities.CreateInstance<EditorWindow>(_services, viewModel);
        _exclusion.Register(window);
        _open.Add((document, window));
        window.Closed += (_, _) =>
        {
            viewModel.Dispose();
            _open.RemoveAll(o => ReferenceEquals(o.Document, document));
            DocumentClosed?.Invoke(this, document);
        };
        try
        {
            window.PlaceOn(_monitors.GetMonitorAtCursor());
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Monitor für das Editorfenster nicht ermittelbar");
        }

        window.Show();
        window.Activate();
        return window;
    }

    public async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.Equals(Path.GetExtension(path), ProjectSerializer.Extension, StringComparison.OrdinalIgnoreCase))
            {
                await OpenProjectAsync(path);
            }
            else
            {
                await OpenImageAsync(path);
            }
        }
    }

    private async Task OpenImageAsync(string path)
    {
        try
        {
            if (_codec.PeekSize(path) is { } size
                && LargeImageGuard.IsLarge(size.Width, size.Height)
                && !_dialogs.Confirm(
                    Strings.LargeImage_Title,
                    string.Format(CultureInfo.CurrentUICulture, Strings.LargeImage_Message, Path.GetFileName(path), size.Width, size.Height, LargeImageGuard.BytesFor(size.Width, size.Height) / (1024 * 1024))))
            {
                return;
            }

            var loaded = await _codec.LoadAsync(path);
            var document = new ImageDocument(loaded.Image, loaded.DpiX, loaded.DpiY) { SourcePath = path };
            var info = loaded.FrameCount > 1
                ? string.Format(CultureInfo.CurrentUICulture, Strings.Editor_MultiFrameInfo, Path.GetFileName(path), loaded.FrameCount)
                : null;
            Open(document, info);
            _recents.Add(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException or FileFormatException)
        {
            _logger.LogError(ex, "Datei konnte nicht geöffnet werden: {Path}", path);
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_OpenFailed, Path.GetFileName(path), ex.Message));
        }
    }

    private async Task OpenProjectAsync(string path)
    {
        // Ein bereits geöffnetes Projekt nicht doppelt öffnen (zwei Fenster würden sich gegenseitig überschreiben).
        var existing = _open.FirstOrDefault(o => string.Equals(o.Document.ProjectPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing.Window is { } window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            return;
        }

        try
        {
            var loaded = await _projects.LoadAsync(path);
            var info = loaded.Warnings.Count > 0
                ? string.Format(CultureInfo.CurrentUICulture, Strings.Project_Warnings, loaded.Warnings.Count, string.Join(" ", loaded.Warnings))
                : null;
            Open(loaded.Document, info);
            _recents.Add(path);
        }
        catch (Exception ex) when (ex is ProjectFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogError(ex, "Projekt konnte nicht geöffnet werden: {Path}", path);
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_OpenFailed, Path.GetFileName(path), ex.Message));
        }
    }

    public bool OpenFromClipboard()
    {
        PixelBuffer? image;
        try
        {
            image = _clipboard.GetImage();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger.LogError(ex, "Zwischenablage konnte nicht gelesen werden");
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_ClipboardRead, ex.Message));
            return false;
        }

        if (image is null)
        {
            return false;
        }

        Open(new ImageDocument(image) { IsDirty = true });
        return true;
    }
}
