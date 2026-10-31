using System.Diagnostics;
using System.Globalization;
using System.Windows.Media;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Capture.Scrolling;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging;
using Windows.Win32;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Ergebnis einer Scroll-Aufnahme: das Bild und ein Hinweis für den Benutzer (Limit, Teilergebnis, unsichere Ränder).</summary>
public sealed record ScrollCaptureOutcome(PixelBuffer Image, ScrollRunEnd End, string? Notice);

public interface IScrollCaptureSession
{
    /// <summary>Nimmt den gewählten Bereich scrollend auf; <c>null</c> bei Abbruch oder wenn nicht gestartet werden konnte (Meldung ist dann gezeigt).</summary>
    Task<ScrollCaptureOutcome?> RunAsync(SelectionResult selection, CancellationToken cancellationToken);
}

/// <summary>Fragt <c>Eingabe</c> und <c>Esc</c> ab, ohne den Fokus zu brauchen (das Zielprogramm behält ihn).</summary>
public interface IScrollCaptureKeys
{
    bool FinishPressed();

    bool CancelPressed();

    /// <summary>Verwirft zuvor gedrückte Tasten (z. B. das <c>Esc</c> aus der Auswahl).</summary>
    void Flush();
}

public sealed class ScrollCaptureKeys : IScrollCaptureKeys
{
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;

    public bool FinishPressed() => Pressed(VkReturn);

    public bool CancelPressed() => Pressed(VkEscape);

    public void Flush()
    {
        _ = Pressed(VkReturn);
        _ = Pressed(VkEscape);
    }

    // Bit 15: gerade gedrückt; Bit 0: seit dem letzten Aufruf gedrückt worden.
    private static bool Pressed(int key) => (PInvoke.GetAsyncKeyState(key) & 0x8001) != 0;
}

/// <summary>
/// Ablauf der Scroll-Aufnahme: Ziel bestimmen, Steuerleiste zeigen, Scrollmethode wählen, automatisch (oder von Hand) aufnehmen und
/// zusammensetzen. <c>IsCapturing</c> und das Ausblenden der Fenster hält der <see cref="CaptureCoordinator"/>; alles, was hier
/// geändert wird (Leiste, Rahmen, Treiber), wird in jedem Fall wieder freigegeben.
/// </summary>
public sealed class ScrollCaptureSession : IScrollCaptureSession
{
    /// <summary>Höchste Höhe des Ergebnisses (hart, siehe Meilenstein M17); die Grafikkarte kann weniger erlauben.</summary>
    public const int MaxHeightLimit = 16384;

    private const int PreviewWidth = 72;
    private const int PreviewHeight = 200;
    private const int ScrollbarGuardPixels = 32;
    private const int DiscardAfterScreens = 2;
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PauseInterval = TimeSpan.FromMilliseconds(250);

    private readonly IScreenCaptureService _capture;
    private readonly IMonitorService _monitors;
    private readonly IScrollTargetResolver _resolver;
    private readonly IScrollDriverSelector _selector;
    private readonly IScrollCaptureViewFactory _views;
    private readonly IScrollCaptureKeys _keys;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly ILogger<ScrollCaptureSession> _logger;
    private readonly Func<(int Width, int Height)> _limits;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ScrollCaptureSession(
        IScreenCaptureService capture,
        IMonitorService monitors,
        IScrollTargetResolver resolver,
        IScrollDriverSelector selector,
        IScrollCaptureViewFactory views,
        IScrollCaptureKeys keys,
        ISettingsService settings,
        IDialogService dialogs,
        ILogger<ScrollCaptureSession> logger,
        Func<(int Width, int Height)>? limits = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _capture = capture;
        _monitors = monitors;
        _resolver = resolver;
        _selector = selector;
        _views = views;
        _keys = keys;
        _settings = settings;
        _dialogs = dialogs;
        _logger = logger;
        _limits = limits ?? TextureLimits;
        _delay = delay ?? ((span, token) => Task.Delay(span, token));
    }

