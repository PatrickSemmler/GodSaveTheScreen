using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.ViewModels;

public enum ZoomMode
{
    Fit,
    ActualSize,
}

/// <summary>Vorläufiges Ergebnisfenster (wird in M4 zum Editor ausgebaut).</summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly ILogger<EditorViewModel> _logger;

    public EditorViewModel(
        ImageDocument document,
        IDialogService dialogs,
        IClipboardService clipboard,
        ILogger<EditorViewModel> logger)
    {
        Document = document;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _logger = logger;
    }

    public ImageDocument Document { get; }

    [ObservableProperty]
    private ZoomMode _zoomMode = ZoomMode.Fit;

    [RelayCommand]
    private void Fit() => ZoomMode = ZoomMode.Fit;

    [RelayCommand]
    private void ActualSize() => ZoomMode = ZoomMode.ActualSize;

    [RelayCommand]
    private void SaveAs()
    {
        var suggestion = string.Format(CultureInfo.CurrentUICulture, Strings.Editor_SaveDefaultName, DateTime.Now);
        var path = _dialogs.PickSavePath(suggestion, Strings.Editor_SaveFilter);
        if (path is null)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(path, Document.BaseImage.EncodePng(Document.DpiX, Document.DpiY));
            Document.SourcePath = path;
            Document.IsDirty = false;
            _logger.LogInformation("Bild gespeichert: {Path}", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Speichern nach {Path} fehlgeschlagen", path);
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_SaveFailed, ex.Message));
        }
    }

    [RelayCommand]
    private void Copy()
    {
        try
        {
            _clipboard.SetImage(Document.BaseImage);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger.LogError(ex, "Kopieren in die Zwischenablage fehlgeschlagen");
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_CopyFailed, ex.Message));
        }
    }
}
