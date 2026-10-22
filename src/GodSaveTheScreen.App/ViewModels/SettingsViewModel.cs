using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Hotkeys;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.ViewModels;

public sealed record Choice<T>(string Label, T Value);

/// <summary>Zeile der Hotkey-Liste.</summary>
public sealed partial class HotkeyEntryViewModel : ObservableObject
{
    public HotkeyEntryViewModel(CaptureMode mode, string label, string gesture)
    {
        Mode = mode;
        Label = label;
        _gesture = gesture;
    }

    public CaptureMode Mode { get; }

    public string Label { get; }

    /// <summary>Serialisierte Form (siehe <see cref="HotkeyGesture"/>); leer = nicht belegt.</summary>
    [ObservableProperty]
    private string _gesture;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsProblem))]
    [NotifyPropertyChangedFor(nameof(ShowPrintHint))]
    private HotkeyStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsProblem))]
    private string? _message;

    public string StatusText => Message ?? Status switch
    {
        HotkeyStatus.Registered => Strings.Hotkey_Active,
        HotkeyStatus.Conflict => Strings.Hotkey_Conflict,
        HotkeyStatus.Invalid => Strings.Hotkey_Invalid,
        _ => string.Empty,
    };

    public bool IsProblem => Message is not null || Status is HotkeyStatus.Conflict or HotkeyStatus.Invalid;

    /// <summary>Die <c>Druck</c>-Taste ist belegt – meist durch das Snipping Tool von Windows.</summary>
    public bool ShowPrintHint => Status == HotkeyStatus.Conflict && HotkeyGesture.TryParse(Gesture)?.KeyName == "Snapshot";
}