    /// <summary>Größte Texturgröße der Grafikkarte; WPF zeigt größere Bilder nicht mehr an (Absturz des Renderthreads).</summary>
    public static (int Width, int Height) TextureLimits()
    {
        var size = RenderCapability.MaxHardwareTextureSize;
        var width = size.Width > 0 ? (int)size.Width : MaxHeightLimit;
        var height = size.Height > 0 ? (int)size.Height : MaxHeightLimit;
        return (width, height);
    }

    public async Task<ScrollCaptureOutcome?> RunAsync(SelectionResult selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var target = await _resolver.ResolveAsync(selection.Bounds, selection.Window?.Handle ?? 0, cancellationToken).ConfigureAwait(true);
        if (target is null)
        {
            _dialogs.ShowError(Strings.Error_Title, Strings.Scroll_ErrorRegion);
            return null;
        }

        var (maxWidth, maxTexture) = _limits();
        var region = target.Region.Width > maxWidth
            ? new PixelRect(target.Region.X, target.Region.Y, maxWidth, target.Region.Height)
            : target.Region;
        target = target with { Region = region };
        var maxHeight = Math.Min(MaxHeightLimit, maxTexture);
        var monitor = MonitorFor(region);

        using var view = _views.Create();
        if (!view.TryShow(region, monitor))
        {
            _logger.LogWarning("Steuerleiste der Scroll-Aufnahme lässt sich nicht von der Aufnahme ausschließen");
            _dialogs.ShowError(Strings.Error_Title, Strings.Scroll_ErrorExclude);
            return null;
        }

        return await RunCoreAsync(view, target, monitor, maxHeight, cancellationToken).ConfigureAwait(true);
    }

    private async Task<ScrollCaptureOutcome?> RunCoreAsync(
        IScrollCaptureView view, ScrollTarget target, MonitorInfo monitor, int maxHeight, CancellationToken token)
    {
        var region = target.Region;
        PixelBuffer Grab() => _capture.CaptureRegion(region);

        var settings = _settings.Current;
        var stitcher = new ScrollStitcher(maxHeight, new ScrollStitcherOptions { ScrollbarGuard = (int)Math.Round(ScrollbarGuardPixels * monitor.Scale) });
        var engine = new ScrollCaptureEngine(
            Grab,
            stitcher,
            new ScrollCaptureSettings { ExtraWait = TimeSpan.FromMilliseconds(Math.Clamp(settings.ScrollExtraWaitMs, 0, 2000)) },
            _delay);

        var finish = false;
        var cancel = false;
        CancellationTokenSource? modeSwitch = null;
        view.FinishRequested += () => finish = true;
        view.CancelRequested += () => cancel = true;
        view.ModeChanged += () => modeSwitch?.Cancel();

        bool ShouldStop()
        {
            if (_keys.CancelPressed())
            {
                cancel = true;
            }

            if (_keys.FinishPressed())
            {
                finish = true;
            }

            return finish || cancel;
        }

        var preview = Stopwatch.StartNew();
        engine.Progress += (_, progress) =>
        {
            view.SetProgress(progress.Height, progress.MaxHeight);
            if (progress.Last.Outcome == StitchOutcome.NoOverlap && view.ManualMode)
            {
                view.SetStatus(Strings.Scroll_StatusSlowDown);
            }

            if (preview.Elapsed >= PreviewInterval)
            {
                preview.Restart();
                view.SetPreview(stitcher.BuildPreview(PreviewWidth, PreviewHeight));
            }
        };

        async Task BeforeStep(CancellationToken ct)
        {
            while (!_resolver.IsInFront(target) && !ShouldStop())
            {
                view.SetStatus(Strings.Scroll_StatusPaused);
                await _delay(PauseInterval, ct).ConfigureAwait(true);
            }

            view.SetStatus(Strings.Scroll_StatusAuto);
        }

        _keys.Flush();
        view.SetProgress(0, maxHeight);
        view.SetStatus(Strings.Scroll_Preparing);
        ScrollSelection? selection = null;
        try
        {
            if (!view.ManualMode)
            {
                selection = await _selector.SelectAsync(target, Grab, token).ConfigureAwait(true);
                if (selection is null && !view.ManualMode)
                {
                    view.SetManual(true);
                    view.SetStatus(Strings.Scroll_StatusNoReaction);
                }
            }

            var toStart = view.ToStartRequested;
            view.LockStartOption();
            ScrollRunEnd end;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                using var modeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                modeSwitch = modeCts;
                try
                {
                    if (!view.ManualMode && selection is not null)
                    {
                        view.SetStatus(Strings.Scroll_StatusAuto);
                        var driver = selection.Driver;
                        var perUnit = selection.PixelsPerUnit;
                        var start = toStart;
                        end = await Task.Run(() => engine.RunAutoAsync(driver, perUnit, start, ShouldStop, BeforeStep, modeCts.Token), CancellationToken.None).ConfigureAwait(true);
                    }
                    else
                    {
                        if (view.ManualMode)
                        {
                            view.SetStatus(Strings.Scroll_StatusManual);
                        }

                        end = await Task.Run(() => engine.RunManualAsync(ShouldStop, modeCts.Token), CancellationToken.None).ConfigureAwait(true);
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Der Benutzer hat zwischen Automatik und Handbetrieb gewechselt: mit dem anderen Lauf weitermachen.
                    continue;
                }
                finally
                {
                    modeSwitch = null;
                }

                if (cancel)
                {
                    if (stitcher.ContentHeight > DiscardAfterScreens * region.Height
                        && !_dialogs.Confirm(Strings.Scroll_DiscardTitle, string.Format(CultureInfo.CurrentUICulture, Strings.Scroll_DiscardMessage, stitcher.ContentHeight)))
                    {
                        cancel = false;
                        finish = false;
                        continue;
                    }

                    _logger.LogInformation("Scroll-Aufnahme abgebrochen");
                    return null;
                }

                if (end == ScrollRunEnd.Stalled && !view.ManualMode)
                {
                    _logger.LogInformation("Automatisches Scrollen kommt nicht weiter bei {Height} px; weiter im Handbetrieb", stitcher.ContentHeight);
                    view.SetManual(true);
                    view.SetStatus(Strings.Scroll_StatusStalled);
                    continue;
                }

                break;
            }

            return Finish(stitcher, engine, end, maxHeight, Grab);
        }
        finally
        {
            selection?.Driver.Dispose();
        }
    }

