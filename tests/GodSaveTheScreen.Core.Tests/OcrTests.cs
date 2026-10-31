using System.Text.Json;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

internal static class OcrLines
{
    public static OcrLine Line(string text, double x, double y, double height = 14)
    {
        var words = new List<OcrWord>();
        var cursor = x;
        foreach (var part in text.Split(' '))
        {
            var width = Math.Max(8, part.Length * 7.0);
            words.Add(new OcrWord(part, new RectD(cursor, y, width, height)));
            cursor += width + 5;
        }

        return new OcrLine(text, new RectD(x, y, cursor - 5 - x, height), words);
    }

    public static OcrLine Cell(string text, double x, double y, double width, double height = 14) =>
        new(text, new RectD(x, y, width, height), [new OcrWord(text, new RectD(x, y, width, height))]);
}

public sealed class OcrTextBuilderTests
{
    [Fact]
    public void LeereListe_GibtLeerenText()
    {
        OcrTextBuilder.Build([]).ShouldBeEmpty();
        OcrTextBuilder.Build([OcrLines.Line("   ", 0, 0)]).ShouldBeEmpty();
    }

    [Fact]
    public void Zeilen_VonObenNachUnten_AuchWennUnsortiert()
    {
        var text = OcrTextBuilder.Build([OcrLines.Line("zweite", 5, 40), OcrLines.Line("erste", 5, 20), OcrLines.Line("dritte", 5, 60)]);

        text.ShouldBe("erste\nzweite\ndritte");
    }

    [Fact]
    public void GrosserAbstand_ErzeugtEineLeerzeile()
    {
        // Zeilenhöhe 14: normale Zeilen im Abstand 20, Absatz mit 44 (Lücke 30 > 0,75 · 14).
        var text = OcrTextBuilder.Build([OcrLines.Line("a1", 0, 0), OcrLines.Line("a2", 0, 20), OcrLines.Line("b1", 0, 64), OcrLines.Line("b2", 0, 84)]);

        text.ShouldBe("a1\na2\n\nb1\nb2");
    }

    [Fact]
    public void Zeilenabstand_UnterDerGrenze_ErzeugtKeineLeerzeile()
    {
        OcrTextBuilder.Build([OcrLines.Line("a", 0, 0), OcrLines.Line("b", 0, 24)]).ShouldBe("a\nb");
    }

    [Fact]
    public void OhneTabulator_BleibenZeilenGleicherHoeheSeparat_VonLinksNachRechts()
    {
        var text = OcrTextBuilder.Build([OcrLines.Cell("Größe", 300, 0, 40), OcrLines.Cell("Name", 0, 1, 40)]);

        text.ShouldBe("Name\nGröße");
    }

    [Fact]
    public void MitTabulator_WerdenZeilenGleicherHoeheZuEinerReiheMitTabs()
    {
        var text = OcrTextBuilder.Build(
            [
                OcrLines.Cell("Name", 0, 0, 40), OcrLines.Cell("Datum", 200, 1, 40), OcrLines.Cell("Größe", 400, 0, 40),
                OcrLines.Cell("a.txt", 0, 20, 40), OcrLines.Cell("01.01.", 200, 20, 40), OcrLines.Cell("4 KB", 400, 21, 40),
            ],
            tabSeparatedColumns: true);

        text.ShouldBe("Name\tDatum\tGröße\na.txt\t01.01.\t4 KB");
    }

    [Fact]
    public void MitTabulator_KleineWortabstaende_BleibenLeerzeichen()
    {
        OcrTextBuilder.Build([OcrLines.Line("ein Satz mit Wörtern", 0, 0)], tabSeparatedColumns: true).ShouldBe("ein Satz mit Wörtern");
    }

