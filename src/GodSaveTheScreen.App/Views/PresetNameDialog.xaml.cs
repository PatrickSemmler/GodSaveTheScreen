using System.Windows;
using System.Windows.Controls;
using GodSaveTheScreen.Core.Settings;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>Eingabe eines Vorlagennamens; die Namensliste dient der Autovervollständigung.</summary>
public partial class PresetNameDialog : FluentWindow
{
    public PresetNameDialog(string title, string prompt, IReadOnlyList<string> existingNames, string initialName)
    {
        InitializeComponent();
        Title = title;
        Bar.Title = title;
        PromptText.Text = prompt;
        NameBox.ItemsSource = existingNames;
        NameBox.Text = initialName;
        NameBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => UpdateOk()));
        NameBox.SelectionChanged += (_, _) => UpdateOk();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            if (NameBox.Template.FindName("PART_EditableTextBox", NameBox) is System.Windows.Controls.TextBox box)
            {
                box.SelectAll();
            }
        };
        UpdateOk();
    }

    /// <summary>Eingegebener Name (bereinigt); <c>null</c> bei Abbruch.</summary>
    public string? Result { get; private set; }

    private void UpdateOk() => OkButton.IsEnabled = PresetNames.IsValid(NameBox.Text);

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!PresetNames.IsValid(NameBox.Text))
        {
            return;
        }

        Result = PresetNames.Clean(NameBox.Text);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
