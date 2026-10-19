using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Capture;

public sealed class CountdownService : ICountdownService
{
    private readonly IMonitorService _monitors;
    private readonly ISettingsService _settings;
    private readonly IWindowExclusionService _exclusion;

    public CountdownService(IMonitorService monitors, ISettingsService settings, IWindowExclusionService exclusion)
    {
        _monitors = monitors;
        _settings = settings;
        _exclusion = exclusion;
    }

    public async Task<bool> RunAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var window = new CountdownWindow(delay, _settings.Current.CountdownSound, _monitors.GetMonitorAtCursor());
        _exclusion.Register(window);
        using var registration = cancellationToken.Register(() => window.Dispatcher.BeginInvoke(window.Abort));
        window.Start();
        try
        {
            return await window.Completion;
        }
        finally
        {
            window.Close();
        }
    }
}
