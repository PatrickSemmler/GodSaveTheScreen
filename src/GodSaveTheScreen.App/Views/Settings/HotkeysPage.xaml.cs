using System.Windows.Controls;
using System.Windows.Input;
using GodSaveTheScreen.App.Controls;
using GodSaveTheScreen.App.ViewModels;

namespace GodSaveTheScreen.App.Views.Settings;

public partial class HotkeysPage : Page
{
    public HotkeysPage()
    {
        InitializeComponent();

        // Während der Recorder eine Kombination aufnimmt, dürfen die globalen Hotkeys nicht auslösen.
        AddHandler(GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.OriginalSource is HotkeyRecorder && DataContext is SettingsViewModel vm)
            {
                vm.SuspendHotkeys();
            }
        }));
        AddHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.OriginalSource is HotkeyRecorder && DataContext is SettingsViewModel vm)
            {
                vm.ResumeHotkeys();
            }
        }));
        Unloaded += (_, _) => (DataContext as SettingsViewModel)?.ResumeHotkeys();
    }
}
