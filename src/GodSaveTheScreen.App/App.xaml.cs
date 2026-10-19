using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Localization;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GodSaveTheScreen.App;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Lebenszyklus über OnExit")]
public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceService? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire(e.Args))
        {
            Shutdown();
            return;
        }

        var paths = AppPaths.ForCurrentProcess();
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
        builder.Services
            .AddCore(paths)
            .AddCapture()
            .AddRendering()
            .AddAppServices();

        _host = builder.Build();
        var services = _host.Services;

        var settings = services.GetRequiredService<ISettingsService>().Current;
        levelSwitch.MinimumLevel = ToSerilog(settings.LogLevel);
        CultureInitializer.Apply(settings.Language);

        var logger = services.GetRequiredService<ILogger<App>>();
        logger.LogInformation(
            "GodSaveTheScreen {Version} gestartet (portabel: {Portable}, Daten: {Data})",
            typeof(App).Assembly.GetName().Version, paths.IsPortable, paths.DataDirectory);

        services.GetRequiredService<GlobalExceptionHandler>().Register(this);
        services.GetRequiredService<IThemeService>().Apply();

        var commandLine = services.GetRequiredService<CommandLineHandler>();
        WeakReferenceMessenger.Default.Register(commandLine);
        _singleInstance.StartServer(logger);

        await _host.StartAsync();

        var startWindow = services.GetRequiredService<IWindowFactory>().Create<StartWindow>();
        startWindow.Show();

        if (e.Args.Length > 0)
        {
            commandLine.Handle(e.Args);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static LogEventLevel ToSerilog(LogLevelSetting level) => level switch
    {
        LogLevelSetting.Verbose => LogEventLevel.Verbose,
        LogLevelSetting.Debug => LogEventLevel.Debug,
        LogLevelSetting.Warning => LogEventLevel.Warning,
        LogLevelSetting.Error => LogEventLevel.Error,
        _ => LogEventLevel.Information,
    };
}
