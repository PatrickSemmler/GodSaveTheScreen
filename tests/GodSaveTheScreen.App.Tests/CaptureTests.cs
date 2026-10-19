using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Tests;

public sealed class CoordinateMapperTests
{
    // Monitor 2: 150 % rechts vom primären 100-%-Monitor (1920 breit), 2880x1620 physische Pixel.
    private static readonly CoordinateMapper Right = new(new PixelRect(1920, 0, 2880, 1620), 1.5);

    // Monitor links vom primären: negativer Ursprung, 100 %.
    private static readonly CoordinateMapper Left = new(new PixelRect(-1280, -200, 1280, 1024), 1.0);

    [Fact]
    public void VirtualToLocalDips_Monitor150Prozent_TeiltDurchSkalierung()
    {
        var local = Right.VirtualToLocalDips(new PointD(1920 + 300, 150));

        local.ShouldBe(new PointD(200, 100));
    }

    [Fact]
    public void LocalDipsToVirtual_Monitor150Prozent_IstUmkehrung()
    {
        Right.LocalDipsToVirtual(new PointD(200, 100)).ShouldBe(new PointD(2220, 150));
    }

    [Fact]
    public void VirtualToLocalDips_NegativerUrsprung_LiefertNichtNegativeLokalkoordinaten()
    {
        Left.VirtualToLocalDips(new PointD(-1280, -200)).ShouldBe(new PointD(0, 0));
        Left.VirtualToLocalDips(new PointD(-1, 0)).ShouldBe(new PointD(1279, 200));
    }

    [Fact]
    public void VirtualToLocalDips_Rechteck_SkaliertAuchGroesse()
    {
        var rect = Right.VirtualToLocalDips(new RectD(1920 + 150, 30, 300, 150));

        rect.ShouldBe(new RectD(100, 20, 200, 100));
    }

    [Fact]
    public void LocalSizeDips_Monitor150Prozent_IstPhysischeGroesseDurchSkalierung()
    {
        Right.LocalSizeDips.ShouldBe(new SizeD(1920, 1080));
    }

    [Fact]
    public void VirtualToSnapshot_NegativerVirtuellerUrsprung_VerschiebtUmUrsprung()
    {
        CoordinateMapper.VirtualToSnapshot(new PointD(-1280, -200), -1280, -200).ShouldBe(new PointD(0, 0));
        CoordinateMapper.VirtualToSnapshot(new PointD(10, 20), -1280, -200).ShouldBe(new PointD(1290, 220));
    }
}

public sealed class SelectionStateTests
{
    private static SelectionState Create() => new(new PixelRect(-100, -50, 400, 300));

    [Fact]
    public void Selection_RueckwaertsZiehen_WirdNormalisiert()
    {
        var state = Create();
        state.Begin(new PointD(50, 60));
        state.MoveCursor(new PointD(20, 10));

        state.Selection.ShouldBe(new PixelRect(20, 10, 30, 50));
    }

    [Fact]
    public void Selection_ZiehenUeberDenRand_WirdAnDesktopgrenzenGeklemmt()
    {
        var state = Create();
        state.Begin(new PointD(0, 0));
        state.MoveCursor(new PointD(5000, 5000));

        state.Selection.ShouldBe(new PixelRect(0, 0, 300, 250));
    }

    [Fact]
    public void Selection_NegativerUrsprung_WirdUnterstuetzt()
    {
        var state = Create();
        state.Begin(new PointD(-90, -40));
        state.MoveCursor(new PointD(-50, -10));

        state.Selection.ShouldBe(new PixelRect(-90, -40, 40, 30));
    }

    [Fact]
    public void IsValid_UnterMindestgroesse_IstFalse()
    {
        var state = Create();
        state.Begin(new PointD(10, 10));
        state.MoveCursor(new PointD(11, 40));

        state.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_AbMindestgroesse_IstTrue()
    {
        var state = Create();
        state.Begin(new PointD(10, 10));
        state.MoveCursor(new PointD(13, 13));

        state.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void NudgeEnd_VerschiebtEndpunktPixelgenau()
    {
        var state = Create();
        state.Begin(new PointD(0, 0));
        state.MoveCursor(new PointD(10, 10));
        state.NudgeEnd(1, -2);

        state.Selection.ShouldBe(new PixelRect(0, 0, 11, 8));
    }

    [Fact]
    public void MoveCursor_OhneBegin_VeraendertAuswahlNicht()
    {
        var state = Create();
        state.MoveCursor(new PointD(10, 10));

        state.HasSelection.ShouldBeFalse();
        state.Selection.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Cursor_AusserhalbDesDesktops_WirdGeklemmt()
    {
        var state = Create();
        state.MoveCursor(new PointD(9999, -9999));

        state.Cursor.ShouldBe(new PointD(299, -50));
    }

    [Fact]
    public void Changed_WirdBeiAenderungenAusgeloest()
    {
        var state = Create();
        var count = 0;
        state.Changed += (_, _) => count++;

        state.Begin(new PointD(1, 1));
        state.MoveCursor(new PointD(5, 5));
        state.End();

        count.ShouldBe(3);
    }
}

public sealed class SelectionOverlayViewModelTests
{
    private static (SelectionOverlayViewModel Vm, List<SelectionResult?> Results) Create()
    {
        var vm = new SelectionOverlayViewModel(new SelectionState(new PixelRect(0, 0, 1000, 800)));
        var results = new List<SelectionResult?>();
        vm.Finished += results.Add;
        return (vm, results);
    }

    [Fact]
    public void PointerUp_GueltigeAuswahl_LiefertErgebnisGenauEinmal()
    {
        var (vm, results) = Create();
        vm.PointerDown(new PointD(10, 10));
        vm.PointerMove(new PointD(110, 60));
        vm.PointerUp();
        vm.Cancel();

        results.ShouldBe([new SelectionResult(new PixelRect(10, 10, 100, 50))]);
        vm.SizeText.ShouldBe("100 × 50");
    }

    [Fact]
    public void PointerUp_ZuKleineAuswahl_WirdVerworfenOhneErgebnis()
    {
        var (vm, results) = Create();
        vm.PointerDown(new PointD(10, 10));
        vm.PointerUp();

        results.ShouldBeEmpty();
        vm.State.HasSelection.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_LiefertNull()
    {
        var (vm, results) = Create();
        vm.Cancel();

        results.ShouldBe([null]);
    }
}
