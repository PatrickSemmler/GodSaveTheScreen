using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views.Settings;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel, IThemeService themes)
    {
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);

        Navigation.SetPageProviderService(new SettingsPageProvider(viewModel));
        Loaded += (_, _) => Navigation.Navigate(typeof(GeneralPage));
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
