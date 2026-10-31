using GodSaveTheScreen.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Bestimmt aus der Auswahl des Benutzers, was gescrollt werden soll.</summary>
public interface IScrollTargetResolver
{
    /// <summary>
    /// Ein Klick auf ein Fenster (<paramref name="window"/> ≠ 0) nimmt das größte scrollbare Element darin (sonst den Clientbereich);
    /// ein aufgezogener Bereich gilt, wie er ist. <c>null</c>, wenn der Bereich zu klein ist.
    /// </summary>
    Task<ScrollTarget?> ResolveAsync(PixelRect bounds, nint window, CancellationToken cancellationToken = default);

    /// <summary>Liegt das Zielfenster noch vorn über dem Bereich (sonst würden Eingaben und Aufnahme das falsche Fenster treffen)?</summary>
    bool IsInFront(ScrollTarget target);
}

public sealed class ScrollTargetResolver : IScrollTargetResolver
{
    /// <summary>Kleinste Seitenlänge, ab der Scrollen sinnvoll ist.</summary>
    public const int MinimumSide = 48;

    public async Task<ScrollTarget?> ResolveAsync(PixelRect bounds, nint window, CancellationToken cancellationToken = default)
    {
        PixelRect region;
        var handle = window;
        if (window != 0)
        {
            var (cx, cy) = (bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
            var found = await Task.Run(() => UiaScrollable.FindRegion(cx, cy, window), cancellationToken).ConfigureAwait(true);
            region = found ?? ClientArea(window) ?? bounds;
        }
        else
        {
            region = bounds;
            handle = (nint)RootWindowAtCenter(bounds);
        }

        region = region.Intersect(VirtualScreen());
        return region.Width < MinimumSide || region.Height < MinimumSide ? null : new ScrollTarget(handle, region);
    }

    public bool IsInFront(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Window == 0)
        {
            return true;
        }

        var (x, y) = target.Center;
        return ScrollInput.RootWindowAt(x, y) == new HWND(target.Window);
    }

    private static nint RootWindowAtCenter(PixelRect bounds)
    {
        var root = ScrollInput.RootWindowAt(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        return root.IsNull ? 0 : RootHandle(root);
    }

    private static unsafe nint RootHandle(HWND hwnd) => (nint)hwnd.Value;

    private static unsafe PixelRect? ClientArea(nint window)
    {
        var hwnd = new HWND(window);
        if (!PInvoke.GetClientRect(hwnd, out var client))
        {
            return null;
        }

        var origin = new System.Drawing.Point(0, 0);
        if (!PInvoke.ClientToScreen(hwnd, ref origin))
        {
            return null;
        }

        return new PixelRect(origin.X, origin.Y, client.right - client.left, client.bottom - client.top);
    }

    private static PixelRect VirtualScreen() => new(
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
}
