using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GodSaveTheScreen.Core.Hotkeys;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Nimmt eine Tastenkombination auf: fokussieren, Kombination drücken. <c>Entf</c>/<c>Rücktaste</c> löschen sie.
/// <see cref="Gesture"/> ist die serialisierte Form (<see cref="HotkeyGesture.ToString"/>), leer = nicht belegt.
/// </summary>
public sealed class HotkeyRecorder : TextBox
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture),
        typeof(string),
        typeof(HotkeyRecorder),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnGestureChanged));

    public HotkeyRecorder()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        ContextMenu = null;
        UpdateText();
    }

    public string Gesture
    {
        get => (string)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    private static void OnGestureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HotkeyRecorder)d).UpdateText();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        var modifiers = (HotkeyModifiers)(int)Keyboard.Modifiers;

        // Tab (Fokuswechsel) und Esc ohne Modifikator bleiben der Bedienung vorbehalten.
        if (modifiers == HotkeyModifiers.None && key is Key.Tab or Key.Escape)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (modifiers == HotkeyModifiers.None && key is Key.Delete or Key.Back)
        {
            Gesture = string.Empty;
            return;
        }

        var gesture = new HotkeyGesture(modifiers, key.ToString());
        if (gesture.IsValid)
        {
            Gesture = gesture.ToString();
        }
    }

    private void UpdateText() =>
        Text = HotkeyGesture.TryParse(Gesture)?.ToDisplayString() ?? string.Empty;
}
