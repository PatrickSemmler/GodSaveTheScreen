using System.Windows;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Zeigt das Ergebnisfenster der Texterkennung für ein Bild (Aufnahmeart „Text aufnehmen“, Editor-Befehl).</summary>
public interface IOcrResultPresenter
{
    /// <summary>Öffnet das Ergebnisfenster und startet die Erkennung.</summary>
    void Show(PixelBuffer image);
}

public sealed class OcrResultPresenter : IOcrResultPresenter
{
    private readonly IWindowFactory _windows;
    private readonly IOcrService _ocr;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<OcrResultPresenter> _logger;

    public OcrResultPresenter(
        IWindowFactory windows,
        IOcrService ocr,
        IClipboardService clipboard,
        ISettingsService settings,
        IServiceProvider services,
        ILogger<OcrResultPresenter> logger)
    {
        _windows = windows;
        _ocr = ocr;
        _clipboard = clipboard;
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public void Show(PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(image);
        void Open()
        {
            var viewModel = new OcrResultViewModel(image, _ocr, _clipboard, _settings, OpenInEditor, _logger);
            var window = _windows.Create<OcrResultWindow>(viewModel);
            window.Show();
            window.Activate();
            _ = viewModel.RecognizeAsync();
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            Open();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(Open);
        }
    }

    private void OpenInEditor(PixelBuffer image)
    {
        // Der Editor bekommt eine Kopie als neues Dokument (wie eine normale Aufnahme).
        var document = new ImageDocument(image.Clone()) { IsDirty = true };
        _services.GetRequiredService<IEditorService>().Open(document);
    }
}
