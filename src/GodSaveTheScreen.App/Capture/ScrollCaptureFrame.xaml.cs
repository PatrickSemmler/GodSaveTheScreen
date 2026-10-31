using System.Windows;
using System.Windows.Interop;
using GodSaveTheScreen.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Rahmen um den Bereich der Scroll-Aufnahme. Er liegt außerhalb des Bereichs (also nie im Bild), nimmt weder Fokus noch Mausklicks
/// (<c>WS_EX_TRANSPARENT</c>) und wird zusätzlich von der Aufnahme ausgeschlossen.
/// </summary>
public partial class ScrollCaptureFrame : Window
{
    /// <summary>Breite des Rahmens in DIP; er wird entsprechend um den Bereich herum gelegt.</summary>
    public const double Thickness = 3;

    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;

    private PixelRect _bounds = PixelRect.Empty;

    public ScrollCaptureFrame()
    {
        InitializeComponent();
    }

    /// <summary>Legt den Rahmen um <paramref name="region"/> (Bildschirmpixel); <paramref name="scale"/> = DPI/96 des Monitors.</summary>
    public void Place(PixelRect region, double scale)
    {
        var border = Math.Max(1, (int)Math.Ceiling(Thickness * scale));
        _bounds = new PixelRect(region.X - border, region.Y - border, region.Width + (2 * border), region.Height + (2 * border));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WsExTransparent | WsExNoActivate | WsExToolWindow);
        if (_bounds.Width > 0)
        {
            PInvoke.SetWindowPos(hwnd, HWND.HWND_TOPMOST, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
    }
}
