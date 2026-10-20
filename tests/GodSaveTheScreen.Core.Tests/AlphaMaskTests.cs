using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Tests;

/// <summary>Transparenzmaske, Bereichsauswahl und Pinsel (Entfernen von Bildbereichen).</summary>
public sealed class AlphaMaskTests
{
    private static PixelBuffer Opaque(int width, int height, byte r = 30, byte g = 20, byte b = 10)
    {
        var buffer = new PixelBuffer(width, height);
        for (var i = 0; i < buffer.Pixels.Length; i += PixelBuffer.BytesPerPixel)
        {
            buffer.Pixels[i] = b;
            buffer.Pixels[i + 1] = g;
            buffer.Pixels[i + 2] = r;
            buffer.Pixels[i + 3] = 255;
        }

        return buffer;
    }

    private static void Paint(PixelBuffer image, PixelRect rect, byte r, byte g, byte b)
    {
        for (var y = rect.Y; y < rect.Bottom; y++)
        {
            for (var x = rect.X; x < rect.Right; x++)
            {
                var i = (y * image.Stride) + (x * PixelBuffer.BytesPerPixel);
                image.Pixels[i] = b;
                image.Pixels[i + 1] = g;
                image.Pixels[i + 2] = r;
            }
        }
    }

    private static byte Alpha(PixelBuffer b, int x, int y) => b.Pixels[(((y * b.Width) + x) * PixelBuffer.BytesPerPixel) + 3];

    private static byte At(AlphaMask mask, int x, int y) => mask.Values[(y * mask.Width) + x];

    private static byte At(AreaSelection selection, int x, int y) => selection.Values[(y * selection.Width) + x];

    // ---- Maske ----

    [Fact]
    public void Neu_AllesSichtbar()
    {
        var mask = new AlphaMask(4, 3);

        mask.IsOpaque.ShouldBeTrue();
        mask.Values.ShouldAllBe(v => v == 255);
    }

    [Fact]
    public void Write_Bereich_AendertNurDiesenUndMeldetIhn()
    {
        var mask = new AlphaMask(10, 10);
        PixelRect? reported = null;
        mask.Changed += (_, r) => reported = r;
        var region = new PixelRect(2, 3, 4, 2);

        mask.Write(region, new byte[8]);

        reported.ShouldBe(region);
        mask.Version.ShouldBe(1);
        mask.IsOpaque.ShouldBeFalse();
        At(mask, 2, 3).ShouldBe((byte)0);
        At(mask, 5, 4).ShouldBe((byte)0);
        At(mask, 6, 4).ShouldBe((byte)255);
        At(mask, 2, 5).ShouldBe((byte)255);
        mask.Read(region).ShouldAllBe(v => v == 0);
    }

    [Fact]
    public void Write_AusserhalbDerMaske_Wirft()
    {
        var mask = new AlphaMask(10, 10);

        Should.Throw<ArgumentException>(() => mask.Write(new PixelRect(8, 8, 4, 4), new byte[16]));
    }

    [Fact]
    public void ApplyTo_VerrechnetAlphaUndLeertEntferntePixel()
    {
        var image = Opaque(4, 1);
        var mask = new AlphaMask(4, 1);
        mask.Write(new PixelRect(0, 0, 4, 1), [255, 128, 0, 255]);

        var result = mask.ApplyTo(image);

        Alpha(result, 0, 0).ShouldBe((byte)255);
        Alpha(result, 1, 0).ShouldBe((byte)128);
        result.Pixels.AsSpan(8, 4).ToArray().ShouldBe(new byte[4]);
        result.Pixels[4].ShouldBe((byte)10, "Farbe teiltransparenter Pixel bleibt erhalten");
        Alpha(image, 1, 0).ShouldBe((byte)255, "das Original bleibt unverändert");
    }

    [Fact]
    public void VisibleBounds_UmschliesstNichtTransparentePixel()
    {
        var image = Opaque(10, 8);
        var mask = new AlphaMask(10, 8);
        var values = new byte[80];
        values[(2 * 10) + 3] = 255;
        values[(5 * 10) + 6] = 10;
        mask.Write(mask.Bounds, values);

        AlphaMask.VisibleBounds(mask.ApplyTo(image)).ShouldBe(PixelRect.FromLtrb(3, 2, 7, 6));
        AlphaMask.VisibleBounds(new PixelBuffer(3, 3)).ShouldBeNull();
    }

