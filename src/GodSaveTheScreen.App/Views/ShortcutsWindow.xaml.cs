using System.Windows.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>Übersicht aller Tastenkürzel (<c>F1</c>); <c>Esc</c> schließt.</summary>
public partial class ShortcutsWindow : FluentWindow
{
    public ShortcutsWindow(IReadOnlyList<ShortcutEntry> entries, IThemeService themes)
    {
        DataContext = new ShortcutsViewModel(entries);
        InitializeComponent();
        themes.Attach(this);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
