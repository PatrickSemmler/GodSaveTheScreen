using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GodSaveTheScreen.App.ViewModels;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Emoji-Auswahl: Suchfeld, Kategorien, virtualisiertes Raster, Hautton-Leiste.</summary>
public partial class EmojiPicker : UserControl
{
    public EmojiPicker() => InitializeComponent();

    private EmojiPickerViewModel? ViewModel => DataContext as EmojiPickerViewModel;

    /// <summary><c>Strg+F</c> springt ins Suchfeld, <c>Esc</c> leert die Suche.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            FocusSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && ViewModel is { IsSearching: true } viewModel)
        {
            viewModel.SearchText = string.Empty;
            viewModel.ApplyFilterNow();
            e.Handled = true;
        }
    }

    /// <summary>Im Suchfeld: Pfeil nach unten wechselt ins Raster, <c>Enter</c> fügt das markierte (sonst erste) Emoji ein.</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down when viewModel.Cells.Count > 0:
                viewModel.SelectedCell ??= viewModel.Cells[0];
                FocusSelectedCell();
                e.Handled = true;
                break;
            case Key.Enter or Key.Return:
                viewModel.ApplyFilterNow();
                viewModel.InsertCommand.Execute(viewModel.SelectedCell ?? (viewModel.Cells.Count > 0 ? viewModel.Cells[0] : null));
                e.Handled = true;
                break;
        }
    }

    /// <summary>Im Raster: <c>Enter</c> fügt ein, Tippen springt ins Suchfeld.</summary>
    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return && ViewModel is { } viewModel)
        {
            viewModel.InsertCommand.Execute(viewModel.SelectedCell);
            e.Handled = true;
        }
    }

    private void OnGridTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text) || char.IsControl(e.Text[0]))
        {
            return;
        }

        SearchBox.Text += e.Text;
        FocusSearch();
        e.Handled = true;
    }

    /// <summary>Klick wählt das Emoji (bzw. tauscht das der Auswahl aus).</summary>
    private void OnGridMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is { } viewModel && FindCell(e.OriginalSource as DependencyObject) is { } cell)
        {
            viewModel.ChooseCommand.Execute(cell);
        }
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is { } viewModel && FindCell(e.OriginalSource as DependencyObject) is { } cell)
        {
            viewModel.InsertCommand.Execute(cell);
            e.Handled = true;
        }
    }

    private static EmojiCellViewModel? FindCell(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem { DataContext: EmojiCellViewModel cell })
            {
                return cell;
            }

            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    private void FocusSelectedCell()
    {
        CellList.UpdateLayout();
        if (CellList.SelectedItem is not null && CellList.ItemContainerGenerator.ContainerFromItem(CellList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
        }
        else
        {
            CellList.Focus();
        }
    }
}
