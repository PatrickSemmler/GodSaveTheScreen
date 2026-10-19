using System.Windows;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Capture;

public enum FullScreenScope
{
    CurrentMonitor,
    AllMonitors,
}

/// <summary>Steuert den Ablauf Snapshot → Overlays → Ergebnis. Es läuft immer nur eine Aufnahme.</summary>
public sealed class CaptureCoordinator
{
    private static readonly TimeSpan HideAnimationDelay = TimeSpan.FromMilliseconds(150);

    private readonly IScreenCaptureService _capture;
    private readonly IMonitorService _monitors;
    private readonly IWindowFactory _windows;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CaptureCoordinator> _logger;

    public CaptureCoordinator(
        IScreenCaptureService capture,
        IMonitorService monitors,
        IWindowFactory windows,
        ISettingsService settings,
        IDialogService dialogs,
        ILogger<CaptureCoordinator> logger)
    {
        _capture = capture;
        _monitors = monitors;
        _windows = windows;
        _settings = settings;
        _dialogs = dialogs;
        _logger = logger;
    }

    public bool IsCapturing { get; private set; }

    public async Task StartAsync(CaptureMode mode, FullScreenScope scope = FullScreenScope.CurrentMonitor)
    {
        if (IsCapturing)
        {
            return;
        }

        if (mode is not (CaptureMode.Rectangle or CaptureMode.FullScreen))
        {
            _logger.LogInformation("Aufnahmemodus {Mode} ist noch nicht verfügbar", mode);
            _dialogs.ShowError(Strings.Error_Title, Strings.Capture_ModeUnavailable);
            return;
        }

        IsCapturing = true;
        var hidden = HideVisibleWindows();
        ImageDocument? document = null;
        try
        {
            await Task.Delay(HideAnimationDelay);

            var currentMonitor = _monitors.GetMonitorAtCursor();
            var snapshot = _capture.CaptureVirtualScreen(_settings.Current.CaptureCursor);
            _logger.LogInformation("Aufnahme {Mode}: Snapshot {Width}x{Height} bei ({Left},{Top}), {Monitors} Monitor(e)",
                mode, snapshot.Image.Width, snapshot.Image.Height, snapshot.VirtualLeft, snapshot.VirtualTop, snapshot.Monitors.Count);

            PixelBuffer? result = mode switch
            {
                CaptureMode.FullScreen when scope == FullScreenScope.AllMonitors => snapshot.Image,
                CaptureMode.FullScreen => snapshot.Crop(currentMonitor.Bounds),
                _ => await SelectRectangleAsync(snapshot) is { } rect ? snapshot.Crop(rect) : null,
            };

            if (result is { Width: > 0, Height: > 0 })
            {
                document = new ImageDocument(result, currentMonitor.DpiX, currentMonitor.DpiY) { IsDirty = true };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme fehlgeschlagen");
            _dialogs.ShowError(Strings.Error_Title, string.Format(System.Globalization.CultureInfo.CurrentUICulture, Strings.Error_CaptureFailed, ex.Message));
        }
        finally
        {
            IsCapturing = false;
            foreach (var window in hidden)
            {
                window.Show();
            }
        }

        if (document is not null)
        {
            var editor = _windows.Create<EditorWindow>(document);
            editor.Show();
            editor.Activate();
        }
    }

    private Task<PixelRect?> SelectRectangleAsync(ScreenSnapshot snapshot)
    {
        var completion = new TaskCompletionSource<PixelRect?>();
        var state = new SelectionState(snapshot.VirtualBounds);
        var viewModel = new SelectionOverlayViewModel(state);
        var bitmap = snapshot.Image.ToBitmapSource();

        var overlays = snapshot.Monitors
            .Select(monitor => new SelectionOverlayWindow(monitor, bitmap, snapshot, viewModel))
            .ToList();

        viewModel.Finished += result =>
        {
            // Schließen erst nach Abschluss des aktuellen Eingabeereignisses.
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (var overlay in overlays)
                {
                    overlay.Close();
                }

                completion.TrySetResult(result);
            });
        };

        foreach (var overlay in overlays)
        {
            overlay.Show();
        }

        var cursorMonitor = _monitors.GetMonitorAtCursor();
        var active = overlays.FirstOrDefault(o => o.Monitor.Handle == cursorMonitor.Handle) ?? overlays[0];
        active.Activate();
        active.Focus();
        return completion.Task;
    }

    private static List<Window> HideVisibleWindows()
    {
        var hidden = Application.Current.Windows
            .OfType<Window>()
            .Where(w => w.IsVisible)
            .ToList();
        foreach (var window in hidden)
        {
            window.Hide();
        }

        return hidden;
    }
}
