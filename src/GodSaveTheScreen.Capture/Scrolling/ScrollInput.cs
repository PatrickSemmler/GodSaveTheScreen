using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Eingaben per <c>SendInput</c>; was zusammengehört, steht in einem Aufruf, damit die Reihenfolge erhalten bleibt.</summary>
internal static class ScrollInput
{
    private const int WheelDelta = 120;

    /// <summary>Mausrad über <paramref name="x"/>/<paramref name="y"/>; der Zeiger kehrt danach an seine Stelle zurück.</summary>
    public static unsafe bool WheelAt(int x, int y, int notches)
    {
        if (!PInvoke.GetCursorPos(out var original))
        {
            return false;
        }

        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var width = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN));
        var height = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
        const MOUSE_EVENT_FLAGS move = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

        Span<INPUT> inputs =
        [
            Mouse(Absolute(x, left, width), Absolute(y, top, height), 0, move),
            Mouse(0, 0, unchecked((uint)(-WheelDelta * notches)), MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL),
            Mouse(Absolute(original.X, left, width), Absolute(original.Y, top, height), 0, move),
        ];
        return PInvoke.SendInput(inputs, sizeof(INPUT)) == inputs.Length;
    }

    /// <summary>Drückt und löst eine Taste, optional mit gehaltener Strg-Taste.</summary>
    public static unsafe bool Key(VIRTUAL_KEY key, bool control = false)
    {
        var list = new List<INPUT>();
        if (control)
        {
            list.Add(Keyboard(VIRTUAL_KEY.VK_CONTROL, up: false));
        }

        list.Add(Keyboard(key, up: false));
        list.Add(Keyboard(key, up: true));
        if (control)
        {
            list.Add(Keyboard(VIRTUAL_KEY.VK_CONTROL, up: true));
        }

        return PInvoke.SendInput(list.ToArray(), sizeof(INPUT)) == list.Count;
    }

    /// <summary>Oberstes Fenster (Wurzel) unter einem Punkt des virtuellen Desktops.</summary>
    public static HWND RootWindowAt(int x, int y)
    {
        var hwnd = PInvoke.WindowFromPoint(new System.Drawing.Point(x, y));
        return hwnd.IsNull ? HWND.Null : PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
    }

    internal static bool IsComFailure(Exception ex) =>
        ex is COMException or InvalidCastException or UnauthorizedAccessException or InvalidOperationException;

    private static int Absolute(int value, int origin, int extent) => (int)((long)(value - origin) * 65535 / (extent - 1));

    private static INPUT Mouse(int dx, int dy, uint data, MOUSE_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags };
        return input;
    }

    private static INPUT Keyboard(VIRTUAL_KEY key, bool up)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT { wVk = key, dwFlags = up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0 };
        return input;
    }
}
