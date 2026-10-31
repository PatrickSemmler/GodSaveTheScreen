#pragma warning disable CA1861 // Testdaten: Offsets stehen bewusst direkt im Test
using System.Diagnostics;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Tests;

/// <summary>Erzeugte „Seite“ aus Bändern (einfarbig oder mit Farbläufen) und daraus geschnittene Bildschirmbilder.</summary>
internal sealed class ScrollScene
{
    private readonly Random _random;

    public ScrollScene(int width, int frameHeight, int pageRows, int seed, double uniformShare = 0.2)
    {
        Width = width;
        FrameHeight = frameHeight;
        _random = new Random(seed);
        Page = BuildPage(width, pageRows, uniformShare);
    }

    public int Width { get; }

    public int FrameHeight { get; }

    public PixelBuffer Page { get; private set; }

    public PixelBuffer? Header { get; private set; }

    public PixelBuffer? Footer { get; private set; }

    public int SidebarWidth { get; set; }

    public int ScrollbarWidth { get; set; }

    public int Noise { get; set; }

    /// <summary>Höhe des Fensters, in dem die Seite scrollt.</summary>
    public int ViewRows => FrameHeight - (Header?.Height ?? 0) - (Footer?.Height ?? 0);

    public void SetHeader(int rows) => Header = BuildPage(Width, rows, 0);

    public void SetFooter(int rows) => Footer = BuildPage(Width, rows, 0);

    public void Replace(int firstRow, PixelBuffer rows)
    {
        for (var y = 0; y < rows.Height; y++)
        {
            Buffer.BlockCopy(rows.Pixels, y * rows.Stride, Page.Pixels, (firstRow + y) * Page.Stride, rows.Stride);
        }
    }

    public PixelBuffer BuildBands(int rows, bool repeating = false)
    {
        if (!repeating)
        {
            return BuildPage(Width, rows, 0.2);
        }

        const int period = 16;
        var unit = BuildPage(Width, period, 0);
        var result = new PixelBuffer(Width, rows);
        for (var y = 0; y < rows; y++)
        {
            Buffer.BlockCopy(unit.Pixels, (y % period) * unit.Stride, result.Pixels, y * result.Stride, unit.Stride);
        }

        return result;
    }

    public PixelBuffer Frame(int offset, int index = 0)
    {
        var frame = new PixelBuffer(Width, FrameHeight);
        var header = Header?.Height ?? 0;
        var footerStart = FrameHeight - (Footer?.Height ?? 0);
        for (var y = 0; y < FrameHeight; y++)
        {
            PixelBuffer source;
            int row;
            if (y < header)
            {
                source = Header!;
                row = y;
            }
            else if (y >= footerStart)
            {
                source = Footer!;
                row = y - footerStart;
            }
            else
            {
                source = Page;
                row = offset + y - header;
            }

            Buffer.BlockCopy(source.Pixels, row * source.Stride, frame.Pixels, y * frame.Stride, frame.Stride);
        }

        DrawSidebar(frame);
        DrawScrollbar(frame, offset);
        if (Noise > 0)
        {
            AddNoise(frame, index);
        }

        return frame;
    }

    /// <summary>Erwartetes Ergebnis ohne Seitenleiste und Bildlaufleiste: Kopf + Seite[first..last+View) + Fuß.</summary>
    public PixelBuffer Expected(int firstOffset, int lastOffset)
    {
        var header = Header?.Height ?? 0;
        var footer = Footer?.Height ?? 0;
        var content = lastOffset + ViewRows - firstOffset;
        var width = Width - ScrollbarWidth;
        var result = new PixelBuffer(width, header + content + footer);
        var y = 0;
        void Copy(PixelBuffer source, int first, int count)
        {
            for (var i = 0; i < count; i++, y++)
            {
                Buffer.BlockCopy(source.Pixels, (first + i) * source.Stride, result.Pixels, y * result.Stride, result.Stride);
            }
        }

        if (header > 0)
        {
            Copy(Header!, 0, header);
        }

        Copy(Page, firstOffset, content);
        if (footer > 0)
        {
            Copy(Footer!, 0, footer);
        }

        return result;
    }

