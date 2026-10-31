using System.Diagnostics;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Wie ein Lauf der Scroll-Aufnahme endete.</summary>
public enum ScrollRunEnd
{
    /// <summary>Das Ende des Inhalts ist erreicht.</summary>
    EndOfContent,

    /// <summary>Die höchste erlaubte Höhe ist erreicht.</summary>
    Limit,

    /// <summary>Der Benutzer hat „Fertig“ gewählt.</summary>
    Finished,

    /// <summary>Das automatische Scrollen kommt nicht weiter (Treiber ohne Wirkung, keine Überlappung); der manuelle Modus kann fortsetzen.</summary>
    Stalled,

    /// <summary>Das Zeitlimit ist abgelaufen.</summary>
    TimedOut,
}

/// <summary>Fortschritt für die Oberfläche.</summary>
/// <param name="Height">Bisherige Höhe des Ergebnisses.</param>
/// <param name="MaxHeight">Höchste erlaubte Höhe.</param>
/// <param name="Last">Ergebnis des letzten Bilds.</param>
public readonly record struct ScrollProgress(int Height, int MaxHeight, StitchStep Last);

public sealed record ScrollCaptureSettings
{
    /// <summary>Wartezeit nach dem Stillstand für nachladende Seiten.</summary>
    public TimeSpan ExtraWait { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Zeit zwischen zwei Aufnahmen im manuellen Modus.</summary>
    public TimeSpan ManualInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gesamtdauer, danach endet die Aufnahme.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Ziel der Schrittgröße als Anteil der Bewegungshöhe.</summary>
    public double StepRatio { get; init; } = 0.7;

    /// <summary>Fehlversuche ohne Überlappung, bevor das automatische Scrollen aufgibt.</summary>
    public int MaxFailures { get; init; } = 3;

    public TimeSpan SettleInterval { get; init; } = RegionSettler.DefaultInterval;

    public TimeSpan SettleTimeout { get; init; } = RegionSettler.DefaultTimeout;

    /// <summary>Anlaufzeit nach einem Scrollbefehl, bevor auf Stillstand geprüft wird.</summary>
    public TimeSpan Latency { get; init; } = RegionSettler.DefaultLatency;
}

/// <summary>
/// Schleife der Scroll-Aufnahme ohne Oberfläche: scrollen, warten bis der Bereich stillsteht, aufnehmen, zusammensetzen,
/// Schrittgröße anpassen – automatisch oder (Bilder alle 100 ms) im manuellen Modus. Beide Läufe teilen sich denselben
/// <see cref="ScrollStitcher"/>, ein Wechsel setzt das Ergebnis also fort.
/// </summary>
public sealed class ScrollCaptureEngine
{
    private const int ToStartAttempts = 25;

    private readonly Func<PixelBuffer> _capture;
    private readonly ScrollCaptureSettings _settings;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Stopwatch _clock = new();

    public ScrollCaptureEngine(
        Func<PixelBuffer> capture,
        ScrollStitcher stitcher,
        ScrollCaptureSettings? settings = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(stitcher);
        _capture = capture;
        Stitcher = stitcher;
        _settings = settings ?? new ScrollCaptureSettings();
        _delay = delay ?? ((span, token) => Task.Delay(span, token));
    }

    public ScrollStitcher Stitcher { get; }

    /// <summary>Wird nach jedem Bild aufgerufen (im Aufnahme-Thread).</summary>
    public event EventHandler<ScrollProgress>? Progress;

    /// <summary>Hinweis für den Benutzer, wenn im manuellen Modus keine Überlappung gefunden wurde („Langsamer scrollen“).</summary>
    public bool LastWasNoOverlap { get; private set; }

