using System.Windows.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>Übersicht aller Tastenkürzel (<c>Strg+F1</c>); <c>Esc</c> schließt, <c>F1</c> öffnet die Hilfe zu den Tastenkürzeln.</summary>
public partial class ShortcutsWindow : FluentWindow
{
    private readonly IAppShell _shell;

    public ShortcutsWindow(IReadOnlyList<ShortcutEntry> entries, IThemeService themes, IAppShell shell)
    {
        _shell = shell;
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
        else if (e.Key == Key.F1)
        {
            e.Handled = true;
            _shell.ShowHelp(HelpTopics.Shortcuts);
        }
    }
}
