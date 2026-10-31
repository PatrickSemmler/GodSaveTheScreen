using System.Diagnostics;
using GodSaveTheScreen.Core.Imaging;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Wartet, bis sich ein Bildschirmbereich nicht mehr verändert (Scroll-Animation, nachladende Inhalte).</summary>
public static class RegionSettler
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(40);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Anlaufzeit nach einem Scrollbefehl: Programme (Browser mit weichem Scrollen) beginnen erst nach etwa 100 ms zu bewegen, vorher sähe das Bild „ruhig“ aus.</summary>
    public static readonly TimeSpan DefaultLatency = TimeSpan.FromMilliseconds(120);

    /// <summary>Nimmt alle <paramref name="interval"/> auf, bis zwei Bilder hintereinander gleich sind, höchstens <paramref name="timeout"/> lang; liefert das letzte Bild.</summary>
    public static async Task<PixelBuffer> WaitAsync(
        Func<PixelBuffer> capture, TimeSpan interval, TimeSpan timeout, TimeSpan latency = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var watch = Stopwatch.StartNew();
        if (latency > TimeSpan.Zero)
        {
            await Task.Delay(latency, cancellationToken).ConfigureAwait(true);
        }

        var previous = capture();
        while (watch.Elapsed < timeout)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(true);
            var current = capture();
            if (FrameCompare.NearlyEqual(previous, current))
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }
}

/// <summary>Der gewählte Treiber und wie weit eine Einheit scrollt.</summary>
/// <param name="Driver">Treiber (der Aufrufer gibt ihn frei).</param>
/// <param name="PixelsPerUnit">Pixel Bewegung je Einheit, aus dem Probelauf (mindestens 1).</param>
/// <param name="AtEnd">Der Probelauf nach unten bewegte nichts, nach oben schon: der Bereich stand bereits am Ende.</param>
public sealed record ScrollSelection(IScrollDriver Driver, int PixelsPerUnit, bool AtEnd);

public interface IScrollDriverSelector
{
    /// <summary>Probiert die Treiber der Reihe nach; <c>null</c>, wenn keiner den Inhalt bewegt (dann bleibt der manuelle Modus).</summary>
    Task<ScrollSelection?> SelectAsync(ScrollTarget target, Func<PixelBuffer> capture, CancellationToken cancellationToken = default);
}

/// <summary>
/// Wählt den ersten Treiber, der den Inhalt wirklich bewegt: eine Einheit scrollen, warten, zusammenfügen – nur eine erkannte
/// Verschiebung zählt (ein Video oder eine Animation ist keine). Danach geht es eine Einheit zurück.
/// </summary>
public sealed class ScrollDriverSelector : IScrollDriverSelector
{
    private readonly IReadOnlyList<Func<IScrollDriver>> _factories;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _timeout;
    private readonly ILogger<ScrollDriverSelector>? _logger;

    public ScrollDriverSelector(IEnumerable<Func<IScrollDriver>> factories, ILogger<ScrollDriverSelector>? logger = null)
        : this(factories, RegionSettler.DefaultInterval, RegionSettler.DefaultTimeout, logger)
    {
    }

    public ScrollDriverSelector(IEnumerable<Func<IScrollDriver>> factories, TimeSpan interval, TimeSpan timeout, ILogger<ScrollDriverSelector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(factories);
        _factories = factories.ToList();
        _interval = interval;
        _timeout = timeout;
        _logger = logger;
    }

    /// <summary>Die Treiber in der Reihenfolge, die der Vorversuch ergeben hat.</summary>
    public static IEnumerable<Func<IScrollDriver>> DefaultFactories() =>
    [
        () => new UiaScrollDriver(),
        () => new WheelScrollDriver(),
        () => new MessageScrollDriver(),
        () => new KeyboardScrollDriver(),
    ];

    public async Task<ScrollSelection?> SelectAsync(ScrollTarget target, Func<PixelBuffer> capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(capture);
        foreach (var factory in _factories)
        {
            var driver = factory();
            var keep = false;
            try
            {
                if (!await Task.Run(() => driver.TryPrepare(target), cancellationToken).ConfigureAwait(true))
                {
                    _logger?.LogInformation("Scroll-Treiber {Driver}: nicht anwendbar", driver.Name);
                    continue;
                }

                var selection = await ProbeAsync(driver, capture, cancellationToken).ConfigureAwait(true);
                _logger?.LogInformation("Scroll-Treiber {Driver}: {Result}", driver.Name, selection is null ? "bewegt nichts" : $"{selection.PixelsPerUnit} px je Einheit");
                if (selection is not null)
                {
                    keep = true;
                    return selection;
                }
            }
            finally
            {
                if (!keep)
                {
                    driver.Dispose();
                }
            }
        }

        return null;
    }

    private async Task<ScrollSelection?> ProbeAsync(IScrollDriver driver, Func<PixelBuffer> capture, CancellationToken token)
    {
        var before = await RegionSettler.WaitAsync(capture, _interval, _timeout, TimeSpan.Zero, token).ConfigureAwait(true);
        if (!driver.Scroll(1))
        {
            return null;
        }

        var after = await RegionSettler.WaitAsync(capture, _interval, _timeout, RegionSettler.DefaultLatency, token).ConfigureAwait(true);
        if (Measure(before, after) is > 0 and var down)
        {
            driver.Scroll(-1);
            await RegionSettler.WaitAsync(capture, _interval, _timeout, RegionSettler.DefaultLatency, token).ConfigureAwait(true);
            return new ScrollSelection(driver, down, AtEnd: false);
        }

        // Nichts bewegt sich nach unten: vielleicht steht der Bereich schon am Ende. Nach oben prüfen.
        if (!driver.Scroll(-1))
        {
            return null;
        }

        var up = await RegionSettler.WaitAsync(capture, _interval, _timeout, RegionSettler.DefaultLatency, token).ConfigureAwait(true);
        if (Measure(before, up) is < 0 and var back)
        {
            driver.Scroll(1);
            await RegionSettler.WaitAsync(capture, _interval, _timeout, RegionSettler.DefaultLatency, token).ConfigureAwait(true);
            return new ScrollSelection(driver, -back, AtEnd: true);
        }

        return null;
    }

    /// <summary>Verschiebung in Zeilen (&gt; 0: Inhalt wanderte nach oben = nach unten gescrollt), 0 wenn keine sichere.</summary>
    private static int Measure(PixelBuffer before, PixelBuffer after)
    {
        var stitcher = new ScrollStitcher(100_000, new ScrollStitcherOptions { ScrollbarGuard = 0 });
        stitcher.Add(before);
        var step = stitcher.Add(after);
        return step.Outcome == StitchOutcome.Moved ? Math.Clamp(step.Dy, -before.Height, before.Height) : 0;
    }
}
