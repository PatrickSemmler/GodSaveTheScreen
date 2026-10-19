using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Resources;

namespace GodSaveTheScreen.App.ViewModels;

public sealed record CaptureModeOption(string Label, CaptureMode Mode, FullScreenScope Scope);

public sealed partial class StartViewModel : ObservableObject
{
    private readonly CaptureCoordinator _coordinator;

    public StartViewModel(CaptureCoordinator coordinator)
    {
        _coordinator = coordinator;
        Modes =
        [
            new CaptureModeOption(Strings.Mode_Rectangle, CaptureMode.Rectangle, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_FullscreenMonitor, CaptureMode.FullScreen, FullScreenScope.CurrentMonitor),
            new CaptureModeOption(Strings.Mode_FullscreenAll, CaptureMode.FullScreen, FullScreenScope.AllMonitors),
        ];
        _selectedMode = Modes[0];
    }

    public IReadOnlyList<CaptureModeOption> Modes { get; }

    [ObservableProperty]
    private CaptureModeOption _selectedMode;

    [RelayCommand]
    private Task NewCaptureAsync() => _coordinator.StartAsync(SelectedMode.Mode, SelectedMode.Scope);
}