    public static int MaxDifference(PixelBuffer a, PixelBuffer b, int firstColumn = 0)
    {
        var max = 0;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = firstColumn; x < a.Width; x++)
            {
                var i = (y * a.Stride) + (x * 4);
                for (var k = 0; k < 3; k++)
                {
                    max = Math.Max(max, Math.Abs(a.Pixels[i + k] - b.Pixels[i + k]));
                }
            }
        }

        return max;
    }

    private PixelBuffer BuildPage(int width, int rows, double uniformShare)
    {
        var page = new PixelBuffer(width, rows);
        var y = 0;
        var row = new byte[width * 4];
        while (y < rows)
        {
            var bandHeight = _random.Next(1, 7);
            if (_random.NextDouble() < uniformShare)
            {
                var colour = _random.Next(0x808080, 0xFFFFFF);
                for (var x = 0; x < width; x++)
                {
                    Put(row, x, colour);
                }
            }
            else
            {
                var x = 0;
                while (x < width)
                {
                    var run = _random.Next(2, 40);
                    var colour = _random.Next(0, 0xFFFFFF);
                    for (var i = 0; i < run && x < width; i++, x++)
                    {
                        Put(row, x, colour);
                    }
                }
            }

            for (var i = 0; i < bandHeight && y < rows; i++, y++)
            {
                Buffer.BlockCopy(row, 0, page.Pixels, y * page.Stride, row.Length);
            }
        }

        return page;
    }

    private static void Put(byte[] row, int x, int rgb)
    {
        var i = x * 4;
        row[i] = (byte)(rgb & 0xFF);
        row[i + 1] = (byte)((rgb >> 8) & 0xFF);
        row[i + 2] = (byte)((rgb >> 16) & 0xFF);
        row[i + 3] = 255;
    }

    private void DrawSidebar(PixelBuffer frame)
    {
        for (var y = 0; y < FrameHeight; y++)
        {
            for (var x = 0; x < SidebarWidth; x++)
            {
                var value = (byte)(((y * 7) + (x * 13)) % 200);
                var i = (y * frame.Stride) + (x * 4);
                frame.Pixels[i] = value;
                frame.Pixels[i + 1] = (byte)(255 - value);
                frame.Pixels[i + 2] = 40;
            }
        }
    }

    private void DrawScrollbar(PixelBuffer frame, int offset)
    {
        if (ScrollbarWidth == 0)
        {
            return;
        }

        var header = Header?.Height ?? 0;
        var thumbTop = header + (int)((long)offset * ViewRows / Page.Height);
        for (var y = 0; y < FrameHeight; y++)
        {
            var thumb = y >= thumbTop && y < thumbTop + 60;
            for (var x = Width - ScrollbarWidth; x < Width; x++)
            {
                var i = (y * frame.Stride) + (x * 4);
                var value = (byte)(thumb ? 90 : 235);
                frame.Pixels[i] = value;
                frame.Pixels[i + 1] = value;
                frame.Pixels[i + 2] = value;
            }
        }
    }

    private void AddNoise(PixelBuffer frame, int index)
    {
        var random = new Random(1000 + index);
        for (var i = 0; i < frame.Pixels.Length; i++)
        {
            if ((i & 3) == 3)
            {
                continue;
            }

            frame.Pixels[i] = (byte)Math.Clamp(frame.Pixels[i] + random.Next(-Noise, Noise + 1), 0, 255);
        }
    }
}

public sealed class ScrollStitcherTests
{
    private static ScrollStitcher Stitcher(int maxHeight = 16384, int guard = 32) =>
        new(maxHeight, new ScrollStitcherOptions { ScrollbarGuard = guard });

    [Fact]
    public void AlleVersaetze_VonEinsBisBildhoeheMinusZwoelf_WerdenExaktZusammengesetzt()
    {
        var scene = new ScrollScene(120, 160, 600, seed: 1, uniformShare: 0);
        for (var dy = 1; dy <= scene.FrameHeight - 12; dy++)
        {
            var stitcher = Stitcher();
            stitcher.Add(scene.Frame(0));
            var step = stitcher.Add(scene.Frame(dy));
            step.Outcome.ShouldBe(StitchOutcome.Moved, $"dy={dy}");
            step.NewRows.ShouldBe(dy);
            ScrollScene.MaxDifference(stitcher.Build(), scene.Expected(0, dy)).ShouldBe(0, $"dy={dy}");
        }
    }

