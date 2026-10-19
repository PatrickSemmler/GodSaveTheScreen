using System.Windows;
using System.Windows.Controls;
using GodSaveTheScreen.App.ViewModels;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Eigenschaften der Auswahl bzw. der Werkzeug-Standardwerte (Form, Text, Marker) plus Ebenenliste.</summary>
public partial class PropertyPanel : UserControl
{
    public PropertyPanel() => InitializeComponent();

    /// <summary>Wahl aus der Größenliste sofort übernehmen (die editierbare Box übernimmt Eingaben erst beim Verlassen).</summary>
    private void OnFontSizeDropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ComboBox { SelectedItem: double size } && DataContext is EditorViewModel viewModel)
        {
            viewModel.TextProperties.FontSize = size;
        }
    }
}
