using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Ergebnisfenster der Texterkennung: erkennt beim Öffnen im Hintergrund (abbrechbar), zeigt den Text bearbeitbar, erlaubt
/// einen neuen Lauf mit anderer Sprache bzw. anderen Spaltenoptionen, Kopieren und „Im Editor öffnen“.
/// </summary>
public sealed partial class OcrResultViewModel : ObservableObject, IDisposable
{
    private readonly IOcrService _ocr;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsService _settings;
    private readonly Action<PixelBuffer>? _openInEditor;
    private readonly ILogger? _logger;
    private CancellationTokenSource? _cancellation;
    private OcrResult? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Statistics))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isBusy;

    /// <summary>Kein sichtbarer Text erkannt (leeres Ergebnis).</summary>
    [ObservableProperty]
    private bool _isEmptyResult;

    /// <summary>Es ist keine Erkennungssprache installiert.</summary>
    [ObservableProperty]
    private bool _isNoLanguage;

    /// <summary>Fehlermeldung eines fehlgeschlagenen Laufs (<c>null</c> = keine).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Kurzer Hinweis (z. B. „Kopiert“).</summary>
    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private bool _separateColumns;

    private string _languageTag;

    public OcrResultViewModel(
        PixelBuffer image,
        IOcrService ocr,
        IClipboardService clipboard,
        ISettingsService settings,
        Action<PixelBuffer>? openInEditor = null,
        ILogger? logger = null)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        _ocr = ocr;
        _clipboard = clipboard;
        _settings = settings;
        _openInEditor = openInEditor;
        _logger = logger;
        _languageTag = settings.Current.OcrLanguage ?? string.Empty;
        Languages = BuildLanguages();
        _languageTag = Languages.Any(l => string.Equals(l.Value, _languageTag, StringComparison.OrdinalIgnoreCase)) ? _languageTag : string.Empty;
    }

    /// <summary>Das erkannte Bild (Quelle der Vorschau und für „Im Editor öffnen“).</summary>
    public PixelBuffer Image { get; }

    public ImageSource Preview
    {
        get
        {
            var source = Image.ToBitmapSource();
            source.Freeze();
            return source;
        }
    }

    public IReadOnlyList<Choice<string>> Languages { get; }

    /// <summary>Gewählte Sprache (leer = automatisch). Ein Wechsel wird gespeichert und startet die Erkennung neu.</summary>
    public string LanguageTag
    {
        get => _languageTag;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_languageTag, value, StringComparison.Ordinal))
            {
                return;
            }

            _languageTag = value;
            OnPropertyChanged();
            _settings.Current = _settings.Current with { OcrLanguage = value };
            _ = _settings.SaveAsync();
            _ = RecognizeAsync();
        }
    }

    public bool IsIdle => !IsBusy;

    public bool HasError => ErrorMessage is not null;

    /// <summary>Zeichen- und Zeilenzahl des aktuellen Texts.</summary>
    public string Statistics => string.Format(
        CultureInfo.CurrentUICulture,
        Strings.Ocr_Statistics,
        Text.Count(c => !char.IsWhiteSpace(c)),
        string.IsNullOrEmpty(Text) ? 0 : Text.Split('\n').Length);

    /// <summary>Erkennt den Text (neu); läuft im Hintergrund, ein laufender Durchgang wird abgebrochen.</summary>
    [RelayCommand]
    public async Task RecognizeAsync()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        IsEmptyResult = false;
        IsNoLanguage = false;
        ErrorMessage = null;
        Notice = null;
        try
        {
            if (_ocr.AvailableLanguages.Count == 0)
            {
                throw new OcrUnavailableException();
            }

            var result = await _ocr.RecognizeAsync(Image, string.IsNullOrEmpty(_languageTag) ? null : _languageTag, cancellation.Token);
            if (!ReferenceEquals(_cancellation, cancellation))
            {
                return;
            }

            _result = result;
            ApplyResult();
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                Notice = Strings.Ocr_Cancelled;
            }
        }
        catch (OcrUnavailableException)
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _result = null;
                Text = string.Empty;
                IsNoLanguage = true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or IOException or NotSupportedException or OutOfMemoryException)
        {
            _logger?.LogError(ex, "Texterkennung fehlgeschlagen");
            if (ReferenceEquals(_cancellation, cancellation))
            {
                ErrorMessage = string.Format(CultureInfo.CurrentUICulture, Strings.Ocr_Failed, ex.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>Bricht den laufenden Durchgang ab.</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    public void Cancel() => _cancellation?.Cancel();

    /// <summary>Kopiert den Text (nach Bearbeitung) in die Zwischenablage.</summary>
    [RelayCommand]
    public void Copy()
    {
        if (!string.IsNullOrEmpty(Text))
        {
            CopyText(Text);
        }
    }

    /// <summary>Kopiert einen Textausschnitt (Markierung) bzw. alles, wenn nichts markiert ist.</summary>
    public void CopyText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return;
        }

        try
        {
            _clipboard.SetText(text.ReplaceLineEndings("\r\n"));
            Notice = Strings.Ocr_Copied;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _logger?.LogWarning(ex, "Text konnte nicht in die Zwischenablage kopiert werden");
            ErrorMessage = string.Format(CultureInfo.CurrentUICulture, Strings.Ocr_Failed, ex.Message);
        }
    }

    [RelayCommand]
    private void OpenInEditor() => _openInEditor?.Invoke(Image);

    /// <summary>Öffnet die Windows-Einstellungen für Sprachpakete.</summary>
    [RelayCommand]
    private void OpenLanguageSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:regionlanguage") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Die Windows-Spracheinstellungen ließen sich nicht öffnen");
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    partial void OnSeparateColumnsChanged(bool value)
    {
        if (_result is not null)
        {
            ApplyResult();
        }
    }

    private void ApplyResult()
    {
        var result = _result;
        if (result is null)
        {
            return;
        }

        IsEmptyResult = result.IsEmpty;
        Text = result.IsEmpty ? string.Empty : OcrTextBuilder.Build(result.Lines, SeparateColumns);
    }

    private List<Choice<string>> BuildLanguages()
    {
        var list = new List<Choice<string>> { new(Strings.Ocr_LanguageAuto, string.Empty) };
        list.AddRange(_ocr.AvailableLanguages.Select(l => new Choice<string>(l.DisplayName, l.Tag)));
        return list;
    }
}
