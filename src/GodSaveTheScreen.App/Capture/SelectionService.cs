using System.Windows;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Zeigt je Monitor ein <see cref="SelectionOverlayWindow"/> und liefert die gemeinsame Auswahl.</summary>
public sealed class SelectionService : ISelectionService
{
    private readonly IMonitorService _monitors;
    private readonly ISettingsService _settings;

    public SelectionService(IMonitorService monitors, ISettingsService settings)
    {
        _monitors = monitors;
        _settings = settings;
    }

    public Task<SelectionResult?> SelectAsync(ScreenSnapshot snapshot, CaptureMode mode, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<SelectionResult?>();
        var state = new SelectionState(snapshot.VirtualBounds);
        var viewModel = new SelectionOverlayViewModel(state, mode, snapshot.Windows);
        var bitmap = snapshot.Image.ToBitmapSource();
        var showMagnifier = _settings.Current.ShowMagnifier;

        var overlays = snapshot.Monitors
            .Select(monitor => new SelectionOverlayWindow(monitor, bitmap, snapshot, viewModel, showMagnifier))
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
        cancellationToken.Register(() => Application.Current.Dispatcher.BeginInvoke(viewModel.Cancel));

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
}
