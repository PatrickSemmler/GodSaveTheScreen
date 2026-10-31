using System.Windows;
using System.Windows.Interop;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Schließt eigene Fenster per <c>WDA_EXCLUDEFROMCAPTURE</c> von Aufnahmen aus (Windows 10 2004+).</summary>
public interface IWindowExclusionService
{
    /// <summary>Ob das Betriebssystem <c>WDA_EXCLUDEFROMCAPTURE</c> unterstützt (Build ≥ 19041).</summary>
    bool IsSupported { get; }

    /// <summary>Wendet die Einstellung auf das Fenster an (sobald es ein Handle hat) und hält es aktuell.</summary>
    void Register(Window window);

    /// <summary>
    /// Schließt das Fenster immer von Aufnahmen aus, unabhängig von der Einstellung (Steuerleiste und Rahmen der Scroll-Aufnahme
    /// würden sonst in jedem Teilbild landen). <c>false</c>, wenn das Betriebssystem das nicht kann oder der Aufruf fehlschlägt.
    /// </summary>
    bool RegisterAlwaysExcluded(Window window);
}

public sealed class WindowExclusionService : IWindowExclusionService
{
    private const int MinimumBuild = 19041;

    private readonly ISettingsService _settings;
    private readonly ILogger<WindowExclusionService> _logger;
    private readonly List<WeakReference<Window>> _windows = [];
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, object> _always = new();

    public WindowExclusionService(ISettingsService settings, ILogger<WindowExclusionService> logger)
    {
        _settings = settings;
        _logger = logger;
        _settings.Changed += (_, _) => ReapplyAll();
    }

    public bool IsSupported => Environment.OSVersion.Version.Build >= MinimumBuild;

    public void Register(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows.Add(new WeakReference<Window>(window));
        window.SourceInitialized += (_, _) => Apply(window);
        if (new WindowInteropHelper(window).Handle != 0)
        {
            Apply(window);
        }
    }

    public bool RegisterAlwaysExcluded(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!IsSupported)
        {
            return false;
        }

        _always.AddOrUpdate(window, new object());
        _ = new WindowInteropHelper(window).EnsureHandle();
        return Apply(window);
    }

    private void ReapplyAll()
    {
        _windows.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var reference in _windows.ToList())
        {
            if (reference.TryGetTarget(out var window) && new WindowInteropHelper(window).Handle != 0)
            {
                window.Dispatcher.BeginInvoke(() => Apply(window));
            }
        }
    }

    private bool Apply(Window window)
    {
        if (!IsSupported)
        {
            return false;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return false;
        }

        var affinity = _settings.Current.ExcludeOwnWindows || _always.TryGetValue(window, out _)
            ? WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE
            : WINDOW_DISPLAY_AFFINITY.WDA_NONE;
        if (!PInvoke.SetWindowDisplayAffinity(new HWND(handle), affinity))
        {
            _logger.LogWarning("SetWindowDisplayAffinity fehlgeschlagen für {Window}", window.GetType().Name);
            return false;
        }

        return true;
    }
}
