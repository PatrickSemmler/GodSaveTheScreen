#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Einfache Einstellungs-Fälschung mit echtem Setter-Verhalten.</summary>
internal sealed class FakeSettings : ISettingsService
{
    private AppSettings _current = new();

    public AppSettings Current
    {
        get => _current;
        set
        {
            _current = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int SaveCount { get; private set; }

    public event EventHandler? Changed;

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

public sealed class WindowHitTestTests
{
    private static WindowInfo Window(string title, int z, PixelRect bounds) => new(z + 1, title, "p", bounds, false, z);

    [Fact]
    public void Find_UeberlappendeFenster_LiefertDasOberste()
    {
        IReadOnlyList<WindowInfo> windows =
        [
            Window("hinten", 2, new PixelRect(0, 0, 500, 500)),
            Window("vorn", 0, new PixelRect(100, 100, 200, 200)),
            Window("mitte", 1, new PixelRect(50, 50, 400, 400)),
        ];

        WindowHitTest.Find(windows, 150, 150)!.Title.ShouldBe("vorn");
        WindowHitTest.Find(windows, 60, 60)!.Title.ShouldBe("mitte");
        WindowHitTest.Find(windows, 10, 10)!.Title.ShouldBe("hinten");
    }

    [Fact]
    public void Find_Reihenfolge_Der_Liste_Ist_Egal_ZIndexEntscheidet()
    {
        IReadOnlyList<WindowInfo> windows =
        [
            Window("unten", 5, new PixelRect(0, 0, 100, 100)),
            Window("oben", 1, new PixelRect(0, 0, 100, 100)),
        ];

        WindowHitTest.Find(windows, 10, 10)!.Title.ShouldBe("oben");
    }

    [Fact]
    public void Find_AusserhalbAllerFenster_LiefertNull()
    {
        WindowHitTest.Find([Window("a", 0, new PixelRect(0, 0, 10, 10))], 10, 10).ShouldBeNull();
    }

    [Fact]
    public void Find_NegativerMonitorUrsprung_TrifftFenster()
    {
        WindowHitTest.Find([Window("links", 0, new PixelRect(-1920, 0, 1920, 1080))], -5, 500)!.Title.ShouldBe("links");
    }
}

public sealed class FreeformPathTests
{
    private static readonly PixelRect Desktop = new(-100, -100, 400, 400);

    [Fact]
    public void Add_UnterMindestabstand_WirdIgnoriert()
    {
        var path = new FreeformPath(Desktop);
        path.Begin(new PointD(10, 10));

        path.Add(new PointD(11, 11)).ShouldBeFalse();
        path.Add(new PointD(12, 10)).ShouldBeTrue();
        path.Points.Count.ShouldBe(2);
    }

    [Fact]
    public void Add_AusserhalbDesDesktops_WirdGeklemmt()
    {
        var path = new FreeformPath(Desktop);
        path.Begin(new PointD(0, 0));
        path.Add(new PointD(9999, -9999));

        path.Points[1].ShouldBe(new PointD(299, -100));
    }

    [Fact]
    public void IsValid_ZuWenigePunkte_IstFalse()
    {
        var path = new FreeformPath(Desktop);
        path.Begin(new PointD(0, 0));
        path.Add(new PointD(50, 50));

        path.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Bounds_Pfad_UmschliesstAllePunkte()
    {
        var path = new FreeformPath(Desktop);
        path.Begin(new PointD(10, 10));
        path.Add(new PointD(40, 12));
        path.Add(new PointD(20, 60));

        path.IsValid.ShouldBeTrue();
        path.Bounds.ShouldBe(new PixelRect(10, 10, 30, 50));
    }
}

public sealed class WindowModeOverlayTests
{
    private static readonly PixelRect Desktop = new(0, 0, 1000, 800);

    private static WindowInfo Window(string title, int z, PixelRect bounds) => new(z + 1, title, "p", bounds, false, z);

    private static (SelectionOverlayViewModel Vm, List<SelectionResult?> Results) Create(CaptureMode mode, params WindowInfo[] windows)
    {
        var vm = new SelectionOverlayViewModel(new SelectionState(Desktop), mode, windows);
        var results = new List<SelectionResult?>();
        vm.Finished += results.Add;
        return (vm, results);
    }

    [Fact]
    public void PointerMove_Fenstermodus_HebtFensterUnterCursorHervor()
    {
        var (vm, _) = Create(CaptureMode.Window, Window("A", 0, new PixelRect(100, 100, 300, 200)));

        vm.PointerMove(new PointD(150, 150));
        vm.HoveredWindow!.Title.ShouldBe("A");
        vm.HighlightBounds.ShouldBe(new PixelRect(100, 100, 300, 200));

        vm.PointerMove(new PointD(900, 700));
        vm.HoveredWindow.ShouldBeNull();
    }

    [Fact]
    public void PointerUp_OhneZiehen_UebernimmtFenster()
    {
        var window = Window("A", 0, new PixelRect(100, 100, 300, 200));
        var (vm, results) = Create(CaptureMode.Window, window);

        vm.PointerMove(new PointD(150, 150));
        vm.PointerDown(new PointD(150, 150));
        vm.PointerUp();

        results.ShouldBe([new SelectionResult(new PixelRect(100, 100, 300, 200), window)]);
    }

    [Fact]
    public void Ziehen_ImFenstermodus_WechseltZurRechteckauswahl()
    {
        var (vm, results) = Create(CaptureMode.Window, Window("A", 0, new PixelRect(0, 0, 1000, 800)));

        vm.PointerMove(new PointD(100, 100));
        vm.PointerDown(new PointD(100, 100));
        vm.PointerMove(new PointD(180, 160));
        vm.PointerUp();

        vm.IsRectangleSelection.ShouldBeTrue();
        results.Count.ShouldBe(1);
        results[0]!.Window.ShouldBeNull();
        results[0]!.Bounds.ShouldBe(new PixelRect(100, 100, 80, 60));
    }

    [Fact]
    public void Freiform_ZiehenUndLoslassen_LiefertPolygon()
    {
        var (vm, results) = Create(CaptureMode.Freeform);

        vm.PointerDown(new PointD(100, 100));
        vm.PointerMove(new PointD(200, 100));
        vm.PointerMove(new PointD(200, 200));
        vm.PointerMove(new PointD(100, 200));
        vm.PointerUp();

        results.Count.ShouldBe(1);
        results[0]!.Polygon!.Count.ShouldBe(4);
        results[0]!.Bounds.ShouldBe(new PixelRect(100, 100, 100, 100));
    }

    [Fact]
    public void Freiform_ZuKurz_WirdVerworfen()
    {
        var (vm, results) = Create(CaptureMode.Freeform);

        vm.PointerDown(new PointD(100, 100));
        vm.PointerMove(new PointD(110, 100));
        vm.PointerUp();

        results.ShouldBeEmpty();
        vm.Path.HasPath.ShouldBeFalse();
    }

    [Fact]
    public void Confirm_ImFenstermodus_NimmtHervorgehobenesFenster()
    {
        var window = Window("A", 0, new PixelRect(10, 10, 100, 100));
        var (vm, results) = Create(CaptureMode.Window, window);

        vm.PointerMove(new PointD(20, 20));
        vm.Confirm();

        results.Count.ShouldBe(1);
        results[0]!.Window.ShouldBe(window);
    }
}

public sealed class StartViewModelTimerTests
{
    private static (StartViewModel Vm, FakeSettings Settings) Create(AppSettings? initial = null)
    {
        var settings = new FakeSettings();
        if (initial is not null)
        {
            settings.Current = initial;
        }

        var coordinator = TestCoordinator.Create(settings).Coordinator;
        return (new StartViewModel(coordinator, settings, Substitute.For<IAppShell>(), Substitute.For<IEditorService>(), Substitute.For<IDialogService>(), TestDoubles.EmptyRecents(), Substitute.For<IProjectService>()), settings);
    }

    [Fact]
    public void Standard_KeinTimer_DelayIstNull()
    {
        var (vm, _) = Create();

        vm.SelectedTimer.Seconds.ShouldBe(0);
        vm.DelaySeconds.ShouldBe(0);
        vm.IsCustomTimer.ShouldBeFalse();
    }

    [Fact]
    public void Voreinstellung_10Sekunden_WirdAusgewaehlt()
    {
        var (vm, _) = Create(new AppSettings { DefaultTimerSeconds = 10 });

        vm.DelaySeconds.ShouldBe(10);
    }

    [Fact]
    public void Benutzerdefiniert_NutztGespeichertenWert()
    {
        var (vm, _) = Create(new AppSettings { CustomTimerSeconds = 42 });

        vm.SelectedTimer = vm.TimerOptions[^1];

        vm.IsCustomTimer.ShouldBeTrue();
        vm.DelaySeconds.ShouldBe(42);
    }

    [Fact]
    public void CustomSecondsInput_WirdAufGrenzenBegrenzt()
    {
        var (vm, _) = Create();
        vm.SelectedTimer = vm.TimerOptions[^1];

        vm.CustomSecondsInput = 99999;
        vm.CustomSeconds.ShouldBe(3600);

        vm.CustomSecondsInput = -5;
        vm.CustomSeconds.ShouldBe(0);

        vm.CustomSecondsInput = null;
        vm.CustomSeconds.ShouldBe(0);
    }

    [Fact]
    public void CustomSeconds_Aenderung_WirdGespeichert()
    {
        var (vm, settings) = Create();
        vm.SelectedTimer = vm.TimerOptions[^1];

        vm.CustomSecondsInput = 8;

        settings.Current.CustomTimerSeconds.ShouldBe(8);
        settings.SaveCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Modi_EnthaltenAlleVierAufnahmearten()
    {
        var (vm, _) = Create();

        vm.Modes.Select(m => m.Mode).Distinct().Count().ShouldBe(4);
    }
}

/// <summary>Baut einen <see cref="CaptureCoordinator"/> mit Fälschungen für alle Abhängigkeiten.</summary>
internal sealed class TestCoordinator
{
    public required CaptureCoordinator Coordinator { get; init; }

    public required IScreenCaptureService Capture { get; init; }

    public required ICountdownService Countdown { get; init; }

    public required ISelectionService Selection { get; init; }

    public required IDialogService Dialogs { get; init; }

    public required IWindowVisibilityService Visibility { get; init; }

    public static TestCoordinator Create(ISettingsService settings, params IPostCaptureAction[] actions)
    {
        var capture = Substitute.For<IScreenCaptureService>();
        var monitor = new MonitorInfo(1, new PixelRect(0, 0, 100, 100), new PixelRect(0, 0, 100, 100), 96, 96, true, "M");
        capture.CaptureVirtualScreen(Arg.Any<bool>())
            .Returns(_ => new ScreenSnapshot(new PixelBuffer(100, 100), 0, 0, [monitor]));
        var monitors = Substitute.For<IMonitorService>();
        monitors.GetMonitorAtCursor().Returns(monitor);
        var countdown = Substitute.For<ICountdownService>();
        countdown.RunAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var selection = Substitute.For<ISelectionService>();
        var dialogs = Substitute.For<IDialogService>();
        var visibility = Substitute.For<IWindowVisibilityService>();
        visibility.HideAll().Returns(Substitute.For<IDisposable>());

        var coordinator = new CaptureCoordinator(
            capture,
            monitors,
            Substitute.For<IWindowContentCapture>(),
            visibility,
            countdown,
            selection,
            actions,
            settings,
            dialogs,
            NullLogger<CaptureCoordinator>.Instance);

        return new TestCoordinator
        {
            Coordinator = coordinator,
            Capture = capture,
            Countdown = countdown,
            Selection = selection,
            Dialogs = dialogs,
            Visibility = visibility,
        };
    }
}

public sealed class CaptureCoordinatorTests
{
    [Fact]
    public async Task StartAsync_AbbruchWaehrendCountdown_NimmtKeinenSnapshotAuf()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        t.Countdown.RunAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle, TimeSpan.FromSeconds(5)));

        t.Capture.DidNotReceive().CaptureVirtualScreen(Arg.Any<bool>());
        await t.Selection.DidNotReceive().SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<CancellationToken>());
        t.Coordinator.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_OhneTimer_ZaehltNichtHerunter()
    {
        var t = TestCoordinator.Create(new FakeSettings());

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.FullScreen));

        await t.Countdown.DidNotReceive().RunAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        t.Capture.Received(1).CaptureVirtualScreen(Arg.Any<bool>());
    }

