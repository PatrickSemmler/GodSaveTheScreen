using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Storage.Xps;

namespace GodSaveTheScreen.Capture;

public sealed class WindowContentCapture : IWindowContentCapture
{
    /// <summary><c>PW_RENDERFULLCONTENT</c> (nicht Teil der CsWin32-Enum).</summary>
    private const PRINT_WINDOW_FLAGS RenderFullContent = (PRINT_WINDOW_FLAGS)2;

    public PixelBuffer? TryCapture(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var hwnd = new HWND(window.Handle);
        if (!PInvoke.GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var full = new PixelBuffer(width, height);
        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
        {
            return null;
        }

        HDC memDc = default;
        HBITMAP bitmap = default;
        try
        {
            memDc = PInvoke.CreateCompatibleDC(screenDc);
            bitmap = PInvoke.CreateCompatibleBitmap(screenDc, width, height);
            if (memDc.IsNull || bitmap.IsNull)
            {
                return null;
            }

            var previous = PInvoke.SelectObject(memDc, bitmap);
            try
            {
                if (!PInvoke.PrintWindow(hwnd, memDc, RenderFullContent))
                {
                    return null;
                }
            }
            finally
            {
                PInvoke.SelectObject(memDc, previous);
            }

            ScreenCaptureService.ReadPixels(memDc, bitmap, full);
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

        // Auf den sichtbaren Rahmen (ohne unsichtbare Ränder) zuschneiden.
        var frame = window.Bounds.Offset(-rect.left, -rect.top).Intersect(full.Bounds);
        if (frame.IsEmpty)
        {
            return null;
        }

        var cropped = full.Crop(frame);
        return IsBlank(cropped) ? null : cropped;
    }

    /// <summary>Komplett schwarz = App rendert nicht per <c>PrintWindow</c> (z. B. Hardwarebeschleunigung).</summary>
    internal static bool IsBlank(PixelBuffer image)
    {
        var data = image.Pixels;
        for (var i = 0; i < image.Width * image.Height * PixelBuffer.BytesPerPixel; i += PixelBuffer.BytesPerPixel)
        {
            if (data[i] != 0 || data[i + 1] != 0 || data[i + 2] != 0)
            {
                return false;
            }
        }

        return true;
    }
}