    [Fact]
    public void ImageDocument_MaskedImage_OhneMaskeIstBasisbild_SonstNeuerPufferJeStand()
    {
        var document = new ImageDocument(Opaque(6, 6));

        document.MaskedImage.ShouldBeSameAs(document.BaseImage);

        document.Mask.Write(new PixelRect(0, 0, 2, 2), new byte[4]);
        var first = document.MaskedImage;
        first.ShouldNotBeSameAs(document.BaseImage);
        Alpha(first, 1, 1).ShouldBe((byte)0);
        document.MaskedImage.ShouldBeSameAs(first, "unveränderte Maske → gleicher Puffer");

        document.Mask.Write(new PixelRect(0, 0, 2, 2), [255, 255, 255, 255]);
        document.MaskedImage.ShouldBeSameAs(document.BaseImage);
    }

    [Fact]
    public void MaskEditAction_UndoRedo_StelltBereichWiederHer()
    {
        var document = new ImageDocument(Opaque(5, 5));
        var region = new PixelRect(1, 1, 2, 2);

        document.Undo.Execute(new MaskEditAction(document.Mask, region, new byte[4], "Bereich entfernen"));
        document.Mask.Read(region).ShouldAllBe(v => v == 0);
        document.IsDirty.ShouldBeTrue();

        document.Undo.Undo();
        document.Mask.IsOpaque.ShouldBeTrue();

        document.Undo.Redo();
        document.Mask.Read(region).ShouldAllBe(v => v == 0);
    }

    // ---- Auswahl ----

    [Fact]
    public void Rectangle_RundetAufPixelUndKlemmt()
    {
        var (box, coverage) = AreaSelection.Rectangle(new RectD(8.4, -3, -5, 6.6), new PixelRect(0, 0, 10, 10));

        box.ShouldBe(PixelRect.FromLtrb(3, 0, 8, 4));
        coverage.ShouldAllBe(v => v == 255);
    }

    [Fact]
    public void Ellipse_MitteVollRandGeglaettetEckenLeer()
    {
        var (box, coverage) = AreaSelection.Ellipse(new RectD(0, 0, 40, 20), new PixelRect(0, 0, 50, 50));

        box.ShouldBe(new PixelRect(0, 0, 40, 20));
        coverage[(10 * 40) + 20].ShouldBe((byte)255);
        coverage[0].ShouldBe((byte)0);
        coverage.ShouldContain(v => v > 0 && v < 255);
    }

    [Fact]
    public void Combine_ErsetzenHinzufuegenAbziehen()
    {
        var selection = new AreaSelection(20, 20);
        var clip = selection.Area;
        var (a, ca) = AreaSelection.Rectangle(new RectD(0, 0, 10, 10), clip);
        var (b, cb) = AreaSelection.Rectangle(new RectD(5, 5, 10, 10), clip);
        var (c, cc) = AreaSelection.Rectangle(new RectD(0, 0, 6, 20), clip);

        selection.Combine(a, ca, SelectionCombine.Replace);
        selection.Combine(b, cb, SelectionCombine.Add);
        selection.Bounds.ShouldBe(new PixelRect(0, 0, 15, 15));

        selection.Combine(c, cc, SelectionCombine.Subtract);
        At(selection, 2, 2).ShouldBe((byte)0);
        At(selection, 7, 2).ShouldBe((byte)255);
        At(selection, 12, 12).ShouldBe((byte)255);
        selection.Bounds.ShouldBe(PixelRect.FromLtrb(6, 0, 15, 15));

        selection.Combine(b, cb, SelectionCombine.Replace);
        selection.Bounds.ShouldBe(b);
    }

    [Fact]
    public void Invert_UndSelectAll()
    {
        var selection = new AreaSelection(10, 10);
        selection.IsEmpty.ShouldBeTrue();

        selection.Invert();
        selection.Bounds.ShouldBe(selection.Area);

        var (box, coverage) = AreaSelection.Rectangle(new RectD(0, 0, 10, 5), selection.Area);
        selection.Combine(box, coverage, SelectionCombine.Subtract);
        selection.Invert();
        selection.Bounds.ShouldBe(new PixelRect(0, 0, 10, 5));

        selection.Clear();
        selection.IsEmpty.ShouldBeTrue();
        selection.SelectAll();
        selection.Values.ShouldAllBe(v => v == 255);
    }

