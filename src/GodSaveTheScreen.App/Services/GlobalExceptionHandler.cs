using System.Windows;
using System.Windows.Threading;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Loggt unbehandelte Ausnahmen und meldet sie über <see cref="IDialogService"/>.</summary>
public sealed class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IDialogService _dialogs;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IDialogService dialogs)
    {
        _logger = logger;
        _dialogs = dialogs;
    }

    public void Register(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Report(e.Exception, "DispatcherUnhandledException");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()), "AppDomain.UnhandledException");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    private void Report(Exception exception, string source)
    {
        _logger.LogError(exception, "Unbehandelte Ausnahme ({Source})", source);

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var message = string.Format(System.Globalization.CultureInfo.CurrentUICulture, Strings.Error_Unhandled, exception.Message);
        if (app.Dispatcher.CheckAccess())
        {
            _dialogs.ShowError(Strings.Error_Title, message);
        }
        else
        {
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => _dialogs.ShowError(Strings.Error_Title, message));
        }
    }
}
