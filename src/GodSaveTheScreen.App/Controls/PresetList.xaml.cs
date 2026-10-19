using System.Windows.Controls;
using System.Windows.Input;
using GodSaveTheScreen.App.ViewModels;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Vorlagenliste des Eigenschaftenpanels: Klick bzw. <c>Enter</c> wendet die Vorlage an.</summary>
public partial class PresetList : UserControl
{
    public PresetList() => InitializeComponent();

    private PresetListViewModel? ViewModel => DataContext as PresetListViewModel;

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: PresetItem item })
        {
            ViewModel?.ApplyCommand.Execute(item);
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && ViewModel is { SelectedItem: { } item } viewModel)
        {
            viewModel.ApplyCommand.Execute(item);
            e.Handled = true;
        }
    }
}
