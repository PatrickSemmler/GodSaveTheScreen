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
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Öffnet Editorfenster (je Fenster eigene Kontext-/Werkzeug-/ViewModel-Instanzen).</summary>
public interface IEditorService
{
    /// <summary>Öffnet ein Dokument in einem neuen Editorfenster.</summary>
    EditorWindow Open(ImageDocument document, string? info = null);

    /// <summary>Lädt Bilddateien und öffnet je einen Editor; Fehler werden angezeigt.</summary>
    Task OpenFilesAsync(IEnumerable<string> paths);

    /// <summary>Öffnet das Bild aus der Zwischenablage; <c>false</c>, wenn keines vorhanden ist.</summary>
    bool OpenFromClipboard();
}

public sealed class EditorService : IEditorService
{
    private readonly IServiceProvider _services;
    private readonly IWindowExclusionService _exclusion;
    private readonly IImageCodecService _codec;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly IMonitorService _monitors;
    private readonly ILogger<EditorService> _logger;

    public EditorService(
        IServiceProvider services,
        IWindowExclusionService exclusion,
        IImageCodecService codec,
        IClipboardService clipboard,
        IDialogService dialogs,
        IMonitorService monitors,
        ILogger<EditorService> logger)
    {
        _services = services;
        _exclusion = exclusion;
        _codec = codec;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _monitors = monitors;
        _logger = logger;
    }

    public EditorWindow Open(ImageDocument document, string? info = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = ActivatorUtilities.CreateInstance<EditorContext>(_services, document);
        var tools = ToolRegistry.Create(context);
        var viewModel = ActivatorUtilities.CreateInstance<EditorViewModel>(_services, context, tools);
        viewModel.InfoMessage = info;

        var window = ActivatorUtilities.CreateInstance<EditorWindow>(_services, viewModel);
        _exclusion.Register(window);
        window.Closed += (_, _) => viewModel.Dispose();
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
            try
            {
                var loaded = await _codec.LoadAsync(path);
                var document = new ImageDocument(loaded.Image, loaded.DpiX, loaded.DpiY) { SourcePath = path };
                var info = loaded.FrameCount > 1
                    ? string.Format(CultureInfo.CurrentUICulture, Strings.Editor_MultiFrameInfo, Path.GetFileName(path), loaded.FrameCount)
                    : null;
                Open(document, info);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException or FileFormatException)
            {
                _logger.LogError(ex, "Datei konnte nicht geöffnet werden: {Path}", path);
                _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_OpenFailed, Path.GetFileName(path), ex.Message));
            }
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
