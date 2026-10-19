#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

public sealed class SettingsViewModelTests
{
    private static (SettingsViewModel Vm, FakeSettings Settings, IHotkeyService Hotkeys, IAutostartService Autostart) Create()
    {
        var settings = new FakeSettings();
        var hotkeys = Substitute.For<IHotkeyService>();
        hotkeys.Statuses.Returns(new Dictionary<CaptureMode, HotkeyStatus>
        {
            [CaptureMode.Rectangle] = HotkeyStatus.Registered,
            [CaptureMode.Window] = HotkeyStatus.Conflict,
        });
        var autostart = Substitute.For<IAutostartService>();
        var paths = Substitute.For<IAppPaths>();
        var vm = new SettingsViewModel(settings, autostart, hotkeys, Substitute.For<IThemeService>(), paths, Substitute.For<IDialogService>());
        return (vm, settings, hotkeys, autostart);
    }

    [Fact]
    public void Aenderung_WirdSofortUebernommenUndGespeichert()
    {
        var (vm, settings, _, _) = Create();

        vm.CopyToClipboard = true;
        vm.AutoSave = true;

        settings.Current.CopyToClipboard.ShouldBeTrue();
        settings.Current.AutoSave.ShouldBeTrue();
        settings.SaveCount.ShouldBe(2);
    }

    [Fact]
    public void GleicherWert_SpeichertNichtErneut()
    {
        var (vm, settings, _, _) = Create();

        vm.CaptureCursor = false;

        settings.SaveCount.ShouldBe(0);
    }

    [Fact]
    public void Sprachwechsel_ZeigtNeustartHinweis()
    {
        var (vm, _, _, _) = Create();
        vm.RestartRequired.ShouldBeFalse();

        vm.Language = vm.Language == "de" ? "en" : "de";

        vm.RestartRequired.ShouldBeTrue();
    }

    [Fact]
    public void Autostart_SchreibtUeberDenDienst()
    {
        var (vm, settings, _, autostart) = Create();

        vm.Autostart = true;

        autostart.Received(1).SetEnabled(true);
        settings.Current.Autostart.ShouldBeTrue();
    }

    [Fact]
    public void Hotkey_Aenderung_WirdGespeichertUndNeuRegistriert()
    {
        var (vm, settings, hotkeys, _) = Create();

        vm.Hotkeys.First(h => h.Mode == CaptureMode.Rectangle).Gesture = "Ctrl+Alt+R";

        settings.Current.HotkeyRectangle.ShouldBe("Ctrl+Alt+R");
        hotkeys.Received().Apply();
    }

    [Fact]
    public void Hotkey_DoppeltInDerApp_WirdAbgelehntUndZurueckgesetzt()
    {
        var (vm, settings, _, _) = Create();
        var window = vm.Hotkeys.First(h => h.Mode == CaptureMode.Window);

        window.Gesture = settings.Current.HotkeyRectangle;

        window.Gesture.ShouldBe(new AppSettings().HotkeyWindow);
        settings.Current.HotkeyWindow.ShouldBe(new AppSettings().HotkeyWindow);
        window.IsProblem.ShouldBeTrue();
        window.Message.ShouldNotBeNull();
    }

    [Fact]
    public void Hotkey_Loeschen_LeertDieEinstellung()
    {
        var (vm, settings, _, _) = Create();

        vm.Hotkeys.First(h => h.Mode == CaptureMode.Freeform).Gesture = string.Empty;

        settings.Current.HotkeyFreeform.ShouldBeEmpty();
    }

    [Fact]
    public void Status_BelegterHotkey_WirdAngezeigt()
    {
        var (vm, _, _, _) = Create();

        vm.Hotkeys.First(h => h.Mode == CaptureMode.Window).Status.ShouldBe(HotkeyStatus.Conflict);
        vm.Hotkeys.First(h => h.Mode == CaptureMode.Window).IsProblem.ShouldBeTrue();
        vm.Hotkeys.First(h => h.Mode == CaptureMode.Rectangle).IsProblem.ShouldBeFalse();
    }