    [Fact]
    public void Feathered_OhneKanteUnveraendert_MitKanteWeicherUebergang()
    {
        var selection = new AreaSelection(60, 60);
        var (box, coverage) = AreaSelection.Rectangle(new RectD(20, 20, 20, 20), selection.Area);
        selection.Combine(box, coverage, SelectionCombine.Replace);

        var (hardBox, hard) = selection.Feathered(0);
        hardBox.ShouldBe(box);
        hard.ShouldAllBe(v => v == 255);

        var (softBox, soft) = selection.Feathered(9);
        softBox.Width.ShouldBeGreaterThan(box.Width);
        byte Soft(int x, int y) => soft[((y - softBox.Y) * softBox.Width) + (x - softBox.X)];
        Soft(30, 30).ShouldBe((byte)255);
        Soft(20, 30).ShouldBeInRange((byte)80, (byte)180);
        Soft(softBox.X, 30).ShouldBeLessThan((byte)10);
    }

    [Fact]
    public void Feathered_GanzesBild_RandBleibtVoll()
    {
        var selection = new AreaSelection(30, 30);
        selection.SelectAll();

        var (_, soft) = selection.Feathered(12);

        soft.ShouldAllBe(v => v == 255);
    }

    [Fact]
    public void MagicWand_FuelltZusammenhaengendeAehnlicheFlaeche()
    {
        var image = Opaque(20, 10, 255, 255, 255);
        Paint(image, new PixelRect(8, 0, 2, 10), 0, 0, 0);    // Trennlinie
        Paint(image, new PixelRect(2, 2, 2, 2), 250, 250, 250); // leicht abweichend

        var (box, coverage) = AreaSelection.MagicWand(image, new PointD(1, 1), 5);

        box.ShouldBe(new PixelRect(0, 0, 8, 10), "die Linie trennt die rechte Hälfte ab");
        coverage.ShouldAllBe(v => v == 255, "250 liegt innerhalb von 5 % Toleranz");

        var (strictBox, strict) = AreaSelection.MagicWand(image, new PointD(1, 1), 0);
        strictBox.ShouldBe(new PixelRect(0, 0, 8, 10));
        strict[(2 * 8) + 2].ShouldBe((byte)0);
    }

    [Fact]
    public void MagicWand_AusserhalbDesBilds_Leer() =>
        AreaSelection.MagicWand(Opaque(5, 5), new PointD(-1, 2), 10).Box.IsEmpty.ShouldBeTrue();

    [Fact]
    public void Remove_MachtAuswahlTransparent_TeildeckungAnteilig()
    {
        var mask = new AlphaMask(10, 10);
        var selection = new AreaSelection(10, 10);
        selection.Combine(new PixelRect(0, 0, 2, 1), [255, 128], SelectionCombine.Replace);

        var edit = MaskEditing.Remove(mask, selection, 0).ShouldNotBeNull();

        edit.Region.ShouldBe(new PixelRect(0, 0, 2, 1));
        edit.Values[0].ShouldBe((byte)0);
        edit.Values[1].ShouldBe((byte)127);
        mask.IsOpaque.ShouldBeTrue("Remove ändert die Maske nicht selbst");
    }

    [Fact]
    public void Remove_LeereAuswahlOderSchonEntfernt_Null()
    {
        var mask = new AlphaMask(5, 5);
        var selection = new AreaSelection(5, 5);
        MaskEditing.Remove(mask, selection, 0).ShouldBeNull();

        selection.SelectAll();
        mask.Write(mask.Bounds, new byte[25]);
        MaskEditing.Remove(mask, selection, 0).ShouldBeNull();
    }

    // ---- Pinsel ----

