using System.Runtime.InteropServices;
using GodSaveTheScreen.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Findet das größte senkrecht scrollbare UI-Automation-Element an einem Punkt.</summary>
public static class UiaScrollable
{
    private const int MaxDepth = 24;
    private const int Attempts = 3;
    private const int RetryMilliseconds = 500;

    /// <summary>Bereich des größten scrollbaren Elements unter dem Punkt (innerhalb des Fensters) oder <c>null</c>.</summary>
    public static PixelRect? FindRegion(int x, int y, nint window)
    {
        IUIAutomation? automation = null;
        try
        {
            automation = (IUIAutomation)new CUIAutomation();
            return Find(automation, x, y, window)?.Region;
        }
        catch (Exception ex) when (ScrollInput.IsComFailure(ex))
        {
            return null;
        }
        finally
        {
            Release(automation);
        }
    }

    /// <summary>Sucht mehrmals: Browser schalten ihre Bedienungshilfen erst nach der ersten Anfrage eines UIA-Clients ein.</summary>
    internal static Found? Find(IUIAutomation automation, int x, int y, nint window)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (FindOnce(automation, x, y, window) is { } found)
            {
                return found;
            }

            if (attempt < Attempts - 1)
            {
                Thread.Sleep(RetryMilliseconds);
            }
        }

        return null;
    }

    private static Found? FindOnce(IUIAutomation automation, int x, int y, nint window)
    {
        var element = automation.ElementFromPoint(new System.Drawing.Point(x, y));
        var walker = automation.ControlViewWalker;
        Found? best = null;
        for (var depth = 0; element is not null && depth < MaxDepth; depth++)
        {
            if (TryScrollable(element, window, out var found)
                && (best is null || found.Region.Width * found.Region.Height > best.Region.Width * best.Region.Height))
            {
                best = found;
            }

            element = walker.GetParentElement(element);
        }

        return best;
    }

    internal static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
        {
            Marshal.ReleaseComObject(com);
        }
    }

    private static bool TryScrollable(IUIAutomationElement element, nint window, out Found found)
    {
        found = null!;
        object? pattern;
        try
        {
            pattern = element.GetCurrentPattern(UIA_PATTERN_ID.UIA_ScrollPatternId);
        }
        catch (Exception ex) when (ScrollInput.IsComFailure(ex))
        {
            return false;
        }

        if (pattern is not IUIAutomationScrollPattern scroll || !scroll.CurrentVerticallyScrollable)
        {
            return false;
        }

        var rect = element.CurrentBoundingRectangle;
        var region = new PixelRect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
        if (region.Width < 16 || region.Height < 16)
        {
            return false;
        }

        if (window != 0 && PInvoke.GetWindowRect(new HWND(window), out var win))
        {
            var bounds = new PixelRect(win.left, win.top, win.right - win.left, win.bottom - win.top);
            var inside = region.Intersect(bounds);
            if (inside.Width * inside.Height < region.Width * region.Height / 2)
            {
                return false;
            }

            region = inside;
        }

        found = new Found(scroll, region);
        return true;
    }

    internal sealed record Found(IUIAutomationScrollPattern Pattern, PixelRect Region);
}

/// <summary>Scrollt über das <c>ScrollPattern</c> der UI-Automation: keine Zeigerbewegung, kennt den Endpunkt. Browser melden es nur, wenn sie Bedienungshilfen erkennen.</summary>
public sealed class UiaScrollDriver : IScrollDriver
{
    private const double NoScroll = -1;
    private const double EndPercent = 99.5;

    private IUIAutomation? _automation;
    private IUIAutomationScrollPattern? _pattern;

    public string Name => "Uia";

    public bool MovesCursor => false;

    public bool NeedsFocus => false;

    public bool? IsAtEnd
    {
        get
        {
            try
            {
                var percent = _pattern?.CurrentVerticalScrollPercent ?? NoScroll;
                return percent < 0 ? null : percent >= EndPercent;
            }
            catch (Exception ex) when (ScrollInput.IsComFailure(ex))
            {
                return null;
            }
        }
    }

    public bool TryPrepare(ScrollTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        try
        {
            _automation = (IUIAutomation)new CUIAutomation();
            var (x, y) = target.Center;
            _pattern = UiaScrollable.Find(_automation, x, y, target.Window)?.Pattern;
            return _pattern is not null;
        }
        catch (Exception ex) when (ScrollInput.IsComFailure(ex))
        {
            return false;
        }
    }

    public bool Scroll(int units)
    {
        if (_pattern is null)
        {
            return false;
        }

        try
        {
            var amount = units > 0 ? ScrollAmount.ScrollAmount_SmallIncrement : ScrollAmount.ScrollAmount_SmallDecrement;
            for (var i = 0; i < Math.Abs(units); i++)
            {
                _pattern.Scroll(ScrollAmount.ScrollAmount_NoAmount, amount);
            }

            return true;
        }
        catch (Exception ex) when (ScrollInput.IsComFailure(ex))
        {
            return false;
        }
    }

    public bool ScrollToStart()
    {
        if (_pattern is null)
        {
            return false;
        }

        try
        {
            _pattern.SetScrollPercent(NoScroll, 0);
            return true;
        }
        catch (Exception ex) when (ScrollInput.IsComFailure(ex))
        {
            return false;
        }
    }

    public void Dispose()
    {
        UiaScrollable.Release(_pattern);
        UiaScrollable.Release(_automation);
        _pattern = null;
        _automation = null;
    }
}