    [Fact]
    public void PrintHinweis_NurBeiBelegterDruckTaste()
    {
        var (vm, _, _, _) = Create();
        var entry = vm.Hotkeys.First(h => h.Mode == CaptureMode.Window);

        entry.Gesture = "Snapshot";
        entry.ShowPrintHint.ShouldBeTrue();

        entry.Gesture = "Ctrl+Shift+2";
        entry.ShowPrintHint.ShouldBeFalse();
    }

    [Fact]
    public void FindConflict_ErkenntGleicheKombinationUnabhaengigVonSchreibweise()
    {
        var entries = new[] { (CaptureMode.Rectangle, "Ctrl+Shift+1"), (CaptureMode.Window, "Alt+X") };

        SettingsViewModel.FindConflict(entries, CaptureMode.Freeform, "shift+ctrl+1").ShouldBe(CaptureMode.Rectangle);
        SettingsViewModel.FindConflict(entries, CaptureMode.Rectangle, "Ctrl+Shift+1").ShouldBeNull();
        SettingsViewModel.FindConflict(entries, CaptureMode.Freeform, string.Empty).ShouldBeNull();
    }
}

public sealed class AutoSaveActionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CaptureResultContext Context() =>
        new(new ImageDocument(new PixelBuffer(4, 4)), CaptureMode.Rectangle, null, new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero));

    [StaFact]
    public async Task ExecuteAsync_Aktiv_SchreibtDateiNachMusterUndBenachrichtigt()
    {
        var settings = new FakeSettings
        {
            Current = new AppSettings { AutoSave = true, AutoSaveDirectory = _directory, FileNamePattern = "Test {date:yyyy-MM-dd}" },
        };
        var notifications = Substitute.For<INotificationService>();
        var action = new AutoSaveAction(settings, notifications, NullLogger<AutoSaveAction>.Instance);

        await action.ExecuteAsync(Context());

        var expected = Path.Combine(_directory, "Test 2026-10-19.png");
        File.Exists(expected).ShouldBeTrue();
        notifications.Received(1).ShowSaved(expected);
    }

    [StaFact]
    public async Task ExecuteAsync_ZweiteAufnahme_HaengtSuffixAn()
    {
        var settings = new FakeSettings
        {
            Current = new AppSettings { AutoSave = true, AutoSaveDirectory = _directory, FileNamePattern = "Test" },
        };
        var action = new AutoSaveAction(settings, Substitute.For<INotificationService>(), NullLogger<AutoSaveAction>.Instance);

        await action.ExecuteAsync(Context());
        await action.ExecuteAsync(Context());

        File.Exists(Path.Combine(_directory, "Test.png")).ShouldBeTrue();
        File.Exists(Path.Combine(_directory, "Test (2).png")).ShouldBeTrue();
    }

    [StaFact]
    public async Task ExecuteAsync_Jpeg_NutztJpgEndung()
    {
        var settings = new FakeSettings
        {
            Current = new AppSettings { AutoSave = true, AutoSaveDirectory = _directory, FileNamePattern = "Foto", AutoSaveFormat = ImageFormatSetting.Jpeg },
        };
        var action = new AutoSaveAction(settings, Substitute.For<INotificationService>(), NullLogger<AutoSaveAction>.Instance);

        await action.ExecuteAsync(Context());

        File.Exists(Path.Combine(_directory, "Foto.jpg")).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Deaktiviert_SchreibtNichts()
    {
        var settings = new FakeSettings { Current = new AppSettings { AutoSave = false, AutoSaveDirectory = _directory } };
        var action = new AutoSaveAction(settings, Substitute.For<INotificationService>(), NullLogger<AutoSaveAction>.Instance);

        await action.ExecuteAsync(Context());

        Directory.Exists(_directory).ShouldBeFalse();
    }

    [Fact]
    public void ResolveDirectory_Leer_NutztStandardordner()
    {
        AutoSaveAction.ResolveDirectory(new AppSettings()).ShouldEndWith(Path.Combine("Screenshots", "GodSaveTheScreen"));
        AutoSaveAction.ResolveDirectory(new AppSettings { AutoSaveDirectory = @"D:\x" }).ShouldBe(@"D:\x");
    }
}