    [Fact]
    public async Task StartAsync_MitTimer_ZaehltVorDemSnapshotHerunter()
    {
        var t = TestCoordinator.Create(new FakeSettings());

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.FullScreen, TimeSpan.FromSeconds(7)));

        Received.InOrder(() =>
        {
            t.Countdown.RunAsync(TimeSpan.FromSeconds(7), Arg.Any<CancellationToken>());
            t.Capture.CaptureVirtualScreen(Arg.Any<bool>());
        });
    }

    [Fact]
    public async Task StartAsync_Vollbild_FuehrtAktionenInReihenfolgeAus()
    {
        var order = new List<string>();
        var first = Substitute.For<IPostCaptureAction>();
        first.ExecuteAsync(Arg.Any<CaptureResultContext>()).Returns(_ =>
        {
            order.Add("1");
            return Task.CompletedTask;
        });
        var second = Substitute.For<IPostCaptureAction>();
        second.ExecuteAsync(Arg.Any<CaptureResultContext>()).Returns(_ =>
        {
            order.Add("2");
            return Task.CompletedTask;
        });
        var t = TestCoordinator.Create(new FakeSettings(), first, second);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.FullScreen));

        order.ShouldBe(["1", "2"]);
    }

    [Fact]
    public async Task StartAsync_ClipboardFehler_AutoSaveLaeuftTrotzdem()
    {
        var settings = new FakeSettings { Current = new AppSettings { CopyToClipboard = true } };
        var clipboard = Substitute.For<IClipboardService>();
        clipboard.When(c => c.SetImage(Arg.Any<PixelBuffer>())).Do(_ => throw new InvalidOperationException("gesperrt"));
        var autoSaved = false;
        var autoSave = Substitute.For<IPostCaptureAction>();
        autoSave.ExecuteAsync(Arg.Any<CaptureResultContext>()).Returns(_ =>
        {
            autoSaved = true;
            return Task.CompletedTask;
        });
        var t = TestCoordinator.Create(settings, new CopyToClipboardAction(settings, clipboard), autoSave);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.FullScreen));

        autoSaved.ShouldBeTrue();
        t.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartAsync_Rechteck_AbbruchInDerAuswahl_FuehrtKeineAktionenAus()
    {
        var action = Substitute.For<IPostCaptureAction>();
        var t = TestCoordinator.Create(new FakeSettings(), action);
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SelectionResult?>(null));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle));

        await action.DidNotReceive().ExecuteAsync(Arg.Any<CaptureResultContext>());
    }

    [Fact]
    public async Task StartAsync_Rechteck_SchneidetAuswahlAus()
    {
        CaptureResultContext? seen = null;
        var action = Substitute.For<IPostCaptureAction>();
        action.ExecuteAsync(Arg.Do<CaptureResultContext>(c => seen = c)).Returns(Task.CompletedTask);
        var t = TestCoordinator.Create(new FakeSettings(), action);
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), CaptureMode.Rectangle, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SelectionResult?>(new SelectionResult(new PixelRect(10, 10, 30, 20))));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle));

        seen!.Document.BaseImage.Width.ShouldBe(30);
        seen.Document.BaseImage.Height.ShouldBe(20);
    }

    [Fact]
    public async Task StartAsync_Freiform_LiefertTransparentenAussenbereich()
    {
        CaptureResultContext? seen = null;
        var action = Substitute.For<IPostCaptureAction>();
        action.ExecuteAsync(Arg.Do<CaptureResultContext>(c => seen = c)).Returns(Task.CompletedTask);
        var settings = new FakeSettings { Current = new AppSettings { FreeformAntiAlias = false } };
        var t = TestCoordinator.Create(settings, action);
        // Snapshot: weiß, undurchsichtig
        t.Capture.CaptureVirtualScreen(Arg.Any<bool>()).Returns(_ =>
        {
            var image = new PixelBuffer(100, 100);
            Array.Fill(image.Pixels, (byte)255);
            var monitor = new MonitorInfo(1, new PixelRect(0, 0, 100, 100), new PixelRect(0, 0, 100, 100), 96, 96, true, "M");
            return new ScreenSnapshot(image, 0, 0, [monitor]);
        });
        PointD[] triangle = [new(10, 10), new(60, 10), new(10, 60)];
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), CaptureMode.Freeform, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SelectionResult?>(new SelectionResult(new PixelRect(10, 10, 50, 50), Polygon: triangle)));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Freeform));

        var image = seen!.Document.BaseImage;
        image.Pixels[((5 * image.Width) + 5) * 4 + 3].ShouldBe((byte)255);
        image.Pixels[((45 * image.Width) + 45) * 4 + 3].ShouldBe((byte)0);
    }

    [Fact]
    public async Task StartAsync_WaehrendEinerAufnahme_WirdZweiterAuftragIgnoriert()
    {
        var gate = new TaskCompletionSource<SelectionResult?>();
        var t = TestCoordinator.Create(new FakeSettings());
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<CancellationToken>()).Returns(gate.Task);

        var first = t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle));
        while (!t.Coordinator.IsCapturing || !t.Capture.ReceivedCalls().Any())
        {
            await Task.Delay(10);
        }

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle));
        gate.SetResult(null);
        await first;

        t.Capture.Received(1).CaptureVirtualScreen(Arg.Any<bool>());
    }
}
