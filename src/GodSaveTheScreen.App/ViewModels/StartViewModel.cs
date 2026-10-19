using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.ViewModels;

public sealed record CaptureModeOption(string Label, CaptureMode Mode, FullScreenScope Scope);

/// <summary>Timer-Auswahl; <see cref="Seconds"/> = <c>null</c> steht für „Benutzerdefiniert…“.</summary>
public sealed record TimerOption(string Label, int? Seconds);

public sealed partial class StartViewModel : ObservableObject
{
    public const int MaxTimerSeconds = 3600;

    private readonly CaptureCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly IAppShell _shell;

    public StartViewModel(CaptureCoordinator coordinator, ISettingsService settings, IAppShell shell)
    {
        _coordinator = coordinator;
        _settings = settings;
        _shell = shell;

        Modes =
        [
            new CaptureModeOption(Strings.Mode_Rectangle, CaptureMode.Rectangle, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_Window, CaptureMode.Window, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_Freeform, CaptureMode.Freeform, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_FullscreenMonitor, CaptureMode.FullScreen, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_FullscreenAll, CaptureMode.FullScreen, FullScreenScope.AllMonitors),
        ];
        TimerOptions =
        [
            new TimerOption(Strings.Timer_None, 0),
            new TimerOption(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 3), 3),
            new TimerOption(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 5), 5),
            new TimerOption(string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, 10), 10),
            new TimerOption(Strings.Timer_Custom, null),
        ];

        var current = settings.Current;
        _selectedMode = Modes.First(m => m.Mode == current.DefaultMode);
        _customSeconds = Clamp(current.CustomTimerSeconds);

        var preset = TimerOptions.FirstOrDefault(t => t.Seconds == current.DefaultTimerSeconds);
        if (preset is null)
        {
            // Nicht voreingestellter Standard-Timer: als benutzerdefinierter Wert anzeigen.
            _customSeconds = Clamp(current.DefaultTimerSeconds);
            preset = TimerOptions[^1];
        }

        _selectedTimer = preset;
    }

    public IReadOnlyList<CaptureModeOption> Modes { get; }

    public IReadOnlyList<TimerOption> TimerOptions { get; }

    [ObservableProperty]
    private CaptureModeOption _selectedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomTimer))]
    [NotifyPropertyChangedFor(nameof(DelaySeconds))]
    private TimerOption _selectedTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DelaySeconds))]
    private int _customSeconds;

    public bool IsCustomTimer => SelectedTimer.Seconds is null;

    /// <summary>Verzögerung der nächsten Aufnahme in Sekunden.</summary>
    public int DelaySeconds => SelectedTimer.Seconds ?? CustomSeconds;

    /// <summary>Für die <c>NumberBox</c>: leere Eingabe wird zu 0, Werte werden auf 0–3600 begrenzt.</summary>
    public double? CustomSecondsInput
    {
        get => CustomSeconds;
        set => CustomSeconds = Clamp(value is { } v && !double.IsNaN(v) ? (int)Math.Round(v) : 0);
    }

    public static int Clamp(int seconds) => Math.Clamp(seconds, 0, MaxTimerSeconds);

    partial void OnCustomSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(CustomSecondsInput));
        _settings.Current = _settings.Current with { CustomTimerSeconds = value };
        _ = _settings.SaveAsync();
    }

    [RelayCommand]
    private Task NewCaptureAsync() =>
        _coordinator.StartAsync(new CaptureRequest(SelectedMode.Mode, TimeSpan.FromSeconds(DelaySeconds), SelectedMode.Scope));

    [RelayCommand]
    private void OpenSettings() => _shell.ShowSettings();
}