    private ScrollCaptureOutcome Finish(ScrollStitcher stitcher, ScrollCaptureEngine engine, ScrollRunEnd end, int maxHeight, Func<PixelBuffer> grab)
    {
        if (stitcher.Width == 0)
        {
            stitcher.Add(grab());
        }

        var notes = new List<string>();
        if (end == ScrollRunEnd.Limit || stitcher.LimitHit)
        {
            notes.Add(string.Format(CultureInfo.CurrentUICulture, Strings.Scroll_NoticeLimit, maxHeight));
        }
        else if (engine.LastWasNoOverlap)
        {
            notes.Add(string.Format(CultureInfo.CurrentUICulture, Strings.Scroll_NoticePartial, stitcher.ContentHeight));
        }

        if (end == ScrollRunEnd.TimedOut)
        {
            notes.Add(Strings.Scroll_NoticeTimeout);
        }

        if (stitcher.FixedRegionsUncertain && stitcher.HeaderRows + stitcher.FooterRows >= 8)
        {
            notes.Add(Strings.Scroll_NoticeFixed);
        }

        var image = stitcher.Build();
        _logger.LogInformation(
            "Scroll-Aufnahme beendet: {End}, {Width}x{Height}, Kopf {Header} Fuß {Footer}, Hinweise {Notes}",
            end, image.Width, image.Height, stitcher.HeaderRows, stitcher.FooterRows, notes.Count);
        return new ScrollCaptureOutcome(image, end, notes.Count == 0 ? null : string.Join(Environment.NewLine, notes));
    }

    private MonitorInfo MonitorFor(PixelRect region)
    {
        var cx = region.X + (region.Width / 2);
        var cy = region.Y + (region.Height / 2);
        return _monitors.GetMonitors().FirstOrDefault(m => m.Bounds.Contains(cx, cy)) ?? _monitors.GetMonitorAtCursor();
    }
}