public sealed class CopyToClipboardActionTests
{
    [Fact]
    public async Task ExecuteAsync_Aktiv_KopiertBild()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var action = new CopyToClipboardAction(new FakeSettings { Current = new AppSettings { CopyToClipboard = true } }, clipboard);

        await action.ExecuteAsync(new CaptureResultContext(new ImageDocument(new PixelBuffer(2, 2)), CaptureMode.Rectangle, null, DateTimeOffset.Now));

        clipboard.Received(1).SetImage(Arg.Any<PixelBuffer>());
    }

    [Fact]
    public async Task ExecuteAsync_Inaktiv_KopiertNichts()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var action = new CopyToClipboardAction(new FakeSettings(), clipboard);

        await action.ExecuteAsync(new CaptureResultContext(new ImageDocument(new PixelBuffer(2, 2)), CaptureMode.Rectangle, null, DateTimeOffset.Now));

        clipboard.DidNotReceive().SetImage(Arg.Any<PixelBuffer>());
    }
}

public sealed class AutostartServiceTests
{
    private sealed class FakeRegistry : IAutostartRegistry
    {
        public Dictionary<string, string> Values { get; } = [];

        public string? Read(string name) => Values.GetValueOrDefault(name);

        public void Write(string name, string value) => Values[name] = value;

        public void Remove(string name) => Values.Remove(name);
    }

    [Fact]
    public void SetEnabled_SchreibtUndLoeschtEintragMitTrayParameter()
    {
        var registry = new FakeRegistry();
        var service = new AutostartService(registry, @"C:\App\GodSaveTheScreen.exe");

        service.SetEnabled(true);
        service.IsEnabled.ShouldBeTrue();
        registry.Values[AutostartService.ValueName].ShouldBe("\"C:\\App\\GodSaveTheScreen.exe\" --tray");

        service.SetEnabled(false);
        service.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void RepairPath_VerschobeneExe_KorrigiertEintrag()
    {
        var registry = new FakeRegistry();
        new AutostartService(registry, @"C:\Alt\GodSaveTheScreen.exe").SetEnabled(true);

        new AutostartService(registry, @"D:\Neu\GodSaveTheScreen.exe").RepairPath();

        registry.Values[AutostartService.ValueName].ShouldBe("\"D:\\Neu\\GodSaveTheScreen.exe\" --tray");
    }

    [Fact]
    public void RepairPath_OhneEintrag_LegtNichtsAn()
    {
        var registry = new FakeRegistry();

        new AutostartService(registry, @"D:\Neu\GodSaveTheScreen.exe").RepairPath();

        registry.Values.ShouldBeEmpty();
    }
}

public sealed class CommandLineTrayTests
{
    [Fact]
    public void Parse_Tray_WirdErkannt()
    {
        CommandLineParser.Parse(["--tray"]).Tray.ShouldBeTrue();
        CommandLineParser.Parse([]).Tray.ShouldBeFalse();
    }

    [Fact]
    public void Parse_TrayMitCapture_KeinFehler()
    {
        var options = CommandLineParser.Parse(["--tray", "--capture", "window", "--delay", "3"]);

        options.Errors.ShouldBeEmpty();
        options.Capture.ShouldBe(CaptureMode.Window);
        options.DelaySeconds.ShouldBe(3);
    }
}

public sealed class CaptureRequestTests
{
    [Fact]
    public void FromSettings_NutztStandardTimer()
    {
        var request = CaptureRequest.FromSettings(new AppSettings { DefaultTimerSeconds = 5 }, CaptureMode.Window);

        request.Mode.ShouldBe(CaptureMode.Window);
        request.Delay.ShouldBe(TimeSpan.FromSeconds(5));
    }
}
