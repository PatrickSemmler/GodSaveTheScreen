using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GodSaveTheScreen.App.ViewModels;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Ebenenliste: Auswahl synchron zum Dokument, Umsortieren per Drag &amp; Drop, Umbenennen per Doppelklick.</summary>
public partial class LayersPanel : UserControl
{
    private const string DragFormat = "GodSaveTheScreen.Layer";

    private bool _syncing;
    private Point _dragStart;
    private LayerItem? _dragItem;
    private LayersViewModel? _viewModel;

    public LayersPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as LayersViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SelectionChanged += OnViewModelSelectionChanged;
            SyncFromViewModel();
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectionChanged -= OnViewModelSelectionChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelSelectionChanged(object? sender, EventArgs e) => SyncFromViewModel();

    private void SyncFromViewModel()
    {
        if (_viewModel is null || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            List.SelectedItems.Clear();
            var selected = _viewModel.SelectedAnnotations.ToHashSet();
            foreach (var item in _viewModel.Items.Where(i => selected.Contains(i.Annotation)))
            {
                List.SelectedItems.Add(item);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _viewModel is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            _viewModel.SetSelection(List.SelectedItems.OfType<LayerItem>().Select(i => i.Annotation));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(List);
        _dragItem = ItemAt(e.OriginalSource as DependencyObject);
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
        {
            return;
        }

        var position = e.GetPosition(List);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(List, new DataObject(DragFormat, item), DragDropEffects.Move);
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null || e.Data.GetData(DragFormat) is not LayerItem dragged)
        {
            return;
        }

        var target = ItemAt(e.OriginalSource as DependencyObject);
        var index = target is null ? _viewModel.Items.Count - 1 : _viewModel.Items.IndexOf(target);
        _viewModel.MoveTo(dragged.Annotation, index);
        e.Handled = true;
    }

    private LayerItem? ItemAt(DependencyObject? source)
    {
        var container = source is null ? null : ItemsControl.ContainerFromElement(List, source) as ListBoxItem;
        return container?.DataContext as LayerItem;
    }

    private void OnNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not TextBlock label || label.Parent is not Grid grid || label.DataContext is not LayerItem item)
        {
            return;
        }

        var editor = grid.Children.OfType<TextBox>().First();
        editor.Text = item.DisplayName;
        editor.Visibility = Visibility.Visible;
        label.Visibility = Visibility.Collapsed;
        editor.Focus();
        editor.SelectAll();
        e.Handled = true;
    }

    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox editor)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Finish(editor, commit: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Finish(editor, commit: false);
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Visibility: Visibility.Visible } editor)
        {
            Finish(editor, commit: true);
        }
    }

    private void Finish(TextBox editor, bool commit)
    {
        if (editor.Parent is not Grid grid)
        {
            return;
        }

        var label = grid.Children.OfType<TextBlock>().First();
        editor.Visibility = Visibility.Collapsed;
        label.Visibility = Visibility.Visible;
        if (commit && editor.DataContext is LayerItem item && _viewModel is not null)
        {
            _viewModel.Rename(item, editor.Text);
        }
    }
}
