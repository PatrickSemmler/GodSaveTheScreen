using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Farbwahl mit Alpha: Vorschaufeld, Popup mit Palette, zuletzt verwendeten Farben, Hex-Eingabe, „Keine“,
/// Pipette und dem PixiEditor-<c>StandardColorPicker</c>. <see cref="SelectedColor"/> = <c>null</c> bedeutet gemischte Werte.
/// </summary>
public partial class ColorButton : UserControl
{
    private static readonly ArgbColor[] Palette =
    [
        ArgbColor.FromRgb(0x00, 0x00, 0x00), ArgbColor.FromRgb(0xFF, 0xFF, 0xFF), ArgbColor.FromRgb(0x80, 0x80, 0x80), ArgbColor.FromRgb(0xC0, 0xC0, 0xC0),
        ArgbColor.FromRgb(0xFF, 0x00, 0x00), ArgbColor.FromRgb(0x99, 0x00, 0x00), ArgbColor.FromRgb(0xFF, 0x80, 0x00), ArgbColor.FromRgb(0xFF, 0xE0, 0x00),
        ArgbColor.FromRgb(0x00, 0xA0, 0x00), ArgbColor.FromRgb(0x00, 0x80, 0x80), ArgbColor.FromRgb(0x00, 0xBC, 0xD4), ArgbColor.FromRgb(0x00, 0x50, 0xFF),
        ArgbColor.FromRgb(0x00, 0x00, 0x80), ArgbColor.FromRgb(0x80, 0x00, 0xFF), ArgbColor.FromRgb(0xFF, 0x00, 0xFF), ArgbColor.FromRgb(0x80, 0x40, 0x00),
    ];

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(ArgbColor?), typeof(ColorButton),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly DependencyProperty RecentColorsProperty = DependencyProperty.Register(
        nameof(RecentColors), typeof(IEnumerable), typeof(ColorButton), new PropertyMetadata(null, OnRecentColorsChanged));

    public static readonly DependencyProperty PipetteCommandProperty = DependencyProperty.Register(
        nameof(PipetteCommand), typeof(ICommand), typeof(ColorButton));

    public static readonly DependencyProperty RememberCommandProperty = DependencyProperty.Register(
        nameof(RememberCommand), typeof(ICommand), typeof(ColorButton));

    private bool _syncing;
    private bool _changedByUser;

    public ColorButton()
    {
        InitializeComponent();
        BuildPalette();
        SwatchChecker.Fill = CreateChecker();
        UpdateDisplay();
    }

    public ArgbColor? SelectedColor
    {
        get => (ArgbColor?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public IEnumerable? RecentColors
    {
        get => (IEnumerable?)GetValue(RecentColorsProperty);
        set => SetValue(RecentColorsProperty, value);
    }

    /// <summary>Wird mit einer <c>Action&lt;ArgbColor&gt;</c> als Parameter aufgerufen; der Editor aktiviert dann die Pipette.</summary>
    public ICommand? PipetteCommand
    {
        get => (ICommand?)GetValue(PipetteCommandProperty);
        set => SetValue(PipetteCommandProperty, value);
    }

    /// <summary>Wird beim Schließen des Popups mit der gewählten Farbe aufgerufen (zuletzt verwendete Farben).</summary>
    public ICommand? RememberCommand
    {
        get => (ICommand?)GetValue(RememberCommandProperty);
        set => SetValue(RememberCommandProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColorButton)d).UpdateDisplay();

    private static void OnRecentColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColorButton)d).BuildRecent();

    private static DrawingBrush CreateChecker()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 2, 2))));
        group.Children.Add(new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        group.Children.Add(new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(1, 1, 1, 1))));
        return new DrawingBrush(group) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 8, 8), ViewportUnits = BrushMappingMode.Absolute };
    }

    private void UpdateDisplay()
    {
        var color = SelectedColor;
        MixedText.Visibility = color is null ? Visibility.Visible : Visibility.Collapsed;
        NoneSlash.Visibility = color is { A: 0 } ? Visibility.Visible : Visibility.Collapsed;
        SwatchColor.Fill = color is { } c ? BrushCache.GetBrush(c) : null;
        SwatchChecker.Visibility = color is null ? Visibility.Collapsed : Visibility.Visible;

        if (color is { } value)
        {
            HexBox.Text = value.ToString();
            if (!_syncing)
            {
                _syncing = true;
                Picker.SelectedColor = BrushCache.ToColor(value);
                _syncing = false;
            }
        }
        else
        {
            HexBox.Text = string.Empty;
        }
    }

    private void BuildPalette()
    {
        foreach (var color in Palette)
        {
            PalettePanel.Children.Add(CreateSwatch(color));
        }
    }

    private void BuildRecent()
    {
        RecentPanel.Children.Clear();
        var colors = RecentColors?.OfType<ArgbColor>().ToList() ?? [];
        foreach (var color in colors)
        {
            RecentPanel.Children.Add(CreateSwatch(color));
        }

        RecentLabel.Visibility = RecentPanel.Visibility = colors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private Button CreateSwatch(ArgbColor color)
    {
        var button = new Button
        {
            Style = (Style)Resources["SwatchStyle"],
            Background = BrushCache.GetBrush(color),
            Tag = color,
            ToolTip = color.ToString(),
        };
        AutomationProperties.SetName(button, color.ToString());
        button.Click += (_, _) => Commit(color);
        return button;
    }

    private void Commit(ArgbColor color)
    {
        _changedByUser = true;
        SelectedColor = color;
    }

    private void OnNoneClick(object sender, RoutedEventArgs e) => Commit(ArgbColor.Transparent);

    private void OnPickerColorChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        Commit(BrushCache.ToArgb(Picker.SelectedColor));
        _syncing = false;
    }

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHex();
            e.Handled = true;
        }
    }

    private void OnHexLostFocus(object sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (ArgbColor.TryParse(HexBox.Text, out var color) && color != SelectedColor)
        {
            Commit(color);
        }
        else
        {
            UpdateDisplay();
        }
    }

    private void OnPipetteClick(object sender, RoutedEventArgs e)
    {
        if (PipetteCommand is not { } command)
        {
            return;
        }

        Action<ArgbColor> picked = color => Dispatcher.BeginInvoke(() =>
        {
            Commit(color);
            if (RememberCommand is { } remember && remember.CanExecute(color))
            {
                remember.Execute(color);
            }
        });
        PickerPopup.IsOpen = false;
        if (command.CanExecute(picked))
        {
            command.Execute(picked);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_changedByUser && SelectedColor is { } color && RememberCommand is { } remember && remember.CanExecute(color))
        {
            remember.Execute(color);
        }

        _changedByUser = false;
    }
}
