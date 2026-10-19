using System.Windows;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

public interface IWindowFactory
{
    /// <summary>Erzeugt ein Fenster über DI; <paramref name="args"/> ergänzen nicht registrierte Konstruktorparameter.</summary>
    TWindow Create<TWindow>(params object[] args)
        where TWindow : Window;
}

public sealed class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _services;

    public WindowFactory(IServiceProvider services) => _services = services;

    public TWindow Create<TWindow>(params object[] args)
        where TWindow : Window
    {
        var window = ActivatorUtilities.CreateInstance<TWindow>(_services, args);
        window.Closed += OnWindowClosed;
        return window;
    }

    // Bis zum Tray-Betrieb (M3) endet die Anwendung mit dem letzten Fenster.
    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        var app = Application.Current;
        app?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () =>
            {
                if (app.Windows.Count == 0)
                {
                    app.Shutdown();
                }
            });
    }
}

public sealed class DialogService : IDialogService
{
    public void ShowError(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? PickSavePath(string suggestedFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
            Title = Strings.Editor_SaveAs,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