    [Fact]
    public void MehrereBilder_ErgebenDieGanzeSeite()
    {
        var scene = new ScrollScene(200, 240, 1500, seed: 2);
        var stitcher = Stitcher();
        int[] offsets = [0, 150, 310, 450, 600, 760, 900, 1050];
        foreach (var offset in offsets)
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Height.ShouldBe(1050 + 240);
        ScrollScene.MaxDifference(result, scene.Expected(0, 1050)).ShouldBe(0);
    }

    [Fact]
    public void KopfUndFusszeile_ErscheinenGenauEinmal()
    {
        var scene = new ScrollScene(200, 300, 2000, seed: 3);
        scene.SetHeader(40);
        scene.SetFooter(30);
        var stitcher = Stitcher();
        int[] offsets = [0, 120, 250, 380, 500, 640];
        foreach (var offset in offsets)
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Height.ShouldBe(40 + 640 + scene.ViewRows + 30);
        ScrollScene.MaxDifference(result, scene.Expected(0, 640)).ShouldBe(0);
        stitcher.HeaderRows.ShouldBeGreaterThanOrEqualTo(40);
        stitcher.FooterRows.ShouldBeGreaterThanOrEqualTo(30);
        stitcher.FixedRegionsUncertain.ShouldBeFalse();
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(0, 50)]
    public void NurKopfOderNurFusszeile(int header, int footer)
    {
        var scene = new ScrollScene(200, 300, 2000, seed: 4);
        if (header > 0)
        {
            scene.SetHeader(header);
        }

        if (footer > 0)
        {
            scene.SetFooter(footer);
        }

        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 130, 260, 400 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        ScrollScene.MaxDifference(stitcher.Build(), scene.Expected(0, 400)).ShouldBe(0);
    }

    [Fact]
    public void Seitenleiste_StoertDenVergleichNicht()
    {
        var scene = new ScrollScene(240, 300, 1800, seed: 5) { SidebarWidth = 50 };
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 140, 280, 430, 560 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Height.ShouldBe(560 + 300);
        ScrollScene.MaxDifference(result, scene.Expected(0, 560), firstColumn: 50).ShouldBe(0);
    }

    [Fact]
    public void Bildlaufleiste_WirdAbgeschnitten()
    {
        var scene = new ScrollScene(240, 300, 2000, seed: 6) { ScrollbarWidth = 14 };
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 140, 280, 430, 560 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Width.ShouldBe(240 - 14);
        result.Height.ShouldBe(560 + 300);
        ScrollScene.MaxDifference(result, scene.Expected(0, 560)).ShouldBe(0);
    }

    [Fact]
    public void OhneBildlaufleiste_BleibtDieBreiteErhalten()
    {
        var scene = new ScrollScene(240, 300, 2000, seed: 7);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(scene.Frame(200));
        stitcher.Add(scene.Frame(400));
        stitcher.Build().Width.ShouldBe(240);
    }

