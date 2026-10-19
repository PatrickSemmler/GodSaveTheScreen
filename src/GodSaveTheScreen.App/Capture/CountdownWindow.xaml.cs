using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GodSaveTheScreen.Capture;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Kleines rundes Countdown-Fenster. Es nimmt nie den Fokus (ein offenes Menü der Zielanwendung bleibt offen);
/// <c>Esc</c> wird deshalb per <c>GetAsyncKeyState</c> abgefragt, ein Klick bricht ebenfalls ab.
/// </summary>
public partial class CountdownWindow : Window
{
    private const int VkEscape = 0x1B;
    private const int WsExNoActivate = 0x08000000;
    private const double EdgeMargin = 32;

    private readonly TimeSpan _delay;
    private readonly bool _sound;
    private readonly MonitorInfo _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly TaskCompletionSource<bool> _completion = new();
    private DateTime _end;
    private int _shown = -1;

    public CountdownWindow(TimeSpan delay, bool sound, MonitorInfo monitor)
    {
        _delay = delay;
        _sound = sound;
        _monitor = monitor;
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => Finish(false);
        _timer.Tick += (_, _) => OnTick();
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary><c>true</c> = abgelaufen, <c>false</c> = abgebrochen.</summary>
    public Task<bool> Completion => _completion.Task;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new HWND(new WindowInteropHelper(this).Handle);
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WsExNoActivate);

        // Unten rechts im Arbeitsbereich des Monitors, auf dem der Mauszeiger steht.
        var scale = _monitor.Scale;
        var work = _monitor.WorkArea;
        var size = (int)Math.Round(Width * scale);
        var margin = (int)Math.Round(EdgeMargin * scale);
        PInvoke.SetWindowPos(
            hwnd,
            HWND.HWND_TOPMOST,
            work.Right - size - margin,
            work.Bottom - size - margin,
            size,
            size,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    /// <summary>Zeigt das Fenster ohne Aktivierung und startet den Countdown.</summary>
    public void Start()
    {
        _end = DateTime.UtcNow + _delay;
        Show();
        OnTick();
        _timer.Start();
    }

    /// <summary>Bricht von außen ab (z. B. <see cref="CancellationToken"/>).</summary>
    public void Abort() => Finish(false);

    private void OnTick()
    {
        if ((PInvoke.GetAsyncKeyState(VkEscape) & 0x8000) != 0)
        {
            Finish(false);
            return;
        }

        var remaining = _end - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            Finish(true);
            return;
        }

        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        if (seconds == _shown)
        {
            return;
        }

        _shown = seconds;
        Digit.Text = seconds.ToString(CultureInfo.CurrentCulture);
        if (_sound && seconds <= 3)
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    private void Finish(bool completed)
    {
        _timer.Stop();
        _completion.TrySetResult(completed);
    }
}
