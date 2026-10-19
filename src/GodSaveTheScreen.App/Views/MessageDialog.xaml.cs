using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>Schaltfläche eines <see cref="MessageDialog"/>; <see cref="Result"/> wird als Antwort geliefert.</summary>
public sealed record DialogButton(string Label, int Result, bool IsPrimary = false, bool IsCancel = false);

public enum DialogKind
{
    Info,
    Warning,
    Error,
    Question,
}

/// <summary>Modaler Meldungsdialog im Stil der App (folgt Hell/Dunkel), Ersatz für <c>MessageBox</c>.</summary>
public partial class MessageDialog : FluentWindow
{
    private readonly int _cancelResult;

    public MessageDialog(string title, string message, DialogKind kind, IReadOnlyList<DialogButton> buttons)
    {
        InitializeComponent();
        Title = title;
        Bar.Title = title;
        MessageText.Text = message;
        (SymbolHost.Symbol, var color) = kind switch
        {
            DialogKind.Warning => (SymbolRegular.Warning24, Color.FromRgb(0xD9, 0x82, 0x00)),
            DialogKind.Error => (SymbolRegular.ErrorCircle24, Color.FromRgb(0xD3, 0x2F, 0x2F)),
            DialogKind.Question => (SymbolRegular.QuestionCircle24, Color.FromRgb(0x2E, 0x9E, 0x4F)),
            _ => (SymbolRegular.Info24, Color.FromRgb(0x3B, 0x91, 0xE8)),
        };
        SymbolHost.Foreground = new SolidColorBrush(color);
        _cancelResult = buttons.FirstOrDefault(b => b.IsCancel)?.Result ?? buttons[^1].Result;
        Result = _cancelResult;

        foreach (var definition in buttons)
        {
            var button = new Wpf.Ui.Controls.Button
            {
                Content = definition.Label,
                MinWidth = 88,
                Margin = new Thickness(8, 0, 0, 0),
                Appearance = definition.IsPrimary ? ControlAppearance.Primary : ControlAppearance.Secondary,
                IsDefault = definition.IsPrimary,
                IsCancel = definition.IsCancel,
            };
            button.Click += (_, _) =>
            {
                Result = definition.Result;
                Close();
            };
            Buttons.Children.Add(button);
        }
    }

    public int Result { get; private set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = _cancelResult;
            e.Handled = true;
            Close();
        }
    }
}
