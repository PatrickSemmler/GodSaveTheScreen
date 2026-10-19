using System.ComponentModel;
using GodSaveTheScreen.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.Capture;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly IMonitorService _monitors;
    private readonly IWindowEnumerator _windows;

    public ScreenCaptureService(IMonitorService monitors, IWindowEnumerator windows)
    {
        _monitors = monitors;
        _windows = windows;
    }

    public ScreenSnapshot CaptureVirtualScreen(bool includeCursor = false)
    {
        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Die Größe des virtuellen Desktops konnte nicht ermittelt werden.");
        }

        // Fensterliste zum selben Zeitpunkt wie das Abbild.
        var windows = _windows.GetTopLevelWindows();

        var buffer = new PixelBuffer(width, height);
        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
        {
            throw new Win32Exception();
        }

        HDC memDc = default;
        HBITMAP bitmap = default;
        try
        {
            memDc = PInvoke.CreateCompatibleDC(screenDc);
            bitmap = PInvoke.CreateCompatibleBitmap(screenDc, width, height);
            if (memDc.IsNull || bitmap.IsNull)
            {
                throw new Win32Exception();
            }

            var previous = PInvoke.SelectObject(memDc, bitmap);
            try
            {
                if (!PInvoke.BitBlt(memDc, 0, 0, width, height, screenDc, left, top, ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT))
                {
                    throw new Win32Exception();
                }

                if (includeCursor)
                {
                    DrawCursor(memDc, left, top);
                }
            }
            finally
            {
                PInvoke.SelectObject(memDc, previous);
            }

            ReadPixels(memDc, bitmap, buffer);
        }
        finally
        {
            if (!bitmap.IsNull)
            {
                PInvoke.DeleteObject(bitmap);
            }

            if (!memDc.IsNull)
            {
                PInvoke.DeleteDC(memDc);
            }

            _ = PInvoke.ReleaseDC(HWND.Null, screenDc);
        }

        return new ScreenSnapshot(buffer, left, top, _monitors.GetMonitors(), windows);
    }

    internal static unsafe void ReadPixels(HDC dc, HBITMAP bitmap, PixelBuffer buffer)
    {
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = buffer.Width;
        info.bmiHeader.biHeight = -buffer.Height; // Top-Down
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0; // BI_RGB

        fixed (byte* pixels = buffer.Pixels)
        {
            var lines = PInvoke.GetDIBits(dc, bitmap, 0, (uint)buffer.Height, pixels, &info, DIB_USAGE.DIB_RGB_COLORS);
            if (lines == 0)
            {
                throw new Win32Exception();
            }
        }

        // GDI liefert Alpha = 0; das Bild ist undurchsichtig.
        var data = buffer.Pixels;
        for (var i = 3; i < data.Length; i += PixelBuffer.BytesPerPixel)
        {
            data[i] = 255;
        }
    }

    private static unsafe void DrawCursor(HDC dc, int virtualLeft, int virtualTop)
    {
        var cursor = new CURSORINFO { cbSize = (uint)sizeof(CURSORINFO) };
        if (!PInvoke.GetCursorInfo(ref cursor) || (cursor.flags & CURSORINFO_FLAGS.CURSOR_SHOWING) == 0)
        {
            return;
        }

        PInvoke.DrawIconEx(
            dc,
            cursor.ptScreenPos.X - virtualLeft,
            cursor.ptScreenPos.Y - virtualTop,
            cursor.hCursor,
            0,
            0,
            0,
            HBRUSH.Null,
            DI_FLAGS.DI_NORMAL);
    }
}
