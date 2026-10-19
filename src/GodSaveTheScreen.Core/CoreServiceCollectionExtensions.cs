using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services, IAppPaths? paths = null)
    {
        services.AddSingleton(paths ?? AppPaths.ForCurrentProcess());
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        return services;
    }
}
