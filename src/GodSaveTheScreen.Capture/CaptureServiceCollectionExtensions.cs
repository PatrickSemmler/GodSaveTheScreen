using GodSaveTheScreen.Capture.Scrolling;
using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.Capture;

public static class CaptureServiceCollectionExtensions
{
    public static IServiceCollection AddCapture(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IWindowEnumerator, WindowEnumerator>();
        services.AddSingleton<IWindowContentCapture, WindowContentCapture>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IScrollTargetResolver, ScrollTargetResolver>();
        services.AddSingleton<IScrollDriverSelector>(sp =>
            new ScrollDriverSelector(ScrollDriverSelector.DefaultFactories(), sp.GetService<Microsoft.Extensions.Logging.ILogger<ScrollDriverSelector>>()));
        return services;
    }
}
