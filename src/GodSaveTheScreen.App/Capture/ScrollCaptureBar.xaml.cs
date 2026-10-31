using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Steuerleiste der Scroll-Aufnahme: Status, bisherige Höhe, Vorschau, Umschalter Automatisch/Manuell, „Zuerst zum Anfang scrollen“,
/// „Fertig“ und „Abbrechen“. Das Fenster nimmt nie den Fokus (das Zielprogramm behält ihn) und steht außerhalb des Bereichs.
/// </summary>
public partial class ScrollCaptureBar : Window
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;
    private const double Gap = 12;

    private PixelRect _placement = PixelRect.Empty;
    private bool _suppressMode;
    private volatile bool _manual;

    public ScrollCaptureBar()
    {
        InitializeComponent();
    }

    public event Action? ModeChanged;

    public event Action? FinishRequested;

    public event Action? CancelRequested;

    public event Action<bool>? ToStartChanged;

    public bool ManualMode => _manual;

    public bool ToStartRequested { get; private set; }

    /// <summary>Berechnet die Lage in Bildschirmpixeln: rechts vom Bereich, sonst links, sonst in einer Ecke des Monitors darüber.</summary>
    public void Place(PixelRect region, MonitorInfo monitor)
    {
        var scale = monitor.Scale;
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var gap = (int)Math.Round(Gap * scale);
        var work = monitor.WorkArea;
        var y = Math.Clamp(region.Y, work.Y, Math.Max(work.Y, work.Bottom - height));

        PixelRect rect;
        if (region.Right + gap + width <= work.Right)
        {
            rect = new PixelRect(region.Right + gap, y, width, height);
        }
        else if (region.X - gap - width >= work.X)
        {
            rect = new PixelRect(region.X - gap - width, y, width, height);
        }
        else
        {
            // Der Bereich füllt den Monitor: die Leiste liegt in einer Ecke darüber (sie wird nicht mit aufgenommen).
            rect = new PixelRect(work.Right - width - gap, work.Y + gap, width, height);
        }

        _placement = rect;
    }

    public void SetToStart(bool value)
    {
        ToStartRequested = value;
        ToStart.IsChecked = value;
    }

    public void LockStartOption() => Dispatcher.BeginInvoke(() => ToStart.IsEnabled = false);

    public void SetManual(bool manual)
    {
        Dispatcher.Invoke(() =>
        {
            _suppressMode = true;
            try
            {
                ManualRadio.IsChecked = manual;
                AutoRadio.IsChecked = !manual;
                _manual = manual;
            }
            finally
            {
                _suppressMode = false;
            }
        });
    }

    public void SetStatus(string text) => Dispatcher.BeginInvoke(() => Status.Text = text);

    public void SetProgress(int height, int maxHeight) => Dispatcher.BeginInvoke(() =>
    {
        HeightText.Text = string.Format(CultureInfo.CurrentUICulture, Strings.Scroll_HeightFormat, height.ToString("N0", CultureInfo.CurrentCulture), maxHeight.ToString("N0", CultureInfo.CurrentCulture));
        Bar.Value = maxHeight > 0 ? Math.Min(1.0, (double)height / maxHeight) : 0;
    });

    public void SetPreview(PixelBuffer? preview)
    {
        var bitmap = preview?.ToBitmapSource();
        bitmap?.Freeze();
        Dispatcher.BeginInvoke(() => Preview.Source = bitmap);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WsExNoActivate | WsExToolWindow);
        if (_placement.Width > 0)
        {
            PInvoke.SetWindowPos(hwnd, HWND.HWND_TOPMOST, _placement.X, _placement.Y, _placement.Width, _placement.Height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressMode || ManualRadio is null)
        {
            return;
        }

        var manual = ManualRadio.IsChecked == true;
        if (manual == _manual)
        {
            return;
        }

        _manual = manual;
        ModeChanged?.Invoke();
    }

    private void OnToStartClick(object sender, RoutedEventArgs e)
    {
        ToStartRequested = ToStart.IsChecked == true;
        ToStartChanged?.Invoke(ToStartRequested);
    }

    private void OnFinishClick(object sender, RoutedEventArgs e) => FinishRequested?.Invoke();

    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();
}
