using System.Globalization;
using GodSaveTheScreen.Core.Hotkeys;

namespace GodSaveTheScreen.Core.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("Alt+Snapshot")]
    [InlineData("Snapshot")]
    [InlineData("Ctrl+Alt+Shift+Win+F5")]
    [InlineData("Ctrl+D1")]
    public void ToString_NachTryParse_IstRoundtrip(string text)
    {
        var gesture = HotkeyGesture.TryParse(text);

        gesture.ShouldNotBeNull();
        gesture.ToString().ShouldBe(text);
    }

    [Fact]
    public void TryParse_ReihenfolgeUndGrossschreibung_WirdNormalisiert()
    {
        HotkeyGesture.TryParse("shift+ctrl+s")!.ToString().ShouldBe("Ctrl+Shift+s");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("S")]
    [InlineData("Ctrl+A+B")]
    public void TryParse_Ungueltig_LiefertNull(string? text)
    {
        HotkeyGesture.TryParse(text).ShouldBeNull();
    }

    [Fact]
    public void ToDisplayString_Deutsch_NutztLokalisierteNamen()
    {
        var gesture = HotkeyGesture.TryParse("Ctrl+Shift+Snapshot")!;

        gesture.ToDisplayString(new CultureInfo("de-DE")).ShouldBe("Strg+Umschalt+Druck");
        gesture.ToDisplayString(new CultureInfo("en-US")).ShouldBe("Ctrl+Shift+PrtSc");
    }

    [Fact]
    public void ToDisplayString_Ziffer_ZeigtOhneD()
    {
        HotkeyGesture.TryParse("Ctrl+D3")!.ToDisplayString(new CultureInfo("en-US")).ShouldBe("Ctrl+3");
    }

    [Fact]
    public void Gleichheit_GleicheKombination_IstGleich()
    {
        HotkeyGesture.TryParse("Ctrl+Shift+S").ShouldBe(HotkeyGesture.TryParse("shift+CTRL+S"));
    }
}
