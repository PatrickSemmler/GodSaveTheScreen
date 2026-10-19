using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.Capture;

public static class CaptureServiceCollectionExtensions
{
    public static IServiceCollection AddCapture(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        return services;
    }
}