    [Fact]
    public void Brush_Hart_EntferntKreisMitGlattemRand()
    {
        var mask = new AlphaMask(40, 40);
        var brush = new MaskBrush(mask, 20, 1, restore: false);

        brush.LineTo(new PointD(20, 20));

        At(mask, 20, 20).ShouldBe((byte)0);
        At(mask, 14, 20).ShouldBe((byte)0);
        At(mask, 20, 32).ShouldBe((byte)255);
        At(mask, 0, 0).ShouldBe((byte)255);
        brush.Dirty.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Brush_Weich_RandNurTeilweise()
    {
        var mask = new AlphaMask(40, 40);
        var brush = new MaskBrush(mask, 20, 0, restore: false);

        brush.LineTo(new PointD(20, 20));

        At(mask, 20, 20).ShouldBeLessThan((byte)5);
        At(mask, 27, 20).ShouldBeInRange((byte)100, (byte)254);
    }

    [Fact]
    public void Brush_UeberlappendeTupfer_AddierenSichNichtImStrich()
    {
        var mask = new AlphaMask(60, 20);
        var brush = new MaskBrush(mask, 16, 0, restore: false);

        brush.LineTo(new PointD(10, 10));
        var single = At(mask, 14, 10);
        brush.LineTo(new PointD(10, 10));
        brush.LineTo(new PointD(10, 10));

        // Weitere Tupfer an derselben Stelle ändern die teilweise Deckung nicht.
        single.ShouldBeInRange((byte)1, (byte)254);
        At(mask, 14, 10).ShouldBe(single);
    }

    [Fact]
    public void Brush_Linie_SchliesstLueckenZwischenPunkten()
    {
        var mask = new AlphaMask(100, 20);
        var brush = new MaskBrush(mask, 6, 1, restore: false);

        brush.LineTo(new PointD(5, 10));
        brush.LineTo(new PointD(95, 10));

        for (var x = 5; x < 95; x++)
        {
            At(mask, x, 10).ShouldBe((byte)0, $"x = {x}");
        }
    }

    [Fact]
    public void Brush_Wiederherstellen_HebtEntfernungAuf()
    {
        var mask = new AlphaMask(30, 30);
        mask.Write(mask.Bounds, new byte[900]);
        var brush = new MaskBrush(mask, 10, 1, restore: true);

        brush.LineTo(new PointD(15, 15));

        At(mask, 15, 15).ShouldBe((byte)255);
        At(mask, 0, 0).ShouldBe((byte)0);
    }

    [Fact]
    public void Brush_End_LiefertVorherNachher_CancelStelltZurueck()
    {
        var mask = new AlphaMask(30, 30);
        var brush = new MaskBrush(mask, 10, 1, restore: false);
        brush.LineTo(new PointD(15, 15));

        var edit = brush.End().ShouldNotBeNull();
        edit.Before.ShouldAllBe(v => v == 255);
        edit.After.ShouldContain((byte)0);
        mask.Read(edit.Region).ShouldBe(edit.After);

        var second = new MaskBrush(mask, 10, 1, restore: false);
        second.LineTo(new PointD(3, 3));
        second.Cancel();
        At(mask, 3, 3).ShouldBe((byte)255);
        At(mask, 15, 15).ShouldBe((byte)0);
    }

    [Fact]
    public void Brush_AusserhalbDesBilds_KeineAenderung()
    {
        var mask = new AlphaMask(10, 10);
        var brush = new MaskBrush(mask, 4, 1, restore: false);

        brush.LineTo(new PointD(-50, -50));

        brush.End().ShouldBeNull();
        mask.IsOpaque.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 10, 1, 1)]
    [InlineData(10, 10, 1, 0)]
    [InlineData(4, 10, 0.5, 1)]
    public void Falloff_Grenzwerte(double distance, double radius, double hardness, double expected) =>
        MaskBrush.Falloff(distance, radius, hardness).ShouldBe(expected, 1e-9);

    // ---- Projekt ----

    [Fact]
    public void Projekt_Maske_Roundtrip()
    {
        var mask = new byte[640 * 480];
        Array.Fill(mask, (byte)255);
        mask[0] = 0;
        mask[^1] = 77;

        var loaded = ProjectSamples.Load(ProjectSamples.Save(ProjectSamples.Content() with { Mask = mask }));

        loaded.Mask.ShouldBe(mask);
        loaded.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Projekt_OhneMaske_Null() =>
        ProjectSamples.Load(ProjectSamples.Save(ProjectSamples.Content())).Mask.ShouldBeNull();

    [Fact]
    public void Projekt_MaskeFalscherGroesse_WirdMitWarnungIgnoriert()
    {
        var loaded = ProjectSamples.Load(ProjectSamples.Save(ProjectSamples.Content() with { Mask = new byte[100] }));

        loaded.Mask.ShouldBeNull();
        loaded.Warnings.ShouldContain(w => w.Contains("Transparenzmaske", StringComparison.Ordinal));
    }
}