    [Fact]
    public void MitTabulator_GrosseLueckeInnerhalbEinerZeile_WirdTab()
    {
        var line = new OcrLine("A B", new RectD(0, 0, 400, 14), [new OcrWord("A", new RectD(0, 0, 10, 14)), new OcrWord("B", new RectD(300, 0, 10, 14))]);

        OcrTextBuilder.Build([line], tabSeparatedColumns: true).ShouldBe("A\tB");
    }

    [Fact]
    public void Reihenfolge_ZeilenAufGleicherHoehe_SindNachXSortiert()
    {
        var rows = OcrTextBuilder.GroupRows([OcrLines.Cell("rechts", 200, 0, 40), OcrLines.Cell("links", 0, 2, 40), OcrLines.Cell("unten", 0, 30, 40)]);

        rows.Count.ShouldBe(2);
        rows[0].Select(l => l.Text).ShouldBe(["links", "rechts"]);
    }

    [Fact]
    public void TypischeHoehe_IstDerMedian()
    {
        OcrTextBuilder.TypicalHeight([OcrLines.Line("a", 0, 0, 10), OcrLines.Line("b", 0, 20, 12), OcrLines.Line("c", 0, 40, 100)]).ShouldBe(12);
        OcrTextBuilder.TypicalHeight([]).ShouldBe(1);
    }
}

public sealed class OcrTilingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(9999)]
    [InlineData(10000)]
    public void BisZehntausend_EinStreifen(int height)
    {
        OcrTiling.Plan(height).ShouldBe([new OcrTile(0, height)]);
    }

    [Fact]
    public void EinPixelDarueber_ZweiStreifenMitUeberlappung()
    {
        var tiles = OcrTiling.Plan(10001);

        tiles.Count.ShouldBe(2);
        tiles[0].ShouldBe(new OcrTile(0, 10000));
        tiles[1].Bottom.ShouldBe(10001);
        tiles[1].Top.ShouldBeLessThan(tiles[0].Bottom);
        (tiles[0].Bottom - tiles[1].Top).ShouldBe(OcrTiling.DefaultOverlap);
    }

    [Theory]
    [InlineData(16384)]
    [InlineData(25000)]
    [InlineData(60000)]
    public void Streifen_DeckenDasBildLueckenlosAb_UndSindNieZuGross(int height)
    {
        var tiles = OcrTiling.Plan(height);

        tiles[0].Top.ShouldBe(0);
        tiles[^1].Bottom.ShouldBe(height);
        tiles.ShouldAllBe(t => t.Height <= OcrTiling.MaxSide && t.Height > 0);
        for (var i = 1; i < tiles.Count; i++)
        {
            tiles[i].Top.ShouldBeLessThan(tiles[i - 1].Bottom);
            tiles[i].Top.ShouldBeGreaterThan(tiles[i - 1].Top);
        }
    }

    [Fact]
    public void KleineGrenze_UeberlappungWirdBegrenzt()
    {
        var tiles = OcrTiling.Plan(100, maxSide: 30, overlap: 500);

        tiles[^1].Bottom.ShouldBe(100);
        tiles.Count.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void Merge_EntferntDoppelteZeilenImUeberlappungsbereich()
    {
        var tiles = OcrTiling.Plan(250, maxSide: 150, overlap: 50);
        tiles.Count.ShouldBe(2);

        // Zeilen im Abstand von 20 über das ganze Bild (Mitten bei 10, 30, ... 230). Jeder Streifen sieht seine Zeilen
        // in lokalen Koordinaten, die im Überlappungsbereich doppelt vorkommen.
        var global = Enumerable.Range(0, 12).Select(i => OcrLines.Line($"Z{i}", 0, (i * 20) + 3, height: 14)).ToList();
        var perTile = tiles
            .Select(t => (IReadOnlyList<OcrLine>)[.. global
                .Where(l => l.Bounds.Y >= t.Top && l.Bounds.Bottom <= t.Bottom)
                .Select(l => OcrTiling.Shift(l, -t.Top))])
            .ToList();
        perTile.Sum(p => p.Count).ShouldBeGreaterThan(global.Count);

        var merged = OcrTiling.Merge(tiles, perTile);

        merged.Select(l => l.Text).ShouldBe(global.Select(l => l.Text));
        merged.Select(l => l.Bounds.Y).ShouldBe(global.Select(l => l.Bounds.Y));
    }

    [Fact]
    public void Merge_RechnetKoordinatenInsGesamtbildZurueck_AuchBeiWoertern()
    {
        var tiles = OcrTiling.Plan(20000);
        var local = OcrLines.Line("hallo welt", 10, 1000);
        var perTile = tiles.Select((_, i) => (IReadOnlyList<OcrLine>)(i == 1 ? [local] : [])).ToList();

        var merged = OcrTiling.Merge(tiles, perTile).ShouldHaveSingleItem();

        merged.Bounds.Y.ShouldBe(tiles[1].Top + 1000);
        merged.Words.ShouldAllBe(w => w.Bounds.Y == tiles[1].Top + 1000);
        merged.Bounds.X.ShouldBe(10);
    }

    [Fact]
    public void Merge_DreiStreifen_JedeZeileGenauEinmal()
    {
        var tiles = OcrTiling.Plan(30000, maxSide: 10000, overlap: 400);
        tiles.Count.ShouldBe(4);
        var global = Enumerable.Range(0, 1400).Select(i => OcrLines.Line($"Z{i}", 0, (i * 21) + 2)).ToList();
        var perTile = tiles
            .Select(t => (IReadOnlyList<OcrLine>)[.. global
                .Where(l => l.Bounds.Y >= t.Top && l.Bounds.Bottom <= t.Bottom)
                .Select(l => OcrTiling.Shift(l, -t.Top))])
            .ToList();

        var merged = OcrTiling.Merge(tiles, perTile);

        merged.Select(l => l.Text).ShouldBe(global.Select(l => l.Text));
    }

    [Fact]
    public void Merge_ZuWenigListen_WirftFehler()
    {
        Should.Throw<ArgumentException>(() => OcrTiling.Merge(OcrTiling.Plan(20000), [[]]));
    }
}

