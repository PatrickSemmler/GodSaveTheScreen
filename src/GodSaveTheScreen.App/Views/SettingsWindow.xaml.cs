using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views.Settings;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class SettingsWindow : FluentWindow
{
    private readonly IAppShell _shell;

    public SettingsWindow(SettingsViewModel viewModel, IThemeService themes, IAppShell shell)
    {
        _shell = shell;
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);

        Navigation.SetPageProviderService(new SettingsPageProvider(viewModel));
        Loaded += (_, _) => Navigation.Navigate(typeof(GeneralPage));
    }

    /// <summary>Hilfe-Abschnitt der gerade angezeigten Seite.</summary>
    public string CurrentHelpTopic => (Navigation.SelectedItem as NavigationViewItem)?.TargetPageType switch
    {
        { } t when t == typeof(CapturePage) => HelpTopics.SettingsCapture,
        { } t when t == typeof(PostCapturePage) => HelpTopics.SettingsPostCapture,
        { } t when t == typeof(HotkeysPage) => HelpTopics.SettingsHotkeys,
        { } t when t == typeof(AboutPage) => HelpTopics.SettingsAbout,
        _ => HelpTopics.SettingsGeneral,
    };

    private void OnHelpClick(object sender, System.Windows.RoutedEventArgs e) => _shell.ShowHelp(CurrentHelpTopic);

    /// <summary>F1 = Hilfe, Umschalt+F1 = Hilfe zur aktuellen Seite (Hotkey-Felder nehmen F1 selbst auf).</summary>
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.F1 || System.Windows.Input.Keyboard.FocusedElement is Controls.HotkeyRecorder)
        {
            return;
        }

        var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
        _shell.ShowHelp(shift ? CurrentHelpTopic : null);
        e.Handled = true;
    }

    /// <summary>Erzeugt die Seiten; alle teilen sich dasselbe <see cref="SettingsViewModel"/>.</summary>
    private sealed class SettingsPageProvider : INavigationViewPageProvider
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsPageProvider(SettingsViewModel viewModel) => _viewModel = viewModel;

        public object? GetPage(Type pageType)
        {
            if (Activator.CreateInstance(pageType) is System.Windows.Controls.Page page)
            {
                page.DataContext = _viewModel;
                return page;
            }

            return null;
        }
    }
}
