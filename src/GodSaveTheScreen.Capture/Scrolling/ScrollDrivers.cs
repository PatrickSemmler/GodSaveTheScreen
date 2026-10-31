using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Mausrad über dem Bereich per <c>SendInput</c>; der Zeiger springt kurz hin und zurück. Wirkt in fast allen Programmen.</summary>
public sealed class WheelScrollDriver : IScrollDriver
{
    private const int ToStartNotches = 60;

    private ScrollTarget? _target;

    public string Name => "Wheel";

    public bool MovesCursor => true;

    public bool NeedsFocus => false;

    public bool? IsAtEnd => null;

    public bool TryPrepare(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var (x, y) = target.Center;

        // Liegt ein anderes Fenster über dem Bereich, käme das Rad dort an.
        if (ScrollInput.RootWindowAt(x, y) != new HWND(target.Window))
        {
            return false;
        }

        _target = target;
        return true;
    }

    public bool Scroll(int units)
    {
        if (_target is null)
        {
            return false;
        }

        var (x, y) = _target.Center;
        return ScrollInput.WheelAt(x, y, units);
    }

    public bool ScrollToStart() => Scroll(-ToStartNotches);

    public void Dispose()
    {
    }
}

/// <summary>Sendet <c>WM_VSCROLL</c> an das Steuerelement unter dem Bereich; wirkt bei klassischen Fenstern ohne Eingaben.</summary>
public sealed class MessageScrollDriver : IScrollDriver
{
    private const uint SbLineUp = 0;
    private const uint SbLineDown = 1;
    private const uint SbTop = 6;

    private HWND _control;

    public string Name => "Message";

    public bool MovesCursor => false;

    public bool NeedsFocus => false;

    public bool? IsAtEnd
    {
        get
        {
            var info = NewInfo();
            if (_control.IsNull || !PInvoke.GetScrollInfo(_control, SCROLLBAR_CONSTANTS.SB_VERT, ref info) || info.nMax <= info.nMin)
            {
                return null;
            }

            return info.nPos + (int)info.nPage > info.nMax;
        }
    }

    public bool TryPrepare(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var (x, y) = target.Center;
        var hwnd = PInvoke.WindowFromPoint(new System.Drawing.Point(x, y));
        for (var i = 0; i < 8 && !hwnd.IsNull; i++)
        {
            var info = NewInfo();
            if (PInvoke.GetScrollInfo(hwnd, SCROLLBAR_CONSTANTS.SB_VERT, ref info) && info.nMax > info.nMin)
            {
                _control = hwnd;
                return true;
            }

            if (hwnd == new HWND(target.Window))
            {
                break;
            }

            hwnd = PInvoke.GetParent(hwnd);
        }

        return false;
    }

    public bool Scroll(int units)
    {
        if (_control.IsNull)
        {
            return false;
        }

        var code = units > 0 ? SbLineDown : SbLineUp;
        for (var i = 0; i < Math.Abs(units); i++)
        {
            if (!PInvoke.PostMessage(_control, PInvoke.WM_VSCROLL, new WPARAM(code), default))
            {
                return false;
            }
        }

        return true;
    }

    public bool ScrollToStart() =>
        !_control.IsNull && PInvoke.PostMessage(_control, PInvoke.WM_VSCROLL, new WPARAM(SbTop), default);

    public void Dispose()
    {
    }

    private static SCROLLINFO NewInfo() =>
        new() { cbSize = (uint)Marshal.SizeOf<SCROLLINFO>(), fMask = SCROLLINFO_MASK.SIF_ALL };
}

/// <summary><c>Bild↓</c> bzw. <c>Strg+Pos1</c> an das Zielfenster; braucht den Fokus, stört die Sitzung also am meisten.</summary>
public sealed class KeyboardScrollDriver : IScrollDriver
{
    private ScrollTarget? _target;

    public string Name => "Keyboard";

    public bool MovesCursor => false;

    public bool NeedsFocus => true;

    public bool? IsAtEnd => null;

    public bool TryPrepare(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
        return true;
    }

    public bool Scroll(int units)
    {
        if (_target is null || !Focus())
        {
            return false;
        }

        var key = units > 0 ? VIRTUAL_KEY.VK_NEXT : VIRTUAL_KEY.VK_PRIOR;
        for (var i = 0; i < Math.Abs(units); i++)
        {
            if (!ScrollInput.Key(key))
            {
                return false;
            }
        }

        return true;
    }

    public bool ScrollToStart() => _target is not null && Focus() && ScrollInput.Key(VIRTUAL_KEY.VK_HOME, control: true);

    public void Dispose()
    {
    }

    private bool Focus() =>
        PInvoke.GetForegroundWindow() == new HWND(_target!.Window) || PInvoke.SetForegroundWindow(new HWND(_target.Window));
}