public sealed class OcrImagePrepTests
{
    private static PixelBuffer Solid(int width, int height, byte b, byte g, byte r, byte a = 255)
    {
        var buffer = new PixelBuffer(width, height);
        for (var i = 0; i < width * height; i++)
        {
            buffer.Pixels[i * 4] = b;
            buffer.Pixels[(i * 4) + 1] = g;
            buffer.Pixels[(i * 4) + 2] = r;
            buffer.Pixels[(i * 4) + 3] = a;
        }

        return buffer;
    }

    [Fact]
    public void FlattenOnWhite_TransparenteStellenWerdenWeiss()
    {
        var flat = OcrImagePrep.FlattenOnWhite(Solid(3, 2, 0, 0, 0, a: 0));

        flat.Pixels.Where((_, i) => i % 4 != 3).ShouldAllBe(v => v == 255);
        flat.Pixels.Where((_, i) => i % 4 == 3).ShouldAllBe(v => v == 255);
    }

    [Fact]
    public void FlattenOnWhite_DeckendeBleibenUnveraendert_HalbtransparenteMischenMitWeiss()
    {
        var solid = OcrImagePrep.FlattenOnWhite(Solid(2, 2, 10, 20, 30));
        solid.Pixels[..4].ShouldBe(new byte[] { 10, 20, 30, 255 });

        var half = OcrImagePrep.FlattenOnWhite(Solid(1, 1, 0, 0, 0, a: 128));
        half.Pixels[0].ShouldBeInRange((byte)126, (byte)128);
        half.Pixels[3].ShouldBe((byte)255);
    }

