using System.Globalization;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Hotkeys;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

/// <summary>Merkt sich, ob die Windows-Sitzung gesperrt oder getrennt ist (dann werden Aufnahme-Hotkeys ignoriert).</summary>
public interface ISessionState
{
    bool IsLocked { get; }
}

/// <inheritdoc cref="ISessionState"/>
public sealed class SessionState : ISessionState, IDisposable
{
    private volatile bool _locked;

    public SessionState() => SystemEvents.SessionSwitch += OnSessionSwitch;

    public bool IsLocked => _locked;

    public void Dispose() => SystemEvents.SessionSwitch -= OnSessionSwitch;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _locked = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionLogon:
                _locked = false;
                break;
        }
    }
}

/// <summary>Einmaliger Erststart-Hinweis: Hotkeys, Tray und die Taste <c>Druck</c>.</summary>
public sealed class FirstRunHint
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;

    public FirstRunHint(ISettingsService settings, IDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
    }

    /// <summary>Zeigt den Hinweis, falls er noch nie gezeigt wurde; <c>true</c>, wenn er angezeigt wurde.</summary>
    public bool ShowIfNeeded()
    {
        var settings = _settings.Current;
        if (settings.FirstRunHintShown)
        {
            return false;
        }

        _settings.Current = settings with { FirstRunHintShown = true };
        _ = _settings.SaveAsync();
        _dialogs.ShowInfo(Strings.FirstRun_Title, BuildMessage(settings));
        return true;
    }

    public static string BuildMessage(AppSettings settings)
    {
        string Display(CaptureMode mode) =>
            HotkeyGesture.TryParse(settings.GetHotkey(mode))?.ToDisplayString(CultureInfo.CurrentUICulture) ?? Strings.FirstRun_NotAssigned;

        return string.Format(
            CultureInfo.CurrentUICulture,
            Strings.FirstRun_Message,
            Display(CaptureMode.Rectangle),
            Display(CaptureMode.Window),
            Display(CaptureMode.Freeform),
            Display(CaptureMode.FullScreen));
    }
}
