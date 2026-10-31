using System.Windows;
using System.Windows.Input;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>
/// Ergebnis der Texterkennung. <c>Esc</c> schließt, <c>F1</c> öffnet die Hilfe zu „Text erkennen“, <c>Strg+C</c> ohne
/// Markierung im Textfeld kopiert den ganzen Text.
/// </summary>
public partial class OcrResultWindow : FluentWindow
{
    private readonly IAppShell _shell;
    private readonly OcrResultViewModel _viewModel;

    public OcrResultWindow(OcrResultViewModel viewModel, IThemeService themes, IAppShell shell)
    {
        _shell = shell;
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.F1)
        {
            e.Handled = true;
            _shell.ShowHelp(HelpTopics.Ocr);
        }
        else if (e.Key == Key.C && modifiers == ModifierKeys.Control)
        {
            // Mit Markierung im Textfeld gilt das normale Kopieren; sonst (auch außerhalb des Textfelds) der ganze Text.
            if (ResultText.IsKeyboardFocused && ResultText.SelectionLength > 0)
            {
                return;
            }

            e.Handled = true;
            _viewModel.CopyText(ResultText.Text);
        }
    }
}
