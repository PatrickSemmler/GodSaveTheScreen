using GodSaveTheScreen.Capture;
using Shouldly;

namespace GodSaveTheScreen.App.Tests;

public sealed class WindowEnumeratorTests
{
    private const int Layered = 0x80000;
    private const int Transparent = 0x20;
    private const int NoActivate = 0x8000000;
    private const int TopMost = 0x8;

    [Fact]
    public void DurchklickbareUeberlagerung_WirdErkannt() =>
        WindowEnumerator.IsClickThroughOverlay(Layered | Transparent | NoActivate | TopMost).ShouldBeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(Layered)]
    [InlineData(Transparent)]
    [InlineData(Layered | NoActivate | TopMost)]
    public void NurLayeredOderNurTransparent_BleibtZiel(int exStyle) =>
        WindowEnumerator.IsClickThroughOverlay(exStyle).ShouldBeFalse();
}
