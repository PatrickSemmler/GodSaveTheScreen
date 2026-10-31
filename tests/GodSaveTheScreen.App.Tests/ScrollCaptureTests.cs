#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
#pragma warning disable CA1861 // Testdaten stehen bewusst direkt im Test
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Capture.Scrolling;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Eine scrollbare „Seite“ aus Bändern und das Fenster (Höhe <see cref="Height"/>), das den Ausschnitt ab <see cref="Offset"/> zeigt.</summary>
internal sealed class ScrollWorld
{
    private volatile int _offset;

    public ScrollWorld(int width, int height, int pageRows, int seed = 7)
    {
        Width = width;
        Height = height;
        Page = new PixelBuffer(width, pageRows);
        var random = new Random(seed);
        var row = new byte[width * 4];
        var y = 0;
        while (y < pageRows)
        {
            var band = random.Next(2, 9);
            var x = 0;
            while (x < width)
            {
                var run = random.Next(3, 40);
                var colour = random.Next(0, 0xFFFFFF);
                for (var i = 0; i < run && x < width; i++, x++)
                {
                    row[x * 4] = (byte)(colour & 0xFF);
                    row[(x * 4) + 1] = (byte)((colour >> 8) & 0xFF);
                    row[(x * 4) + 2] = (byte)((colour >> 16) & 0xFF);
                    row[(x * 4) + 3] = 255;
                }
            }

            for (var i = 0; i < band && y < pageRows; i++, y++)
            {
                Buffer.BlockCopy(row, 0, Page.Pixels, y * Page.Stride, row.Length);
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    public PixelBuffer Page { get; }

    public int MaxOffset => Page.Height - Height;

    public int Offset
    {
        get => _offset;
        set => _offset = Math.Clamp(value, 0, MaxOffset);
    }

    public PixelRect Region => new(0, 0, Width, Height);

    public PixelBuffer Grab() => Page.Crop(new PixelRect(0, Offset, Width, Height));

    public bool PageEquals(PixelBuffer image, int rows) =>
        image.Width == Width && image.Height == rows
        && image.Pixels.AsSpan(0, rows * image.Stride).SequenceEqual(Page.Pixels.AsSpan(0, rows * Page.Stride));
}

internal sealed class FakeScrollDriver : IScrollDriver
{
    private readonly ScrollWorld _world;
    private readonly int _pixelsPerUnit;

    public FakeScrollDriver(ScrollWorld world, int pixelsPerUnit = 20, bool prepares = true, bool moves = true, bool scrollSucceeds = true)
    {
        _world = world;
        _pixelsPerUnit = pixelsPerUnit;
        Prepares = prepares;
        Moves = moves;
        ScrollSucceeds = scrollSucceeds;
    }

    public bool Prepares { get; }

    public bool Moves { get; }

    public bool ScrollSucceeds { get; set; }

    public bool Disposed { get; private set; }

    public string Name => "Fake";

    public bool MovesCursor => false;

    public bool NeedsFocus => false;

    public bool? IsAtEnd => null;

    public bool TryPrepare(ScrollTarget target) => Prepares;

    public bool Scroll(int units)
    {
        if (!ScrollSucceeds)
        {
            return false;
        }

        if (Moves)
        {
            _world.Offset += units * _pixelsPerUnit;
        }

        return true;
    }

    public bool ScrollToStart()
    {
        _world.Offset = 0;
        return true;
    }

    public void Dispose() => Disposed = true;
}

internal sealed class FakeScrollView : IScrollCaptureView
{
    public event Action? ModeChanged;

    public event Action? FinishRequested;

    public event Action? CancelRequested;

    public bool ShowSucceeds { get; set; } = true;

    public bool ToStartRequested { get; set; }

    public bool ManualMode { get; private set; }

    public bool Shown { get; private set; }

    public bool Disposed { get; private set; }

    public int LastHeight { get; private set; }

    public List<string> Statuses { get; } = [];

    public Action<int>? OnProgress { get; set; }

    public Action<string>? OnStatus { get; set; }

    public void RaiseFinish() => FinishRequested?.Invoke();

    public void RaiseCancel() => CancelRequested?.Invoke();

    public void RaiseModeChanged(bool manual)
    {
        ManualMode = manual;
        ModeChanged?.Invoke();
    }

    public bool TryShow(PixelRect region, MonitorInfo monitor)
    {
        Shown = ShowSucceeds;
        return ShowSucceeds;
    }

    public void SetManual(bool manual) => ManualMode = manual;

    public void SetStatus(string text)
    {
        lock (Statuses)
        {
            Statuses.Add(text);
        }

        OnStatus?.Invoke(text);
    }

    public void SetProgress(int height, int maxHeight)
    {
        LastHeight = height;
        OnProgress?.Invoke(height);
    }

    public void SetPreview(PixelBuffer? preview)
    {
    }

    public void LockStartOption()
    {
    }

    public void Dispose() => Disposed = true;
}

internal sealed class NoKeys : IScrollCaptureKeys
{
    public bool FinishPressed() => false;

    public bool CancelPressed() => false;

    public void Flush()
    {
    }
}

internal sealed class ScrollHarness
{
    public ScrollHarness(ScrollWorld world, ScrollSelection? selection, Func<(int Width, int Height)>? limits = null, int extraWaitMs = 0)
    {
        World = world;
        Dialogs = Substitute.For<IDialogService>();
        View = new FakeScrollView();
        Resolver = Substitute.For<IScrollTargetResolver>();
        Resolver.ResolveAsync(Arg.Any<PixelRect>(), Arg.Any<nint>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<ScrollTarget?>(new ScrollTarget(1, world.Region)));
        Resolver.IsInFront(Arg.Any<ScrollTarget>()).Returns(true);
        Selector = Substitute.For<IScrollDriverSelector>();
        Selector.SelectAsync(Arg.Any<ScrollTarget>(), Arg.Any<Func<PixelBuffer>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(selection));

        var capture = Substitute.For<IScreenCaptureService>();
        Capture = capture;
        capture.CaptureRegion(Arg.Any<PixelRect>(), Arg.Any<bool>()).Returns(_ => world.Grab());
        var monitor = new MonitorInfo(1, new PixelRect(0, 0, 4000, 4000), new PixelRect(0, 0, 4000, 4000), 96, 96, true, "M");
        var monitors = Substitute.For<IMonitorService>();
        monitors.GetMonitors().Returns([monitor]);
        monitors.GetMonitorAtCursor().Returns(monitor);
        var views = Substitute.For<IScrollCaptureViewFactory>();
        views.Create().Returns(View);

        Session = new ScrollCaptureSession(
            capture,
            monitors,
            Resolver,
            Selector,
            views,
            new NoKeys(),
            new FakeSettings { Current = new AppSettings { ScrollExtraWaitMs = extraWaitMs } },
            Dialogs,
            NullLogger<ScrollCaptureSession>.Instance,
            limits ?? (() => (16384, 16384)));
    }

    public ScrollWorld World { get; }

    public IScreenCaptureService Capture { get; }

    public IDialogService Dialogs { get; }

    public FakeScrollView View { get; }

    public IScrollTargetResolver Resolver { get; }

    public IScrollDriverSelector Selector { get; }

    public ScrollCaptureSession Session { get; }

    public Task<ScrollCaptureOutcome?> RunAsync() => Session.RunAsync(new SelectionResult(World.Region), CancellationToken.None);
}

public sealed class ScrollCaptureSessionTests
{
    private static ScrollSelection Auto(ScrollWorld world, int pixelsPerUnit = 20) =>
        new(new FakeScrollDriver(world, pixelsPerUnit), pixelsPerUnit, AtEnd: false);

    [Fact]
    public async Task Automatisch_SetztDieGanzeSeiteZusammen()
    {
        var world = new ScrollWorld(160, 300, 1800);
        var h = new ScrollHarness(world, Auto(world));

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        outcome.End.ShouldBe(ScrollRunEnd.EndOfContent);
        outcome.Notice.ShouldBeNull();
        world.PageEquals(outcome.Image, 1800).ShouldBeTrue();
        h.View.Shown.ShouldBeTrue();
        h.View.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ZuerstZumAnfang_ScrolltVorDerAufnahmeNachOben()
    {
        var world = new ScrollWorld(160, 300, 1500) { Offset = 600 };
        var h = new ScrollHarness(world, Auto(world)) { View = { ToStartRequested = true } };

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        world.PageEquals(outcome.Image, 1500).ShouldBeTrue();
    }

    [Fact]
    public async Task OhneZuerstZumAnfang_BeginntDieAufnahmeAnDerAktuellenStelle()
    {
        var world = new ScrollWorld(160, 300, 1500) { Offset = 600 };
        var h = new ScrollHarness(world, Auto(world));

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        outcome.Image.Height.ShouldBe(900);
        outcome.Image.Pixels.AsSpan().SequenceEqual(world.Page.Crop(new PixelRect(0, 600, 160, 900)).Pixels).ShouldBeTrue();
    }

    [Fact]
    public async Task Limit_BeendetMitHinweisUndFuelltDieHoeheAus()
    {
        var world = new ScrollWorld(160, 300, 2400);
        var h = new ScrollHarness(world, Auto(world), () => (4000, 700));

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        outcome.End.ShouldBe(ScrollRunEnd.Limit);
        outcome.Image.Height.ShouldBe(700);
        outcome.Notice.ShouldNotBeNull();
        outcome.Notice.ShouldContain("700");
        world.PageEquals(outcome.Image, 700).ShouldBeTrue();
    }

    [Fact]
    public async Task Breite_WirdAufDieTexturgroesseBegrenzt()
    {
        var world = new ScrollWorld(160, 300, 900);
        var h = new ScrollHarness(world, Auto(world), () => (100, 16384));

        await h.RunAsync();

        h.Capture.Received().CaptureRegion(Arg.Is<PixelRect>(r => r.Width == 100 && r.Height == 300), Arg.Any<bool>());
        h.Capture.DidNotReceive().CaptureRegion(Arg.Is<PixelRect>(r => r.Width != 100), Arg.Any<bool>());
    }

    [Fact]
    public async Task Abbruch_NachWenigenBildern_OhneRueckfrage()
    {
        var world = new ScrollWorld(160, 300, 3000);
        var h = new ScrollHarness(world, Auto(world));
        h.View.OnProgress = _ => h.View.RaiseCancel();

        var outcome = await h.RunAsync();

        outcome.ShouldBeNull();
        h.Dialogs.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<string>());
        h.View.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Abbruch_NachMehrAlsZweiBildschirmen_FragtNachUndVerwirft()
    {
        var world = new ScrollWorld(160, 300, 3000);
        var h = new ScrollHarness(world, Auto(world));
        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var asked = false;
        h.View.OnProgress = height =>
        {
            if (height > 700 && !asked)
            {
                asked = true;
                h.View.RaiseCancel();
            }
        };

        var outcome = await h.RunAsync();

        outcome.ShouldBeNull();
        h.Dialogs.Received(1).Confirm(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Abbruch_NachRueckfrageMitNein_NimmtWeiterAuf()
    {
        var world = new ScrollWorld(160, 300, 1500);
        var h = new ScrollHarness(world, Auto(world));
        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var asked = false;
        h.View.OnProgress = height =>
        {
            if (height > 700 && !asked)
            {
                asked = true;
                h.View.RaiseCancel();
            }
        };

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        world.PageEquals(outcome.Image, 1500).ShouldBeTrue();
    }

    [Fact]
    public async Task KeinTreiberWirkt_WechseltInDenHandbetrieb_UndNimmtLaufendAuf()
    {
        var world = new ScrollWorld(160, 300, 2000);
        var h = new ScrollHarness(world, selection: null);
        var scroller = Task.Run(async () =>
        {
            await Task.Delay(300);
            for (var i = 0; i < 10; i++)
            {
                world.Offset += 50;
                await Task.Delay(120);
            }

            await Task.Delay(400);
            h.View.RaiseFinish();
        });

        var outcome = await h.RunAsync();
        await scroller;

        h.View.ManualMode.ShouldBeTrue();
        h.View.Statuses.ShouldContain(Strings.Scroll_StatusNoReaction);
        outcome.ShouldNotBeNull();
        outcome.End.ShouldBe(ScrollRunEnd.Finished);
        outcome.Image.Height.ShouldBe(300 + 500);
        world.PageEquals(outcome.Image, 800).ShouldBeTrue();
    }

    [Fact]
    public async Task TreiberOhneWirkungWaehrendDerAufnahme_FuehrtInDenHandbetrieb()
    {
        var world = new ScrollWorld(160, 300, 2000);
        var driver = new FakeScrollDriver(world, scrollSucceeds: false);
        var h = new ScrollHarness(world, new ScrollSelection(driver, 20, AtEnd: false));
        h.View.OnStatus = status =>
        {
            if (status == Strings.Scroll_StatusStalled)
            {
                _ = Task.Delay(300).ContinueWith(_ => h.View.RaiseFinish(), TaskScheduler.Default);
            }
        };

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        h.View.ManualMode.ShouldBeTrue();
        driver.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Pausiert_WennDasZielfensterNichtVornLiegt()
    {
        var world = new ScrollWorld(160, 300, 900);
        var h = new ScrollHarness(world, Auto(world));
        var calls = 0;
        h.Resolver.IsInFront(Arg.Any<ScrollTarget>()).Returns(_ => ++calls > 3);

        var outcome = await h.RunAsync();

        outcome.ShouldNotBeNull();
        h.View.Statuses.ShouldContain(Strings.Scroll_StatusPaused);
        world.PageEquals(outcome.Image, 900).ShouldBeTrue();
    }

    [Fact]
    public async Task Steuerleiste_LaesstSichNichtAusschliessen_BrichtMitMeldungAb()
    {
        var world = new ScrollWorld(160, 300, 900);
        var h = new ScrollHarness(world, Auto(world));
        h.View.ShowSucceeds = false;

        var outcome = await h.RunAsync();

        outcome.ShouldBeNull();
        h.Dialogs.Received(1).ShowError(Arg.Any<string>(), Strings.Scroll_ErrorExclude);
        h.View.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ZuKleinerBereich_ZeigtMeldung()
    {
        var world = new ScrollWorld(160, 300, 900);
        var h = new ScrollHarness(world, Auto(world));
        h.Resolver.ResolveAsync(Arg.Any<PixelRect>(), Arg.Any<nint>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ScrollTarget?>(null));

        var outcome = await h.RunAsync();

        outcome.ShouldBeNull();
        h.Dialogs.Received(1).ShowError(Arg.Any<string>(), Strings.Scroll_ErrorRegion);
        h.View.Shown.ShouldBeFalse();
    }

    [Fact]
    public async Task GewaehlterTreiber_WirdAmEndeFreigegeben()
    {
        var world = new ScrollWorld(160, 300, 900);
        var driver = new FakeScrollDriver(world);
        var h = new ScrollHarness(world, new ScrollSelection(driver, 20, AtEnd: false));

        await h.RunAsync();

        driver.Disposed.ShouldBeTrue();
    }
}

public sealed class ScrollDriverSelectorTests
{
    private static ScrollDriverSelector Selector(params IScrollDriver[] drivers) =>
        new(drivers.Select<IScrollDriver, Func<IScrollDriver>>(d => () => d), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(100));

    private static ScrollTarget Target(ScrollWorld world) => new(1, world.Region);

    [Fact]
    public async Task WaehltDenErstenTreiber_DerDenInhaltBewegt()
    {
        var world = new ScrollWorld(160, 300, 1500);
        var dead = new FakeScrollDriver(world, moves: false);
        var notApplicable = new FakeScrollDriver(world, prepares: false);
        var good = new FakeScrollDriver(world, pixelsPerUnit: 24);

        var selection = await Selector(notApplicable, dead, good).SelectAsync(Target(world), world.Grab);

        selection.ShouldNotBeNull();
        selection.Driver.ShouldBeSameAs(good);
        selection.PixelsPerUnit.ShouldBe(24);
        selection.AtEnd.ShouldBeFalse();
        world.Offset.ShouldBe(0);
        dead.Disposed.ShouldBeTrue();
        notApplicable.Disposed.ShouldBeTrue();
        good.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task KeinTreiberWirkt_ErgibtNull()
    {
        var world = new ScrollWorld(160, 300, 1500);
        var a = new FakeScrollDriver(world, moves: false);
        var b = new FakeScrollDriver(world, scrollSucceeds: false);

        var selection = await Selector(a, b).SelectAsync(Target(world), world.Grab);

        selection.ShouldBeNull();
        a.Disposed.ShouldBeTrue();
        b.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task StehtDerBereichAmEnde_ZaehltDieBewegungNachOben()
    {
        var world = new ScrollWorld(160, 300, 1500);
        world.Offset = world.MaxOffset;
        var driver = new FakeScrollDriver(world, pixelsPerUnit: 20);

        var selection = await Selector(driver).SelectAsync(Target(world), world.Grab);

        selection.ShouldNotBeNull();
        selection.AtEnd.ShouldBeTrue();
        selection.PixelsPerUnit.ShouldBe(20);
        world.Offset.ShouldBe(world.MaxOffset);
    }

    [Fact]
    public async Task EineAnimation_IstKeineScrollbewegung()
    {
        var world = new ScrollWorld(160, 300, 1500);
        var random = new Random(3);
        PixelBuffer Noise()
        {
            var frame = world.Grab();
            random.NextBytes(frame.Pixels.AsSpan(0, frame.Pixels.Length / 2));
            return frame;
        }

        var driver = new FakeScrollDriver(world, moves: false);

        var selection = await Selector(driver).SelectAsync(Target(world), Noise);

        selection.ShouldBeNull();
    }
}

public sealed class ScrollCaptureCoordinatorTests
{
    private static (CaptureCoordinator Coordinator, ISelectionService Selection, IScrollCaptureSession Session, IScreenCaptureService Capture, IPostCaptureAction Action, IDialogService Dialogs)
        Create(ScrollCaptureOutcome? outcome, SelectionResult? selected = null)
    {
        var capture = Substitute.For<IScreenCaptureService>();
        var monitor = new MonitorInfo(1, new PixelRect(0, 0, 100, 100), new PixelRect(0, 0, 100, 100), 120, 120, true, "M");
        capture.CaptureVirtualScreen(Arg.Any<bool>()).Returns(_ => new ScreenSnapshot(new PixelBuffer(100, 100), 0, 0, [monitor]));
        var monitors = Substitute.For<IMonitorService>();
        monitors.GetMonitorAtCursor().Returns(monitor);
        var selection = Substitute.For<ISelectionService>();
        selection.SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(selected));
        var session = Substitute.For<IScrollCaptureSession>();
        session.RunAsync(Arg.Any<SelectionResult>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(outcome));
        var visibility = Substitute.For<IWindowVisibilityService>();
        visibility.HideAll().Returns(Substitute.For<IDisposable>());
        var dialogs = Substitute.For<IDialogService>();
        var action = Substitute.For<IPostCaptureAction>();

        var coordinator = new CaptureCoordinator(
            capture,
            monitors,
            Substitute.For<IWindowContentCapture>(),
            visibility,
            Substitute.For<ICountdownService>(),
            selection,
            [action],
            new FakeSettings { Current = new AppSettings { CaptureCursor = true } },
            dialogs,
            NullLogger<CaptureCoordinator>.Instance,
            null,
            session);
        return (coordinator, selection, session, capture, action, dialogs);
    }

    [Fact]
    public async Task Scrolling_WaehltImFenstermodus_NimmtOhneZeigerAuf_UndLaeuftDurchDieKette()
    {
        var image = new PixelBuffer(50, 400);
        var t = Create(new ScrollCaptureOutcome(image, ScrollRunEnd.EndOfContent, null), new SelectionResult(new PixelRect(5, 5, 50, 50)));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Scrolling));

        t.Capture.Received(1).CaptureVirtualScreen(false);
        await t.Selection.Received(1).SelectAsync(Arg.Any<ScreenSnapshot>(), CaptureMode.Window, Strings.Overlay_HintScroll, Arg.Any<CancellationToken>());
        await t.Action.Received(1).ExecuteAsync(Arg.Is<CaptureResultContext>(c => c.Document.BaseImage.Height == 400 && c.Mode == CaptureMode.Scrolling));
        t.Dialogs.DidNotReceive().ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        t.Coordinator.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public async Task Scrolling_ZeigtDenHinweisDerSitzung()
    {
        var t = Create(new ScrollCaptureOutcome(new PixelBuffer(10, 10), ScrollRunEnd.Limit, "Hinweis"), new SelectionResult(new PixelRect(0, 0, 50, 50)));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Scrolling));

        t.Dialogs.Received(1).ShowInfo(Arg.Any<string>(), "Hinweis");
        await t.Action.Received(1).ExecuteAsync(Arg.Any<CaptureResultContext>());
    }

    [Fact]
    public async Task Scrolling_OhneAuswahlOderBeiAbbruch_FuehrtNichtsAus()
    {
        var abortedSelection = Create(new ScrollCaptureOutcome(new PixelBuffer(10, 10), ScrollRunEnd.Finished, null), selected: null);
        await abortedSelection.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Scrolling));
        await abortedSelection.Session.DidNotReceive().RunAsync(Arg.Any<SelectionResult>(), Arg.Any<CancellationToken>());
        await abortedSelection.Action.DidNotReceive().ExecuteAsync(Arg.Any<CaptureResultContext>());

        var abortedSession = Create(null, new SelectionResult(new PixelRect(0, 0, 50, 50)));
        await abortedSession.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Scrolling));
        await abortedSession.Action.DidNotReceive().ExecuteAsync(Arg.Any<CaptureResultContext>());
        abortedSession.Coordinator.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public void Aufnahmemodus_Scrolling_HatHotkeyUndStehtAmEnde()
    {
        Enum.GetValues<CaptureMode>().Last().ShouldBe(CaptureMode.Scrolling);
        ((int)CaptureMode.Text).ShouldBe(4);
        var settings = new AppSettings();
        settings.GetHotkey(CaptureMode.Scrolling).ShouldBe("Ctrl+Shift+D6");
        settings.WithHotkey(CaptureMode.Scrolling, "Alt+S").HotkeyScroll.ShouldBe("Alt+S");
        settings.WithHotkey(CaptureMode.Scrolling, "Alt+S").HotkeyText.ShouldBe(settings.HotkeyText);
    }

    [Fact]
    public void Einstellungen_Scroll_HabenStandardwerteUndBegrenzung()
    {
        var settings = new AppSettings();
        settings.ScrollCaptureToStart.ShouldBeFalse();
        settings.ScrollExtraWaitMs.ShouldBe(150);
        (settings with { ScrollExtraWaitMs = 99999 }).Normalized().ScrollExtraWaitMs.ShouldBe(2000);
        (settings with { ScrollExtraWaitMs = -5 }).Normalized().ScrollExtraWaitMs.ShouldBe(0);
        (settings with { HotkeyScroll = null! }).Normalized().HotkeyScroll.ShouldBe("Ctrl+Shift+D6");
    }

    [Fact]
    public void Kommandozeile_VersteheCaptureScroll()
    {
        CommandLineParser.Parse(["--capture", "scroll"]).Capture.ShouldBe(CaptureMode.Scrolling);
        CommandLineParser.Parse(["--capture", "SCROLL"]).Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Texturgroesse_LiefertEinenPositivenWert()
    {
        var (width, height) = ScrollCaptureSession.TextureLimits();
        width.ShouldBeGreaterThan(0);
        height.ShouldBeGreaterThan(0);
        ScrollCaptureSession.MaxHeightLimit.ShouldBe(16384);
    }
}
