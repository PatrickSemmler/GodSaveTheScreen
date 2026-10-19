using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton<IWindowExclusionService, WindowExclusionService>();
        services.AddSingleton<IWindowVisibilityService, WindowVisibilityService>();
        services.AddSingleton<ICountdownService, CountdownService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<IAutostartRegistry, RunKeyAutostartRegistry>();
        services.AddSingleton<IAutostartService, AutostartService>();
        services.AddSingleton<IAppShell, AppShell>();

        // Reihenfolge der Nachaufnahme-Aktionen ist fest: Zwischenablage, Auto-Speichern, Editor.
        services.AddSingleton<IPostCaptureAction, CopyToClipboardAction>();
        services.AddSingleton<IPostCaptureAction, AutoSaveAction>();
        services.AddSingleton<IPostCaptureAction, OpenEditorAction>();
        services.AddSingleton<CaptureCoordinator>();

        services.AddSingleton<TrayService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<TrayService>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TrayService>());
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<IHotkeyService>(sp => sp.GetRequiredService<HotkeyService>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HotkeyService>());

        services.AddSingleton<CommandLineHandler>();

        services.AddTransient<StartViewModel>();
        services.AddTransient<StartWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
        return services;
    }
}
