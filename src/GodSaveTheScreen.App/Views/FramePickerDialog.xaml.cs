using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Rendering;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

/// <summary>Auswahl eines Einzelbilds aus einer Mehrbilddatei (animiertes GIF, mehrseitiges TIFF) mit Vorschaubildern.</summary>
public partial class FramePickerDialog : FluentWindow
{
    /// <summary>Höchstzahl angezeigter Einzelbilder (sehr lange Animationen).</summary>
    public const int MaxFrames = 500;

    public FramePickerDialog(ImageImport import, string fileName)
    {
        ArgumentNullException.ThrowIfNull(import);
        InitializeComponent();
        Title = Strings.Image_PickFrameTitle;
        Bar.Title = Strings.Image_PickFrameTitle;
        PromptText.Text = string.Format(CultureInfo.CurrentUICulture, Strings.Image_PickFramePrompt, fileName, import.FrameCount);
        var count = Math.Min(import.FrameCount, MaxFrames);
        var items = new List<FrameItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new FrameItem(i, string.Format(CultureInfo.CurrentUICulture, Strings.Image_FrameLabel, i + 1), import.Thumbnail(i, 96)));
        }

        Frames.ItemsSource = items;
        Frames.SelectedIndex = 0;
        Loaded += (_, _) =>
        {
            Frames.Focus();
            if (Frames.ItemContainerGenerator.ContainerFromIndex(0) is UIElement first)
            {
                first.Focus();
            }
        };
    }

    /// <summary>Gewähltes Einzelbild (0-basiert); <c>null</c> bei Abbruch.</summary>
    public int? Result { get; private set; }

    private void Accept()
    {
        if (Frames.SelectedItem is FrameItem item)
        {
            Result = item.Index;
            Close();
        }
    }

    private void OnOk(object sender, RoutedEventArgs e) => Accept();

    private void OnFrameDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private sealed record FrameItem(int Index, string Label, BitmapSource Thumbnail);
}
