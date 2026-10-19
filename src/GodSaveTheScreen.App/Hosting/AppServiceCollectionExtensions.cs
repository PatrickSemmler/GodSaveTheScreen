using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GodSaveTheScreen.App.Hosting;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddSingleton<IWindowFactory, WindowFactory>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<GlobalExceptionHandler>();
        services.AddSingleton<CaptureCoordinator>();
        services.AddSingleton<CommandLineHandler>();

        services.AddTransient<StartViewModel>();
        services.AddTransient<StartWindow>();
        return services;
    }
}