    [Fact]
    public void FlattenOnWhite_VeraendertDasOriginalNicht()
    {
        var source = Solid(2, 2, 0, 0, 0, a: 0);

        _ = OcrImagePrep.FlattenOnWhite(source);

        source.Pixels[3].ShouldBe((byte)0);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Vergroessern_BehaeltEinfarbigeFlaechenUndGroesse(int factor)
    {
        var scaled = OcrImagePrep.Resize(Solid(20, 10, 40, 80, 120), 20 * factor, 10 * factor);

        scaled.Width.ShouldBe(20 * factor);
        scaled.Height.ShouldBe(10 * factor);
        for (var i = 0; i < scaled.Width * scaled.Height; i++)
        {
            scaled.Pixels[i * 4].ShouldBe((byte)40);
            scaled.Pixels[(i * 4) + 1].ShouldBe((byte)80);
            scaled.Pixels[(i * 4) + 2].ShouldBe((byte)120);
            scaled.Pixels[(i * 4) + 3].ShouldBe((byte)255);
        }
    }

    [Fact]
    public void Vergroessern_HaeltKantenImWesentlichenAnDerselbenStelle()
    {
        // Links schwarz, rechts weiß: Die Kante bleibt bei der Hälfte.
        var source = Solid(10, 4, 255, 255, 255);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var o = ((y * 10) + x) * 4;
                source.Pixels[o] = source.Pixels[o + 1] = source.Pixels[o + 2] = 0;
            }
        }

        var scaled = OcrImagePrep.Resize(source, 20, 8);

        scaled.Pixels[((3 * 20) + 2) * 4].ShouldBeLessThan((byte)10);
        scaled.Pixels[((3 * 20) + 17) * 4].ShouldBeGreaterThan((byte)245);
    }

    [Fact]
    public void Verkleinern_MitteltStattNurAbzutasten()
    {
        var source = Solid(8, 8, 255, 255, 255);

        // Schachbrett aus Schwarz und Weiß: nach dem Halbieren etwa Mittelgrau.
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if ((x + y) % 2 == 0)
                {
                    var o = ((y * 8) + x) * 4;
                    source.Pixels[o] = source.Pixels[o + 1] = source.Pixels[o + 2] = 0;
                }
            }
        }

        var scaled = OcrImagePrep.Resize(source, 4, 4);

        scaled.Pixels.Where((_, i) => i % 4 == 0).ShouldAllBe(v => v > 90 && v < 170);
    }

    [Fact]
    public void GleicheGroesse_LiefertEineKopie()
    {
        var source = Solid(4, 4, 1, 2, 3);

        var copy = OcrImagePrep.Resize(source, 4, 4);

        copy.ShouldNotBeSameAs(source);
        copy.Pixels.ShouldBe(source.Pixels);
    }

    [Fact]
    public void Skalierungsregel_MittlereHoeheUnterZehn_LoestZweitenLaufAus()
    {
        OcrImagePrep.ShouldRetryScaled([OcrLines.Line("klein klein", 0, 0, height: 8)]).ShouldBeTrue();
        OcrImagePrep.ShouldRetryScaled([OcrLines.Line("gross genug", 0, 0, height: 10)]).ShouldBeFalse();
        OcrImagePrep.ShouldRetryScaled([OcrLines.Line("gross", 0, 0, height: 24)]).ShouldBeFalse();
        OcrImagePrep.ShouldRetryScaled([]).ShouldBeFalse();
    }

    [Theory]
    [InlineData(6.0, 3)]
    [InlineData(7.4, 3)]
    [InlineData(7.5, 2)]
    [InlineData(9.9, 2)]
    [InlineData(10.0, 0)]
    [InlineData(12.0, 0)]
    public void Skalierungsregel_WaehltDenFaktorNachDerMittlerenHoehe(double height, int expected)
    {
        OcrImagePrep.RetryFactor([OcrLines.Line("wort wort", 0, 0, height)]).ShouldBe(expected);
    }

    [Fact]
    public void Skalierungsregel_OhneWoerterKeinZweiterLauf()
    {
        OcrImagePrep.RetryFactor([]).ShouldBe(0);
        OcrImagePrep.RetryFactor([new OcrLine(string.Empty, new RectD(0, 0, 10, 8), [])]).ShouldBe(0);
    }

    [Fact]
    public void Skalierungsregel_VergroesserterLaufGiltNurMitMehrZeichen()
    {
        var first = new[] { OcrLines.Line("abc", 0, 0) };

        OcrImagePrep.PreferScaled(first, [OcrLines.Line("abcd", 0, 0)]).ShouldBeTrue();
        OcrImagePrep.PreferScaled(first, [OcrLines.Line("abc", 0, 0)]).ShouldBeFalse();
        OcrImagePrep.PreferScaled(first, [OcrLines.Line("ab", 0, 0)]).ShouldBeFalse();
    }

    [Fact]
    public void Unscale_RechnetRechteckeZurueck()
    {
        var line = OcrLines.Line("a b", 20, 40, height: 20);

        var back = OcrImagePrep.Unscale(line, 2, 2);

        back.Bounds.X.ShouldBe(10);
        back.Bounds.Y.ShouldBe(20);
        back.Bounds.Height.ShouldBe(10);
        back.Words[0].Bounds.X.ShouldBe(10);
    }
}

