using GodSaveTheScreen.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace GodSaveTheScreen.Capture;

public sealed unsafe class MonitorService : IMonitorService
{
    private const uint DefaultDpi = 96;
    private const uint MonitorInfoFlagPrimary = 1;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var handles = new List<HMONITOR>();
        MONITORENUMPROC callback = (HMONITOR monitor, HDC hdc, RECT* rect, LPARAM data) =>
        {
            handles.Add(monitor);
            return true;
        };
        PInvoke.EnumDisplayMonitors(HDC.Null, null, callback, 0);

        return handles.Select(Describe).OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Bounds.X).ToList();
    }

    public MonitorInfo GetMonitorAtCursor()
    {
        PInvoke.GetCursorPos(out var point);
        var handle = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return Describe(handle);
    }

    private static MonitorInfo Describe(HMONITOR monitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info);

        uint dpiX = DefaultDpi;
        uint dpiY = DefaultDpi;
        if (PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var x, out var y).Succeeded)
        {
            dpiX = x;
            dpiY = y;
        }

        return new MonitorInfo(
            (nint)monitor.Value,
            ToPixelRect(info.monitorInfo.rcMonitor),
            ToPixelRect(info.monitorInfo.rcWork),
            dpiX,
            dpiY,
            (info.monitorInfo.dwFlags & MonitorInfoFlagPrimary) != 0,
            info.szDevice.ToString());
    }

    private static PixelRect ToPixelRect(RECT r) => PixelRect.FromLtrb(r.left, r.top, r.right, r.bottom);
}
