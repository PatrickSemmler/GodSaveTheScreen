using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Schmales Zahlenfeld mit gestapelten Pfeilen und optionaler Einheit im Feld (Eigenschaftenpanel).
/// Pfeiltasten/Pfeile = <see cref="SmallChange"/>, Bild↑/↓ = <see cref="LargeChange"/>, Mausrad nur bei Fokus.
/// Freie Eingabe wird bei Enter bzw. Verlassen übernommen (aktuelle Kultur, Punkt wird ebenfalls akzeptiert); Esc verwirft sie.
/// <see cref="Value"/> = <c>null</c> steht für gemischte Werte (leeres Feld).
/// </summary>
public partial class SpinBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double?), typeof(SpinBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((SpinBox)d).ShowValue()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(SpinBox), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SpinBox), new PropertyMetadata(100.0));

    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange), typeof(double), typeof(SpinBox), new PropertyMetadata(1.0));

    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange), typeof(double), typeof(SpinBox), new PropertyMetadata(10.0));

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(SpinBox), new PropertyMetadata(0, (d, _) => ((SpinBox)d).ShowValue()));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(SpinBox), new PropertyMetadata(string.Empty, (d, e) => ((SpinBox)d).UnitText.Text = (string?)e.NewValue ?? string.Empty));

    public SpinBox()
    {
        InitializeComponent();
        Input.PreviewKeyDown += OnInputKeyDown;
        Input.LostKeyboardFocus += (_, _) => Commit();
        PreviewMouseWheel += OnMouseWheel;
        ((FrameworkElement)UnitText.Parent).SizeChanged += (_, e) => Input.Padding = new Thickness(8, 0, e.NewSize.Width + 4, 0);
    }

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    /// <summary>Höchstzahl der Nachkommastellen (Anzeige und Rundung).</summary>
    public int Decimals
    {
        get => (int)GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    /// <summary>Einheit rechts im Feld (z. B. „px“, „°“, „%“).</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Geht um <paramref name="delta"/> weiter (von einem gemischten Wert aus ab dem Minimum bzw. 0).</summary>
    public void Step(double delta)
    {
        Commit();
        var start = Value ?? Math.Clamp(0, Minimum, Maximum);
        Apply(start + delta);
    }

    private void Apply(double value)
    {
        var clamped = Math.Round(Math.Clamp(value, Minimum, Maximum), Math.Clamp(Decimals, 0, 15));
        if (Value == clamped)
        {
            ShowValue();
            return;
        }

        Value = clamped;
    }

    private void Commit()
    {
        var text = Input.Text.Trim();
        if (text == Format(Value))
        {
            return;
        }

        if (TryParse(text, out var parsed))
        {
            Apply(parsed);
        }
        else
        {
            ShowValue();
        }
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private string Format(double? value) =>
        value is { } v ? v.ToString(Decimals > 0 ? "0." + new string('#', Decimals) : "0", CultureInfo.CurrentCulture) : string.Empty;

    private void ShowValue()
    {
        var caret = Input.CaretIndex;
        Input.Text = Format(Value);
        Input.CaretIndex = Math.Min(caret, Input.Text.Length);
    }

    private void OnUp(object sender, RoutedEventArgs e) => Step(SmallChange);

    private void OnDown(object sender, RoutedEventArgs e) => Step(-SmallChange);

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                Step(SmallChange);
                break;
            case Key.Down:
                Step(-SmallChange);
                break;
            case Key.PageUp:
                Step(LargeChange);
                break;
            case Key.PageDown:
                Step(-LargeChange);
                break;
            case Key.Enter:
                Commit();
                Input.SelectAll();
                break;
            case Key.Escape when Input.Text.Trim() != Format(Value):
                ShowValue();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Nur mit Fokus, damit das Scrollen im Panel keine Werte verstellt.</summary>
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Input.IsKeyboardFocusWithin)
        {
            return;
        }

        Step(Math.Sign(e.Delta) * SmallChange);
        e.Handled = true;
    }
}