public sealed class OcrSettingsTests
{
    [Fact]
    public void Hotkey_Text_HatStandardUndWirdGesetzt()
    {
        var settings = new AppSettings();

        settings.GetHotkey(CaptureMode.Text).ShouldBe("Ctrl+Shift+D5");
        settings.GetHotkey(CaptureMode.FullScreen).ShouldBe("Ctrl+Shift+D4");
        settings.WithHotkey(CaptureMode.Text, "Alt+T").HotkeyText.ShouldBe("Alt+T");
        settings.WithHotkey(CaptureMode.Text, "Alt+T").HotkeyRectangle.ShouldBe(settings.HotkeyRectangle);
    }

    [Fact]
    public void AlteDateiOhneNeueFelder_BekommtDieStandardwerte()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"HotkeyRectangle\":\"Ctrl+Alt+R\"}")!.Normalized();

        settings.HotkeyText.ShouldBe("Ctrl+Shift+D5");
        settings.OcrLanguage.ShouldBeEmpty();
        settings.HotkeyRectangle.ShouldBe("Ctrl+Alt+R");
    }

    [Fact]
    public void NullWerte_WerdenNormalisiert()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"HotkeyText\":null,\"OcrLanguage\":null}")!.Normalized();

        settings.HotkeyText.ShouldBe("Ctrl+Shift+D5");
        settings.OcrLanguage.ShouldBeEmpty();
    }

    [Fact]
    public void Roundtrip_BehaeltSpracheUndHotkey()
    {
        var settings = new AppSettings { OcrLanguage = "en-US", HotkeyText = "Ctrl+Alt+O" };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;

        restored.OcrLanguage.ShouldBe("en-US");
        restored.HotkeyText.ShouldBe("Ctrl+Alt+O");
    }

    [Fact]
    public void Aufnahmemodus_TextStehtAmEnde()
    {
        Enum.GetValues<CaptureMode>().Last().ShouldBe(CaptureMode.Scrolling);
        ((int)CaptureMode.Text).ShouldBe(4);
        ((int)CaptureMode.Rectangle).ShouldBe(0);
        ((int)CaptureMode.Window).ShouldBe(1);
        ((int)CaptureMode.FullScreen).ShouldBe(2);
        ((int)CaptureMode.Freeform).ShouldBe(3);
    }

    [Fact]
    public void Ergebnistypen_TextIstLeerNurOhneSichtbareZeichen()
    {
        new OcrResult("de-DE", []).IsEmpty.ShouldBeTrue();
        new OcrResult("de-DE", [OcrLines.Line("  ", 0, 0)]).IsEmpty.ShouldBeTrue();
        new OcrResult("de-DE", [OcrLines.Line("a", 0, 0)]).IsEmpty.ShouldBeFalse();
    }
}
