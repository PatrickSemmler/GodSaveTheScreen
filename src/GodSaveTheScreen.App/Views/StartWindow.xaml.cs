using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class StartWindow : FluentWindow
{
    private readonly StartViewModel _viewModel;

    public StartWindow(StartViewModel viewModel, IThemeService themes)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            await _viewModel.OpenFilesAsync(files);
        }
    }

    private void OnRecentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is RecentItemViewModel item && ItemsControl.ContainerFromElement(RecentList, (DependencyObject)e.OriginalSource) is not null)
        {
            _viewModel.OpenRecentCommand.Execute(item);
        }
    }

    private void OnRecentKeyDown(object sender, KeyEventArgs e)
    {
        if (RecentList.SelectedItem is not RecentItemViewModel item)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _viewModel.OpenRecentCommand.Execute(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            _viewModel.RemoveRecentCommand.Execute(item);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && Keyboard.FocusedElement is not TextBoxBase)
        {
            _viewModel.OpenFromClipboardCommand.Execute(null);
            e.Handled = true;
        }
    }
}
