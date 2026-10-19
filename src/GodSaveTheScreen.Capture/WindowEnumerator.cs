using System.Diagnostics;
using GodSaveTheScreen.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.Capture;

public sealed unsafe class WindowEnumerator : IWindowEnumerator
{
    private const int MinimumSize = 16;
    private const int ToolWindowStyle = 0x80;

    private readonly IMonitorService _monitors;

    public WindowEnumerator(IMonitorService monitors) => _monitors = monitors;

    public IReadOnlyList<WindowInfo> GetTopLevelWindows()
    {
        var handles = new List<HWND>();
        WNDENUMPROC callback = (HWND hwnd, LPARAM data) =>
        {
            handles.Add(hwnd);
            return true;
        };
        PInvoke.EnumWindows(callback, 0);

        var monitors = _monitors.GetMonitors();
        var ownProcess = (uint)Environment.ProcessId;
        var result = new List<WindowInfo>();
        for (var z = 0; z < handles.Count; z++)
        {
            if (Describe(handles[z], z, ownProcess, monitors) is { } info)
            {
                result.Add(info);
            }
        }

        return result;
    }

    private static WindowInfo? Describe(HWND hwnd, int zIndex, uint ownProcess, IReadOnlyList<MonitorInfo> monitors)
    {
        if (!PInvoke.IsWindowVisible(hwnd) || PInvoke.IsIconic(hwnd) || IsCloaked(hwnd))
        {
            return null;
        }

        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        if (processId == ownProcess)
        {
            return null;
        }

        var title = GetTitle(hwnd);
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (title.Length == 0 && (exStyle & ToolWindowStyle) != 0)
        {
            return null;
        }

        if (!TryGetBounds(hwnd, out var bounds) || bounds.Width < MinimumSize || bounds.Height < MinimumSize)
        {
            return null;
        }

        var maximized = PInvoke.IsZoomed(hwnd);
        if (maximized)
        {
            bounds = ClampToWorkArea(bounds, monitors);
        }

        return new WindowInfo((nint)hwnd.Value, title, GetProcessName(processId), bounds, maximized, zIndex);
    }

    private static bool IsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }

    /// <summary>Rahmen über <c>DWMWA_EXTENDED_FRAME_BOUNDS</c> (ohne unsichtbare Ränder), Fallback <c>GetWindowRect</c>.</summary>
    private static bool TryGetBounds(HWND hwnd, out PixelRect bounds)
    {
        RECT rect;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)sizeof(RECT));
        if (hr.Failed && !PInvoke.GetWindowRect(hwnd, out rect))
        {
            bounds = PixelRect.Empty;
            return false;
        }

        bounds = PixelRect.FromLtrb(rect.left, rect.top, rect.right, rect.bottom);
        return true;
    }

    private static PixelRect ClampToWorkArea(PixelRect bounds, IReadOnlyList<MonitorInfo> monitors)
    {
        var center = (X: bounds.X + (bounds.Width / 2), Y: bounds.Y + (bounds.Height / 2));
        var monitor = monitors.FirstOrDefault(m => m.Bounds.Contains(center.X, center.Y));
        if (monitor is null)
        {
            return bounds;
        }

        var clamped = bounds.Intersect(monitor.WorkArea);
        return clamped.IsEmpty ? bounds : clamped;
    }

    private static string GetTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            var copied = PInvoke.GetWindowText(hwnd, p, buffer.Length);
            return new string(p, 0, copied);
        }
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
