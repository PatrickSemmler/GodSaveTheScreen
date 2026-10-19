using System.Windows.Input;
using GodSaveTheScreen.App.Capture;
using CaptureMode = GodSaveTheScreen.Core.Capture.CaptureMode;
using GodSaveTheScreen.Core.Hotkeys;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;

namespace GodSaveTheScreen.App.Services;

public enum HotkeyStatus
{
    /// <summary>Kein Hotkey konfiguriert.</summary>
    Unassigned,

    Registered,

    /// <summary>Bereits von Windows oder einer anderen Anwendung belegt.</summary>
    Conflict,

    /// <summary>Konfigurierte Kombination ist ungültig.</summary>
    Invalid,
}

public interface IHotkeyService
{
    IReadOnlyDictionary<CaptureMode, HotkeyStatus> Statuses { get; }

    event EventHandler? StatusChanged;

    /// <summary>Registriert alle Hotkeys neu entsprechend den Einstellungen.</summary>
    void Apply();

    /// <summary>Deaktiviert Hotkeys vorübergehend (z. B. während der Recorder eine Kombination aufnimmt).</summary>
    void SuspendHotkeys();

    void ResumeHotkeys();
}

public sealed class HotkeyService : IHotkeyService, IHostedService
{
    private static readonly CaptureMode[] Modes = [CaptureMode.Rectangle, CaptureMode.Window, CaptureMode.Freeform, CaptureMode.FullScreen];

    private readonly ISettingsService _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<HotkeyService> _logger;
    private readonly Dictionary<CaptureMode, HotkeyStatus> _statuses = Modes.ToDictionary(m => m, _ => HotkeyStatus.Unassigned);
    private string _applied = string.Empty;

    public HotkeyService(ISettingsService settings, IServiceProvider services, ILogger<HotkeyService> logger)
    {
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public event EventHandler? StatusChanged;

    public IReadOnlyDictionary<CaptureMode, HotkeyStatus> Statuses => _statuses;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _settings.Changed += OnSettingsChanged;
        Apply();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _settings.Changed -= OnSettingsChanged;
        foreach (var mode in Modes)
        {
            HotkeyManager.Current.Remove(Name(mode));
        }

        return Task.CompletedTask;
    }

    public void Apply()
    {
        var settings = _settings.Current;
        _applied = Signature(settings);
        foreach (var mode in Modes)
        {
            _statuses[mode] = Register(mode, settings.GetHotkey(mode));
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SuspendHotkeys() => HotkeyManager.Current.IsEnabled = false;

    public void ResumeHotkeys() => HotkeyManager.Current.IsEnabled = true;

    private static string Name(CaptureMode mode) => "GodSaveTheScreen." + mode;

    private static string Signature(AppSettings s) => string.Join('|', Modes.Select(s.GetHotkey));

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Signature(_settings.Current) != _applied)
        {
            Apply();
        }
    }

    private HotkeyStatus Register(CaptureMode mode, string text)
    {
        var name = Name(mode);
        HotkeyManager.Current.Remove(name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return HotkeyStatus.Unassigned;
        }

        var gesture = HotkeyGesture.TryParse(text);
        if (gesture is null || !Enum.TryParse<Key>(gesture.KeyName, out var key))
        {
            _logger.LogWarning("Ungültiger Hotkey für {Mode}: {Gesture}", mode, text);
            return HotkeyStatus.Invalid;
        }

        try
        {
            HotkeyManager.Current.AddOrReplace(name, key, (ModifierKeys)(int)gesture.Modifiers, (_, e) =>
            {
                e.Handled = true;
                OnHotkey(mode);
            });
            return HotkeyStatus.Registered;
        }
        catch (HotkeyAlreadyRegisteredException)
        {
            _logger.LogWarning("Hotkey {Gesture} für {Mode} ist bereits belegt", text, mode);
            return HotkeyStatus.Conflict;
        }
    }

    private async void OnHotkey(CaptureMode mode)
    {
        try
        {
            var coordinator = _services.GetRequiredService<CaptureCoordinator>();
            await coordinator.StartAsync(CaptureRequest.FromSettings(_settings.Current, mode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme per Hotkey ({Mode}) fehlgeschlagen", mode);
        }
    }
}
