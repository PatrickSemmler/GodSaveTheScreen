using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class StartWindow : FluentWindow
{
    public StartWindow(StartViewModel viewModel, IThemeService themes)
    {
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);
    }
}