    [Fact]
    public void EinfarbigerAbschnitt_ImUeberlappungsbereich()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 8);
        var blank = new PixelBuffer(200, 120);
        Array.Fill(blank.Pixels, (byte)250);
        scene.Replace(350, blank);
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 150, 300, 450, 600 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        ScrollScene.MaxDifference(stitcher.Build(), scene.Expected(0, 600)).ShouldBe(0);
    }

    [Fact]
    public void SichWiederholendeZeilen_NimmtDenBisherigenSchritt()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 9);
        scene.Replace(250, scene.BuildBands(900, repeating: true));
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 100, 200, 300, 400, 500, 600 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Height.ShouldBe(600 + 300);
        ScrollScene.MaxDifference(result, scene.Expected(0, 600)).ShouldBe(0);
    }

    [Fact]
    public void Rauschen_VierJeKanal_WirdToleriert()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 10) { Noise = 4 };
        var stitcher = Stitcher();
        var index = 0;
        foreach (var offset in new[] { 0, 140, 280, 430 })
        {
            stitcher.Add(scene.Frame(offset, index++)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var result = stitcher.Build();
        result.Height.ShouldBe(430 + 300);
        ScrollScene.MaxDifference(result, scene.Expected(0, 430)).ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void KleineAnimierteFlaeche_StoertNicht()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 11);
        var random = new Random(5);
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 140, 280, 430 })
        {
            var frame = scene.Frame(offset);
            for (var y = 100; y < 115; y++)
            {
                for (var x = 60; x < 90; x++)
                {
                    var i = (y * frame.Stride) + (x * 4);
                    frame.Pixels[i] = (byte)random.Next(256);
                    frame.Pixels[i + 1] = (byte)random.Next(256);
                    frame.Pixels[i + 2] = (byte)random.Next(256);
                }
            }

            stitcher.Add(frame).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        stitcher.Build().Height.ShouldBe(430 + 300);
    }

    [Fact]
    public void KeineUeberlappung_LaesstDenZustandUnveraendert()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 12);
        var other = new ScrollScene(200, 300, 1500, seed: 99);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(other.Frame(0)).Outcome.ShouldBe(StitchOutcome.NoOverlap);
        stitcher.Add(scene.Frame(120)).Outcome.ShouldBe(StitchOutcome.Moved);
        ScrollScene.MaxDifference(stitcher.Build(), scene.Expected(0, 120)).ShouldBe(0);
    }

    [Fact]
    public void ZuGrosserSchritt_IstKeineUeberlappung()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 13);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(scene.Frame(400)).Outcome.ShouldBe(StitchOutcome.NoOverlap);
        stitcher.Add(scene.Frame(200)).Outcome.ShouldBe(StitchOutcome.Moved);
    }

    [Fact]
    public void GleichesBild_IstKeineBewegung()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 14);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(scene.Frame(0)).Outcome.ShouldBe(StitchOutcome.NoMovement);
        stitcher.Add(scene.Frame(200)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(200)).Outcome.ShouldBe(StitchOutcome.NoMovement);
        stitcher.Add(scene.Frame(200)).Outcome.ShouldBe(StitchOutcome.NoMovement);
        stitcher.Build().Height.ShouldBe(200 + 300);
    }

    [Fact]
    public void SeitenEnde_MitFesterKopfzeile_MeldetKeineBewegung()
    {
        var scene = new ScrollScene(200, 300, 700, seed: 15);
        scene.SetHeader(40);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(scene.Frame(200)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(440)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(440)).Outcome.ShouldBe(StitchOutcome.NoMovement);
    }

    [Fact]
    public void Limit_StopptMitVollemErgebnis()
    {
        var scene = new ScrollScene(200, 300, 2000, seed: 16);
        var stitcher = Stitcher(maxHeight: 700);
        stitcher.Add(scene.Frame(0)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(150)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(300)).Outcome.ShouldBe(StitchOutcome.Moved);
        var step = stitcher.Add(scene.Frame(450));
        step.Outcome.ShouldBe(StitchOutcome.LimitReached);
        stitcher.LimitHit.ShouldBeTrue();
        stitcher.Add(scene.Frame(600)).Outcome.ShouldBe(StitchOutcome.LimitReached);

        var result = stitcher.Build();
        result.Height.ShouldBe(700);
        var expected = scene.Expected(0, 1000).Crop(new GodSaveTheScreen.Core.Geometry.PixelRect(0, 0, 200, 700));
        ScrollScene.MaxDifference(result, expected).ShouldBe(0);
    }

    [Fact]
    public void ErstesBildGroesserAlsLimit_WirdBeschnitten()
    {
        var scene = new ScrollScene(100, 300, 600, seed: 17);
        var stitcher = Stitcher(maxHeight: 200);
        stitcher.Add(scene.Frame(0)).Outcome.ShouldBe(StitchOutcome.LimitReached);
        stitcher.Build().Height.ShouldBe(200);
    }

    [Fact]
    public void Zurueckscrollen_UndWeiter_SetztFort()
    {
        var scene = new ScrollScene(200, 300, 2000, seed: 18);
        var stitcher = Stitcher();
        foreach (var offset in new[] { 0, 100, 200, 300 })
        {
            stitcher.Add(scene.Frame(offset)).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        var back = stitcher.Add(scene.Frame(150));
        back.Outcome.ShouldBe(StitchOutcome.Moved);
        back.NewRows.ShouldBe(0);
        back.Dy.ShouldBeLessThan(0);
        stitcher.Add(scene.Frame(250)).NewRows.ShouldBe(0);
        stitcher.Add(scene.Frame(400)).Outcome.ShouldBe(StitchOutcome.Moved);

        var result = stitcher.Build();
        result.Height.ShouldBe(400 + 300);
        ScrollScene.MaxDifference(result, scene.Expected(0, 400)).ShouldBe(0);
    }

    [Fact]
    public void Bruchteilsscrollen_MitWeichgezeichnetenZeilen_WirdErkannt()
    {
        // Bild um eine Achtelzeile versetzt: jede Zeile mischt 7/8 der Seitenzeile mit 1/8 der folgenden.
        var scene = new ScrollScene(200, 300, 1500, seed: 19);
        PixelBuffer Blend(int offset)
        {
            var a = scene.Frame(offset);
            var b = scene.Frame(offset + 1);
            for (var i = 0; i < a.Pixels.Length; i++)
            {
                a.Pixels[i] = (byte)(((a.Pixels[i] * 7) + b.Pixels[i]) / 8);
            }

            return a;
        }

        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(Blend(140)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Add(scene.Frame(281)).Outcome.ShouldBe(StitchOutcome.Moved);
        stitcher.Build().Height.ShouldBeInRange(281 + 300 - 2, 281 + 300 + 2);
    }

    [Fact]
    public void Vorschau_HatDieErwarteteGroesse_UndZeigtDenAnfang()
    {
        var scene = new ScrollScene(200, 300, 1500, seed: 22);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        stitcher.Add(scene.Frame(200));
        stitcher.Add(scene.Frame(400));

        var preview = stitcher.BuildPreview(50, 100);

        preview.Width.ShouldBeLessThanOrEqualTo(50);
        preview.Height.ShouldBeLessThanOrEqualTo(100);
        preview.Height.ShouldBeGreaterThan(0);
        // Erste Zeile der Vorschau = erste Zeile des Ergebnisses.
        preview.Pixels.AsSpan(0, 4).SequenceEqual(scene.Page.Pixels.AsSpan(0, 4)).ShouldBeTrue();
    }

    [Fact]
    public void BildVergleich_ErkenntGleicheUndAbweichendeBilder()
    {
        var scene = new ScrollScene(100, 100, 400, seed: 23);
        FrameCompare.NearlyEqual(scene.Frame(0), scene.Frame(0)).ShouldBeTrue();
        FrameCompare.NearlyEqual(scene.Frame(0), scene.Frame(40)).ShouldBeFalse();
        FrameCompare.NearlyEqual(scene.Frame(0), new PixelBuffer(100, 90)).ShouldBeFalse();
    }

    [Fact]
    public void Bildgroesse_MussGleichBleiben()
    {
        var stitcher = Stitcher();
        stitcher.Add(new PixelBuffer(100, 100));
        Should.Throw<ArgumentException>(() => stitcher.Add(new PixelBuffer(100, 90)));
    }

    [Fact]
    public void BuildOhneBild_WirftFehler()
    {
        Should.Throw<InvalidOperationException>(() => Stitcher().Build());
    }

    [Fact]
    public void NurEinBild_ErgibtDasBild()
    {
        var scene = new ScrollScene(100, 100, 300, seed: 20);
        var stitcher = Stitcher();
        stitcher.Add(scene.Frame(0));
        var result = stitcher.Build();
        result.Height.ShouldBe(100);
        stitcher.ContentHeight.ShouldBe(100);
    }

    [Fact]
    public void Rechenzeit_ProBild_BeiFullHd_BleibtKlein()
    {
        var scene = new ScrollScene(1920, 1080, 6000, seed: 21) { ScrollbarWidth = 17 };
        scene.SetHeader(90);
        var stitcher = Stitcher(guard: 32);
        var frames = new[] { 0, 600, 1200, 1800, 2400, 3000 }.Select(o => scene.Frame(o)).ToList();
        stitcher.Add(frames[0]);
        stitcher.Add(frames[1]);

        var watch = Stopwatch.StartNew();
        for (var i = 2; i < frames.Count; i++)
        {
            stitcher.Add(frames[i]).Outcome.ShouldBe(StitchOutcome.Moved);
        }

        watch.Stop();
        var perFrame = watch.Elapsed.TotalMilliseconds / (frames.Count - 2);
        perFrame.ShouldBeLessThan(250, $"{perFrame:F0} ms je Bild");
    }
}
