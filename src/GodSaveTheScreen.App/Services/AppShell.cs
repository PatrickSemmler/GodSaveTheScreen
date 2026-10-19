using System.Windows;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

public sealed class AppShell : IAppShell
{
    private readonly IWindowFactory _windows;
    private readonly IDialogService _dialogs;
    private readonly ILogger<AppShell> _logger;

    public AppShell(IWindowFactory windows, IDialogService dialogs, ILogger<AppShell> logger)
    {
        _windows = windows;
        _dialogs = dialogs;
        _logger = logger;
    }

    public void ShowStartWindow() => ShowSingle<StartWindow>();

    public void ShowSettings() => ShowSingle<SettingsWindow>();

    public void ExitApplication()
    {
        var unsaved = Application.Current.Windows
            .OfType<EditorWindow>()
            .Count(w => w.DataContext is EditorViewModel { Document.IsDirty: true });
        if (unsaved > 0 && !_dialogs.Confirm(Strings.Exit_Title, string.Format(System.Globalization.CultureInfo.CurrentUICulture, Strings.Exit_UnsavedEditors, unsaved)))
        {
            return;
        }

        _logger.LogInformation("Anwendung wird beendet");
        Application.Current.Shutdown();
    }

    private void ShowSingle<TWindow>()
        where TWindow : Window
    {
        var window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault() ?? _windows.Create<TWindow>();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
    }
}
