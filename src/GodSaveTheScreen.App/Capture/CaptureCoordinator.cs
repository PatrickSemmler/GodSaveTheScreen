using System.Globalization;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Capture;

public enum FullScreenScope
{
    CurrentMonitor,
    AllMonitors,
}

/// <summary>Auftrag für eine Aufnahme. <see cref="Delay"/> = Timer vor dem Einfrieren.</summary>
public sealed record CaptureRequest(CaptureMode Mode, TimeSpan Delay = default, FullScreenScope Scope = FullScreenScope.CurrentMonitor)
{
    /// <summary>Auftrag mit dem Standard-Timer aus den Einstellungen (Hotkey, Tray).</summary>
    public static CaptureRequest FromSettings(AppSettings settings, CaptureMode mode) =>
        new(mode, TimeSpan.FromSeconds(Math.Max(0, settings.DefaultTimerSeconds)));
}

/// <summary>Startet Aufnahmen (vom Editor aus nutzbar, ohne den Koordinator zu kennen).</summary>
public interface ICaptureLauncher
{
    Task StartAsync(CaptureRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Steuert den Ablauf Timer → Snapshot → Overlays → Ergebnis → Nachaufnahme-Aktionen.
/// Es läuft immer nur eine Aufnahme; weitere Aufträge (z. B. Hotkey) werden währenddessen ignoriert.
/// </summary>
public sealed class CaptureCoordinator : ICaptureLauncher
{
    private const int RoundedCornerRadius = 8;
    private const int Windows11Build = 22000;

    private static readonly TimeSpan HideAnimationDelay = TimeSpan.FromMilliseconds(150);

    private readonly IScreenCaptureService _capture;
    private readonly IMonitorService _monitors;
    private readonly IWindowContentCapture _windowContent;
    private readonly IWindowVisibilityService _visibility;
    private readonly ICountdownService _countdown;
    private readonly ISelectionService _selection;
    private readonly IReadOnlyList<IPostCaptureAction> _actions;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CaptureCoordinator> _logger;
    private CancellationTokenSource? _cancellation;

    public CaptureCoordinator(
        IScreenCaptureService capture,
        IMonitorService monitors,
        IWindowContentCapture windowContent,
        IWindowVisibilityService visibility,
        ICountdownService countdown,
        ISelectionService selection,
        IEnumerable<IPostCaptureAction> actions,
        ISettingsService settings,
        IDialogService dialogs,
        ILogger<CaptureCoordinator> logger)
    {
        _capture = capture;
        _monitors = monitors;
        _windowContent = windowContent;
        _visibility = visibility;
        _countdown = countdown;
        _selection = selection;
        _actions = actions.ToList();
        _settings = settings;
        _dialogs = dialogs;
        _logger = logger;
    }

    public bool IsCapturing { get; private set; }

    /// <summary>Bricht eine laufende Aufnahme ab (Countdown oder Auswahl).</summary>
    public void CancelCurrent() => _cancellation?.Cancel();

    public async Task StartAsync(CaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellation = cancellation;
        var token = cancellation.Token;

        CaptureResultContext? result = null;
        var hidden = _visibility.HideAll();
        try
        {
            if (request.Delay > TimeSpan.Zero && !await _countdown.RunAsync(request.Delay, token))
            {
                _logger.LogInformation("Aufnahme während des Countdowns abgebrochen");
                return;
            }

            await Task.Delay(HideAnimationDelay, token);

            var currentMonitor = _monitors.GetMonitorAtCursor();
            var snapshot = _capture.CaptureVirtualScreen(_settings.Current.CaptureCursor);
            _logger.LogInformation(
                "Aufnahme {Mode}: Snapshot {Width}x{Height} bei ({Left},{Top}), {Monitors} Monitor(e), {Windows} Fenster",
                request.Mode, snapshot.Image.Width, snapshot.Image.Height, snapshot.VirtualLeft, snapshot.VirtualTop, snapshot.Monitors.Count, snapshot.Windows.Count);

            result = await CaptureAsync(request, snapshot, currentMonitor, token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Aufnahme abgebrochen");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme fehlgeschlagen");
            _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_CaptureFailed, ex.Message));
        }
        finally
        {
            hidden.Dispose();
            _cancellation = null;
            IsCapturing = false;
        }

        if (result is not null)
        {
            await RunActionsAsync(result);
        }
    }

    private async Task<CaptureResultContext?> CaptureAsync(
        CaptureRequest request, ScreenSnapshot snapshot, MonitorInfo currentMonitor, CancellationToken token)
    {
        PixelBuffer? image;
        string? windowTitle = null;

        if (request.Mode == CaptureMode.FullScreen)
        {
            image = request.Scope == FullScreenScope.AllMonitors ? snapshot.Image : snapshot.Crop(currentMonitor.Bounds);
        }
        else
        {
            var selection = await _selection.SelectAsync(snapshot, request.Mode, token);
            if (selection is null)
            {
                return null;
            }

            windowTitle = selection.Window?.Title;
            image = BuildImage(snapshot, selection);
        }

        if (image is not { Width: > 0, Height: > 0 })
        {
            return null;
        }

        var document = new ImageDocument(image, currentMonitor.DpiX, currentMonitor.DpiY)
        {
            IsDirty = true,
            Metadata = new DocumentMetadata(request.Mode.ToString(), windowTitle),
        };
        return new CaptureResultContext(document, request.Mode, windowTitle, DateTimeOffset.Now);
    }

    private PixelBuffer BuildImage(ScreenSnapshot snapshot, SelectionResult selection)
    {
        if (selection.Polygon is { } polygon)
        {
            var local = polygon.Select(p => new PointD(p.X - snapshot.VirtualLeft, p.Y - snapshot.VirtualTop)).ToList();
            return PolygonMask.Apply(snapshot.Image, local, _settings.Current.FreeformAntiAlias);
        }

        if (selection.Window is { } window)
        {
            return BuildWindowImage(snapshot, selection, window);
        }

        return snapshot.Crop(selection.Bounds);
    }

    private PixelBuffer BuildWindowImage(ScreenSnapshot snapshot, SelectionResult selection, WindowInfo window)
    {
        var settings = _settings.Current;
        PixelBuffer? image = null;
        if (settings.WindowSource == WindowSourceSetting.WindowContent)
        {
            image = _windowContent.TryCapture(window);
            if (image is null)
            {
                _logger.LogInformation("PrintWindow lieferte kein Bild für {Window}; Snapshot wird verwendet", window.Title);
            }
        }

        image ??= snapshot.Crop(selection.Bounds);

        if (settings.RoundedCornersTransparent && !window.IsMaximized && Environment.OSVersion.Version.Build >= Windows11Build)
        {
            var monitor = snapshot.Monitors.FirstOrDefault(m => m.Bounds.Contains(window.Bounds.X + (window.Bounds.Width / 2), window.Bounds.Y + (window.Bounds.Height / 2)));
            var scale = monitor?.Scale ?? 1.0;
            CornerMask.Apply(image, (int)Math.Round(RoundedCornerRadius * scale));
        }

        return image;
    }

    private async Task RunActionsAsync(CaptureResultContext result)
    {
        foreach (var action in _actions)
        {
            try
            {
                await action.ExecuteAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nachaufnahme-Aktion {Action} fehlgeschlagen", action.GetType().Name);
                _dialogs.ShowError(Strings.Error_Title, string.Format(CultureInfo.CurrentUICulture, Strings.Error_ActionFailed, ex.Message));
            }
        }
    }
}