    /// <summary>
    /// Scrollt selbst. Das erste Bild nimmt der Aufruf auf, falls der Stitcher noch leer ist. <paramref name="beforeStep"/> läuft
    /// vor jedem Scrollschritt und darf warten (z. B. solange das Zielfenster nicht vorn liegt).
    /// </summary>
    public async Task<ScrollRunEnd> RunAutoAsync(
        IScrollDriver driver,
        int pixelsPerUnit,
        bool scrollToStart,
        Func<bool> finishRequested,
        Func<CancellationToken, Task>? beforeStep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(finishRequested);
        _clock.Start();
        if (scrollToStart && Stitcher.Width == 0)
        {
            await ScrollToStartAsync(driver, cancellationToken).ConfigureAwait(true);
        }

        var frame = await SettleAsync(cancellationToken).ConfigureAwait(true);
        if (Stitcher.Width == 0)
        {
            Report(Stitcher.Add(frame));
        }

        var perUnit = Math.Max(1.0, pixelsPerUnit);
        var units = StepUnits(perUnit);
        var noMovement = 0;
        var failures = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (finishRequested())
            {
                return ScrollRunEnd.Finished;
            }

            if (_clock.Elapsed > _settings.Timeout)
            {
                return ScrollRunEnd.TimedOut;
            }

            if (driver.IsAtEnd == true)
            {
                return ScrollRunEnd.EndOfContent;
            }

            if (beforeStep is not null)
            {
                await beforeStep(cancellationToken).ConfigureAwait(true);
                if (finishRequested())
                {
                    return ScrollRunEnd.Finished;
                }
            }

            if (!driver.Scroll(units))
            {
                return ScrollRunEnd.Stalled;
            }

            frame = await SettleAsync(cancellationToken, afterScroll: true).ConfigureAwait(true);
            var step = Stitcher.Add(frame);
            Report(step);
            switch (step.Outcome)
            {
                case StitchOutcome.LimitReached:
                    return ScrollRunEnd.Limit;

                case StitchOutcome.NoMovement:
                    noMovement++;
                    if (noMovement >= 2)
                    {
                        return ScrollRunEnd.EndOfContent;
                    }

                    break;

                case StitchOutcome.Moved when step.Dy > 0:
                    noMovement = 0;
                    failures = 0;
                    if (step.Dy >= 0.4 * units * perUnit)
                    {
                        perUnit = (perUnit + ((double)step.Dy / units)) / 2;
                    }

                    units = StepUnits(perUnit);
                    break;

                case StitchOutcome.Moved:
                    // Der Inhalt sprang zurück (neu geladene Seite): wie ein Fehlversuch behandeln.
                    failures++;
                    break;

                default:
                    failures++;
                    if (failures >= _settings.MaxFailures)
                    {
                        return ScrollRunEnd.Stalled;
                    }

                    // Zu weit gesprungen: zurück zur letzten sicheren Stelle und mit halber Schrittgröße weiter.
                    driver.Scroll(-units);
                    await SettleAsync(cancellationToken, afterScroll: true).ConfigureAwait(true);
                    units = Math.Max(1, units / 2);
                    break;
            }

            if (failures >= _settings.MaxFailures)
            {
                return ScrollRunEnd.Stalled;
            }
        }
    }

    /// <summary>Nimmt alle 100 ms auf, während der Benutzer selbst scrollt, bis <paramref name="finishRequested"/> wahr wird.</summary>
    public async Task<ScrollRunEnd> RunManualAsync(Func<bool> finishRequested, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(finishRequested);
        _clock.Start();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (finishRequested())
            {
                return ScrollRunEnd.Finished;
            }

            if (_clock.Elapsed > _settings.Timeout)
            {
                return ScrollRunEnd.TimedOut;
            }

            var step = Stitcher.Add(_capture());
            Report(step);
            if (step.Outcome == StitchOutcome.LimitReached)
            {
                return ScrollRunEnd.Limit;
            }

            await _delay(_settings.ManualInterval, cancellationToken).ConfigureAwait(true);
        }
    }

    private int StepUnits(double perUnit)
    {
        var target = _settings.StepRatio * Stitcher.FrameHeightHint;
        return Math.Max(1, (int)Math.Floor(target / perUnit));
    }

    private void Report(StitchStep step)
    {
        LastWasNoOverlap = step.Outcome == StitchOutcome.NoOverlap;
        Progress?.Invoke(this, new ScrollProgress(Stitcher.ContentHeight, Stitcher.MaxHeight, step));
    }

    private async Task ScrollToStartAsync(IScrollDriver driver, CancellationToken token)
    {
        var previous = await SettleAsync(token).ConfigureAwait(true);
        for (var i = 0; i < ToStartAttempts; i++)
        {
            if (!driver.ScrollToStart())
            {
                return;
            }

            var current = await SettleAsync(token, afterScroll: true).ConfigureAwait(true);
            if (FrameCompare.NearlyEqual(previous, current))
            {
                return;
            }

            previous = current;
        }
    }

    /// <summary>Wartet auf Stillstand und danach die Zusatzzeit für nachladende Inhalte; ändert sich der Bereich dabei, wird noch einmal gewartet.</summary>
    private async Task<PixelBuffer> SettleAsync(CancellationToken token, bool afterScroll = false)
    {
        var frame = await RegionSettler.WaitAsync(
            _capture, _settings.SettleInterval, _settings.SettleTimeout, afterScroll ? _settings.Latency : TimeSpan.Zero, token).ConfigureAwait(true);
        if (_settings.ExtraWait <= TimeSpan.Zero)
        {
            return frame;
        }

        await _delay(_settings.ExtraWait, token).ConfigureAwait(true);
        var next = _capture();
        if (FrameCompare.NearlyEqual(frame, next))
        {
            return next;
        }

        return await RegionSettler.WaitAsync(_capture, _settings.SettleInterval, _settings.SettleTimeout, TimeSpan.Zero, token).ConfigureAwait(true);
    }
}