/// <summary>
/// Einstellungen mit Sofortwirkung: jede Änderung geht in <see cref="ISettingsService.Current"/> und wird gespeichert.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAutostartService _autostart;
    private readonly IHotkeyService _hotkeys;
    private readonly IThemeService _theme;
    private readonly IAppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly IHelpService _help;
    private readonly string _startLanguage;

    public SettingsViewModel(
        ISettingsService settings,
        IAutostartService autostart,
        IHotkeyService hotkeys,
        IThemeService theme,
        IAppPaths paths,
        IDialogService dialogs,
        IHelpService help)
    {
        _settings = settings;
        _autostart = autostart;
        _hotkeys = hotkeys;
        _theme = theme;
        _paths = paths;
        _dialogs = dialogs;
        _help = help;
        _startLanguage = settings.Current.Language;

        Languages =
        [
            new(Strings.Settings_LanguageSystem, "System"),
            new("Deutsch", "de"),
            new("English", "en"),
        ];
        Themes =
        [
            new(Strings.Settings_ThemeSystem, ThemeSetting.System),
            new(Strings.Settings_ThemeLight, ThemeSetting.Light),
            new(Strings.Settings_ThemeDark, ThemeSetting.Dark),
        ];
        TrayActions =
        [
            new(Strings.Mode_Rectangle, TrayClickActionSetting.CaptureRectangle),
            new(Strings.Mode_Window, TrayClickActionSetting.CaptureWindow),
            new(Strings.Mode_Freeform, TrayClickActionSetting.CaptureFreeform),
            new(Strings.Mode_Fullscreen, TrayClickActionSetting.CaptureFullScreen),
            new(Strings.Tray_StartWindow, TrayClickActionSetting.ShowStartWindow),
            new(Strings.Settings_TrayNone, TrayClickActionSetting.None),
        ];
        Modes =
        [
            new(Strings.Mode_Rectangle, CaptureMode.Rectangle),
            new(Strings.Mode_Window, CaptureMode.Window),
            new(Strings.Mode_Freeform, CaptureMode.Freeform),
            new(Strings.Mode_Fullscreen, CaptureMode.FullScreen),
        ];
        WindowSources =
        [
            new(Strings.Settings_SourceSnapshot, WindowSourceSetting.Snapshot),
            new(Strings.Settings_SourceContent, WindowSourceSetting.WindowContent),
        ];
        Formats =
        [
            new("PNG", ImageFormatSetting.Png),
            new("JPEG", ImageFormatSetting.Jpeg),
            new("BMP", ImageFormatSetting.Bmp),
        ];
        TimerChoices =
        [
            new(Strings.Timer_None, 0),
            new(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 3), 3),
            new(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 5), 5),
            new(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 10), 10),
        ];

        Hotkeys = new ObservableCollection<HotkeyEntryViewModel>(
            new[] { CaptureMode.Rectangle, CaptureMode.Window, CaptureMode.Freeform, CaptureMode.FullScreen }
                .Select(mode => new HotkeyEntryViewModel(mode, CaptureModeNames.Get(mode), settings.Current.GetHotkey(mode))));
        foreach (var entry in Hotkeys)
        {
            entry.PropertyChanged += OnHotkeyEntryChanged;
        }

        _hotkeys.StatusChanged += (_, _) => RefreshHotkeyStatus();
        RefreshHotkeyStatus();
    }

    public IReadOnlyList<Choice<string>> Languages { get; }

    public IReadOnlyList<Choice<ThemeSetting>> Themes { get; }

    public IReadOnlyList<Choice<TrayClickActionSetting>> TrayActions { get; }

    public IReadOnlyList<Choice<CaptureMode>> Modes { get; }

    public IReadOnlyList<Choice<WindowSourceSetting>> WindowSources { get; }

    public IReadOnlyList<Choice<ImageFormatSetting>> Formats { get; }

    public IReadOnlyList<Choice<int>> TimerChoices { get; }

    public ObservableCollection<HotkeyEntryViewModel> Hotkeys { get; }

    // Allgemein

    public string Language
    {
        get => _settings.Current.Language;
        set
        {
            Update(s => s with { Language = value });
            OnPropertyChanged(nameof(RestartRequired));
        }
    }

    /// <summary>Sprachwechsel wirkt erst nach einem Neustart.</summary>
    public bool RestartRequired => !string.Equals(_settings.Current.Language, _startLanguage, StringComparison.Ordinal);

    public ThemeSetting Theme
    {
        get => _settings.Current.Theme;
        set
        {
            if (Update(s => s with { Theme = value }))
            {
                _theme.Apply();
            }
        }
    }

    public bool Autostart
    {
        get => _autostart.IsEnabled;
        set
        {
            if (_autostart.IsEnabled != value)
            {
                _autostart.SetEnabled(value);
            }

            Update(s => s with { Autostart = value });
            OnPropertyChanged();
        }
    }

    public bool StartInTray
    {
        get => _settings.Current.StartInTray;
        set => Update(s => s with { StartInTray = value });
    }

    public TrayClickActionSetting TrayClickAction
    {
        get => _settings.Current.TrayClickAction;
        set => Update(s => s with { TrayClickAction = value });
    }

    // Aufnahme

    public CaptureMode DefaultMode
    {
        get => _settings.Current.DefaultMode;
        set => Update(s => s with { DefaultMode = value });
    }

    public int DefaultTimerSeconds
    {
        get => _settings.Current.DefaultTimerSeconds;
        set => Update(s => s with { DefaultTimerSeconds = value });
    }

    public bool CaptureCursor
    {
        get => _settings.Current.CaptureCursor;
        set => Update(s => s with { CaptureCursor = value });
    }

    public WindowSourceSetting WindowSource
    {
        get => _settings.Current.WindowSource;
        set => Update(s => s with { WindowSource = value });
    }

    public bool RoundedCornersTransparent
    {
        get => _settings.Current.RoundedCornersTransparent;
        set => Update(s => s with { RoundedCornersTransparent = value });
    }

    public bool ExcludeOwnWindows
    {
        get => _settings.Current.ExcludeOwnWindows;
        set => Update(s => s with { ExcludeOwnWindows = value });
    }

    public bool ShowMagnifier
    {
        get => _settings.Current.ShowMagnifier;
        set => Update(s => s with { ShowMagnifier = value });
    }

    public bool CountdownSound
    {
        get => _settings.Current.CountdownSound;
        set => Update(s => s with { CountdownSound = value });
    }

    public bool FreeformAntiAlias
    {
        get => _settings.Current.FreeformAntiAlias;
        set => Update(s => s with { FreeformAntiAlias = value });
    }

    // Nach der Aufnahme

    public bool CopyToClipboard
    {
        get => _settings.Current.CopyToClipboard;
        set => Update(s => s with { CopyToClipboard = value });
    }

    public bool AutoSave
    {
        get => _settings.Current.AutoSave;
        set => Update(s => s with { AutoSave = value });
    }

    /// <summary>Wirksamer Zielordner (Standardordner, wenn nichts gewählt ist).</summary>
    public string AutoSaveDirectory
    {
        get => AutoSaveAction.ResolveDirectory(_settings.Current);
        set => Update(s => s with { AutoSaveDirectory = value ?? string.Empty });
    }

    public string FileNamePattern
    {
        get => _settings.Current.FileNamePattern;
        set => Update(s => s with { FileNamePattern = value ?? string.Empty });
    }

    public ImageFormatSetting AutoSaveFormat
    {
        get => _settings.Current.AutoSaveFormat;
        set => Update(s => s with { AutoSaveFormat = value });
    }

    // Info

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

    public string DataDirectory => _paths.DataDirectory;

    public string LogDirectory => _paths.LogDirectory;

    /// <summary>Copyright-Vermerk aus <c>Directory.Build.props</c> (<c>&lt;Copyright&gt;</c>).</summary>
    public string Copyright { get; } =
        typeof(SettingsViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyCopyrightAttribute), false)
            is [System.Reflection.AssemblyCopyrightAttribute { Copyright: { Length: > 0 } text }, ..] ? text : string.Empty;

    /// <summary>Entwickler aus <c>Directory.Build.props</c> (<c>&lt;Authors&gt;</c>).</summary>
    public string Author { get; } = AssemblyMetadata("Authors");

    /// <summary>Projektseite auf GitHub aus <c>Directory.Build.props</c> (<c>&lt;RepositoryUrl&gt;</c>).</summary>
    public string RepositoryUrl { get; } = AssemblyMetadata("RepositoryUrl");

    private static string AssemblyMetadata(string key) =>
        typeof(SettingsViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value ?? string.Empty;

    /// <summary>Hotkeys während einer Aufnahme im Recorder ausschalten, damit die Kombination nicht auslöst.</summary>
    public void SuspendHotkeys() => _hotkeys.SuspendHotkeys();

    public void ResumeHotkeys() => _hotkeys.ResumeHotkeys();

    [RelayCommand]
    private void BrowseAutoSaveDirectory()
    {
        if (_dialogs.PickFolder(AutoSaveDirectory) is { } folder)
        {
            AutoSaveDirectory = folder;
            OnPropertyChanged(nameof(AutoSaveDirectory));
        }
    }

    [RelayCommand]
    private void ResetFileNamePattern()
    {
        FileNamePattern = AppSettings.DefaultFileNamePattern;
        OnPropertyChanged(nameof(FileNamePattern));
    }

    [RelayCommand]
    private void OpenDataDirectory() => OpenFolder(DataDirectory);

    [RelayCommand]
    private void OpenLogDirectory() => OpenFolder(LogDirectory);

    [RelayCommand]
    private void ShowLicense() => _help.ShowLicense();

    [RelayCommand]
    private void OpenRepository()
    {
        if (Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            Shell(uri.AbsoluteUri);
    }

    [RelayCommand]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Wird von RelayCommand als Instanzmethode benötigt")]
    private void OpenWindowsKeyboardSettings() => Shell("ms-settings:easeofaccess-keyboard");

    /// <summary>Liefert den Modus, der dieselbe Kombination bereits nutzt, oder <c>null</c>.</summary>
    public static CaptureMode? FindConflict(IEnumerable<(CaptureMode Mode, string Gesture)> entries, CaptureMode mode, string gesture)
    {
        if (HotkeyGesture.TryParse(gesture) is not { } wanted)
        {
            return null;
        }

        foreach (var (otherMode, otherGesture) in entries)
        {
            if (otherMode != mode && HotkeyGesture.TryParse(otherGesture) == wanted)
            {
                return otherMode;
            }
        }

        return null;
    }

    private void OnHotkeyEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not HotkeyEntryViewModel entry || e.PropertyName != nameof(HotkeyEntryViewModel.Gesture))
        {
            return;
        }

        var normalized = HotkeyGesture.TryParse(entry.Gesture)?.ToString() ?? string.Empty;
        if (FindConflict(Hotkeys.Select(h => (h.Mode, h.Gesture)), entry.Mode, normalized) is { } other)
        {
            // Innerhalb der App doppelt belegt: alten Wert wiederherstellen.
            entry.PropertyChanged -= OnHotkeyEntryChanged;
            entry.Gesture = _settings.Current.GetHotkey(entry.Mode);
            entry.PropertyChanged += OnHotkeyEntryChanged;
            entry.Message = string.Format(CultureInfo.CurrentUICulture, Strings.Hotkey_UsedByApp, CaptureModeNames.Get(other));
            return;
        }

        entry.Message = null;
        Update(s => s.WithHotkey(entry.Mode, normalized));
        _hotkeys.Apply();
        RefreshHotkeyStatus();
    }

    private void RefreshHotkeyStatus()
    {
        foreach (var entry in Hotkeys)
        {
            entry.Status = _hotkeys.Statuses.TryGetValue(entry.Mode, out var status) ? status : HotkeyStatus.Unassigned;
        }
    }

    /// <summary>Übernimmt die Änderung sofort in die Einstellungen und speichert. Liefert <c>false</c>, wenn nichts geändert wurde.</summary>
    private bool Update(Func<AppSettings, AppSettings> change, [CallerMemberName] string? property = null)
    {
        var current = _settings.Current;
        var next = change(current);
        if (next == current)
        {
            return false;
        }

        _settings.Current = next;
        _ = _settings.SaveAsync();
        OnPropertyChanged(property);
        return true;
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Shell(path);
    }

    private static void Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nicht kritisch: der Link/Ordner lässt sich nicht öffnen.
            _ = ex;
        }
    }
}
