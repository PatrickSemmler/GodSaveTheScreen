using GodSaveTheScreen.Core.Naming;

namespace GodSaveTheScreen.Core.Tests;

public sealed class FileNamePatternTests
{
    private static readonly FileNameContext Context = new(new DateTimeOffset(2026, 10, 19, 14, 5, 9, TimeSpan.Zero), "Rechteck", "Mein: Fenster?");

    [Fact]
    public void Render_DatumUndZeit_NutztFormat()
    {
        FileNamePattern.Render("{date:yyyy-MM-dd}_{time:HH-mm-ss}", Context).ShouldBe("2026-10-19_14-05-09");
    }

    [Fact]
    public void Render_ModusUndFenster_UngueltigeZeichenWerdenErsetzt()
    {
        FileNamePattern.Render("{mode} - {window}", Context).ShouldBe("Rechteck - Mein_ Fenster_");
    }

    [Fact]
    public void Render_Zaehler_NutztFormat()
    {
        FileNamePattern.Render("Bild {counter:000}", Context, counter: 7).ShouldBe("Bild 007");
    }

    [Fact]
    public void Render_UnbekannterPlatzhalter_BleibtErhalten()
    {
        FileNamePattern.Render("a{foo}b", Context).ShouldBe("a{foo}b");
    }

    [Fact]
    public void Render_FensterOhneTitel_WirdLeer()
    {
        FileNamePattern.Render("x{window}y", Context with { WindowTitle = null }).ShouldBe("xy");
    }

    [Fact]
    public void Render_LeeresMuster_NutztStandard()
    {
        FileNamePattern.Render(" ", Context).ShouldBe("Screenshot 2026-10-19 14-05-09");
    }

    [Fact]
    public void Sanitize_NurUngueltigeZeichen_LiefertStandardnamen()
    {
        FileNamePattern.Sanitize("...").ShouldBe("Screenshot");
    }

    [Fact]
    public void Sanitize_UeberlangerName_WirdGekuerzt()
    {
        FileNamePattern.Sanitize(new string('a', 500)).Length.ShouldBeLessThanOrEqualTo(120);
    }

    [Fact]
    public void BuildPath_FreierName_WirdUnveraendertVerwendet()
    {
        var path = FileNamePattern.BuildPath("Shot", @"C:\out", "png", Context, _ => false);

        path.ShouldBe(Path.Combine(@"C:\out", "Shot.png"));
    }

    [Fact]
    public void BuildPath_Kollision_HaengtSuffixAn()
    {
        var existing = new HashSet<string> { Path.Combine(@"C:\out", "Shot.png"), Path.Combine(@"C:\out", "Shot (2).png") };

        var path = FileNamePattern.BuildPath("Shot", @"C:\out", ".png", Context, existing.Contains);

        path.ShouldBe(Path.Combine(@"C:\out", "Shot (3).png"));
    }

    [Fact]
    public void BuildPath_MitZaehler_WaehltKleinsteFreieNummer()
    {
        var existing = new HashSet<string> { Path.Combine(@"C:\out", "Bild 001.png"), Path.Combine(@"C:\out", "Bild 002.png") };

        var path = FileNamePattern.BuildPath("Bild {counter:000}", @"C:\out", "png", Context, existing.Contains);

        path.ShouldBe(Path.Combine(@"C:\out", "Bild 003.png"));
    }

    [Fact]
    public void ContainsCounter_ErkenntPlatzhalter()
    {
        FileNamePattern.ContainsCounter("a {counter:00}").ShouldBeTrue();
        FileNamePattern.ContainsCounter("a {date}").ShouldBeFalse();
    }
}
