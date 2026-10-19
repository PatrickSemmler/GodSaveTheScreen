using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Tests;

public sealed class CropActionTests
{
    private static ImageDocument NewDocument() => new(new PixelBuffer(200, 100));

    [Fact]
    public void Execute_Undo_Redo_StelltZuschnittWiederHer()
    {
        var document = NewDocument();
        document.EffectiveBounds.ShouldBe(new PixelRect(0, 0, 200, 100));

        document.Undo.Execute(new CropAction(document, new PixelRect(10, 20, 50, 40)));
        document.CropRect.ShouldBe(new PixelRect(10, 20, 50, 40));
        document.EffectiveBounds.ShouldBe(new PixelRect(10, 20, 50, 40));
        document.IsDirty.ShouldBeTrue();

        document.Undo.Undo();
        document.CropRect.ShouldBeNull();
        document.EffectiveBounds.ShouldBe(new PixelRect(0, 0, 200, 100));

        document.Undo.Redo();
        document.CropRect.ShouldBe(new PixelRect(10, 20, 50, 40));
    }

    [Fact]
    public void Konstruktor_KlemmtAnBildgrenzen()
    {
        var document = NewDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(150, 50, 100, 100)));
        document.CropRect.ShouldBe(new PixelRect(150, 50, 50, 50));
    }

    [Fact]
    public void Konstruktor_VolleFlaeche_BedeutetKeinZuschnitt()
    {
        var document = NewDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(10, 10, 20, 20)));
        document.Undo.Execute(new CropAction(document, new PixelRect(-5, -5, 500, 500)));
        document.CropRect.ShouldBeNull();
    }

    [Fact]
    public void Konstruktor_AusserhalbDesBilds_WirftException() =>
        Should.Throw<ArgumentException>(() => new CropAction(NewDocument(), new PixelRect(300, 300, 10, 10)));

    [Fact]
    public void IsNoOp_GleicherZuschnitt_IstTrue()
    {
        var document = NewDocument();
        new CropAction(document, new PixelRect(0, 0, 200, 100)).IsNoOp.ShouldBeTrue();
        new CropAction(document, new PixelRect(0, 0, 100, 100)).IsNoOp.ShouldBeFalse();
    }

    [Fact]
    public void ErneutesZuschneiden_KannBisZurOriginalgroesseVergroessern()
    {
        var document = NewDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(50, 50, 20, 20)));
        document.Undo.Execute(new CropAction(document, new PixelRect(0, 0, 200, 100)));
        document.CropRect.ShouldBeNull();
        document.BaseImage.Width.ShouldBe(200);
    }

    [Fact]
    public void Zuschnitt_LaesstObjekteUnveraendert()
    {
        var document = NewDocument();
        var rect = new RectangleAnnotation { Rect = new RectD(5, 5, 40, 30) };
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, rect));
        document.Undo.Execute(new CropAction(document, new PixelRect(100, 50, 50, 50)));
        rect.Rect.ShouldBe(new RectD(5, 5, 40, 30));
        document.Annotations.ShouldContain(rect);
    }
}

public sealed class CropGeometryTests
{
    private static readonly PixelRect Bounds = new(0, 0, 400, 300);

    [Fact]
    public void Move_KlemmtAnBildgrenzen()
    {
        CropGeometry.Move(new PixelRect(10, 10, 100, 50), -50, -50, Bounds).ShouldBe(new PixelRect(0, 0, 100, 50));
        CropGeometry.Move(new PixelRect(10, 10, 100, 50), 999, 999, Bounds).ShouldBe(new PixelRect(300, 250, 100, 50));
    }

    [Theory]
    [InlineData(CropHandle.Left, 20, 0, 120, 100, 180, 100)]
    [InlineData(CropHandle.Right, 30, 0, 100, 100, 230, 100)]
    [InlineData(CropHandle.Top, 0, 10, 100, 110, 200, 90)]
    [InlineData(CropHandle.Bottom, 0, -10, 100, 100, 200, 90)]
    [InlineData(CropHandle.TopLeft, 10, 10, 110, 110, 190, 90)]
    [InlineData(CropHandle.BottomRight, 10, 10, 100, 100, 210, 110)]
    public void Resize_Frei_VerschiebtNurDieGriffkanten(CropHandle handle, int dx, int dy, int x, int y, int width, int height)
    {
        var result = CropGeometry.Resize(new PixelRect(100, 100, 200, 100), handle, dx, dy, Bounds);
        result.ShouldBe(new PixelRect(x, y, width, height));
    }

    [Fact]
    public void Resize_Frei_KantenKoennenSichNichtUeberkreuzen()
    {
        var result = CropGeometry.Resize(new PixelRect(100, 100, 50, 50), CropHandle.Right, -500, 0, Bounds);
        result.Width.ShouldBe(CropGeometry.MinSize);
        result.X.ShouldBe(100);
    }

    [Fact]
    public void Resize_Frei_KlemmtAnBildgrenzen()
    {
        var result = CropGeometry.Resize(new PixelRect(100, 100, 200, 100), CropHandle.BottomRight, 999, 999, Bounds);
        result.ShouldBe(new PixelRect(100, 100, 300, 200));
    }

    [Fact]
    public void Resize_Eckgriff_MitSeitenverhaeltnis_HaeltGegenecke()
    {
        var start = new PixelRect(100, 100, 160, 90);
        var result = CropGeometry.Resize(start, CropHandle.BottomRight, 80, 10, Bounds, 16.0 / 9);
        result.X.ShouldBe(100);
        result.Y.ShouldBe(100);
        result.Width.ShouldBe(240);
        ((double)result.Width / result.Height).ShouldBe(16.0 / 9, 0.02);
    }

    [Fact]
    public void Resize_Eckgriff_ObenLinks_HaeltUntereRechteEcke()
    {
        var start = new PixelRect(100, 100, 160, 90);
        var result = CropGeometry.Resize(start, CropHandle.TopLeft, -40, -40, Bounds, 16.0 / 9);
        result.Right.ShouldBe(260);
        result.Bottom.ShouldBe(190);
        ((double)result.Width / result.Height).ShouldBe(16.0 / 9, 0.02);
        result.Width.ShouldBeGreaterThan(160);
    }

    [Fact]
    public void Resize_Eckgriff_MitSeitenverhaeltnis_PasstInsBild()
    {
        var start = new PixelRect(100, 100, 100, 100);
        var result = CropGeometry.Resize(start, CropHandle.BottomRight, 5000, 5000, Bounds, 1);
        result.ShouldBe(new PixelRect(100, 100, 200, 200));
        Bounds.Intersect(result).ShouldBe(result);
    }

    [Fact]
    public void Resize_Kantengriff_MitSeitenverhaeltnis_BleibtZentriert()
    {
        var start = new PixelRect(100, 100, 100, 100);
        var result = CropGeometry.Resize(start, CropHandle.Right, 100, 0, Bounds, 1);
        result.X.ShouldBe(100);
        result.Width.ShouldBe(200);
        result.Height.ShouldBe(200);
        result.Y.ShouldBe(50);
    }

    [Fact]
    public void Resize_Kantengriff_Oben_MitSeitenverhaeltnis()
    {
        var start = new PixelRect(100, 100, 100, 100);
        var result = CropGeometry.Resize(start, CropHandle.Top, 0, -50, Bounds, 2);
        result.Bottom.ShouldBe(200);
        result.Height.ShouldBe(150);
        result.Width.ShouldBe(300);
        Bounds.Intersect(result).ShouldBe(result);
    }

    [Fact]
    public void FromDrag_OhneVerhaeltnis_NormalisiertUndKlemmt()
    {
        CropGeometry.FromDrag(new PointD(100, 100), new PointD(40, 20), Bounds).ShouldBe(new PixelRect(40, 20, 60, 80));
        CropGeometry.FromDrag(new PointD(100, 100), new PointD(999, 999), Bounds).ShouldBe(new PixelRect(100, 100, 300, 200));
    }

    [Fact]
    public void FromDrag_MitVerhaeltnis_BleibtImBild()
    {
        var rect = CropGeometry.FromDrag(new PointD(300, 250), new PointD(999, 999), Bounds, 1);
        rect.Width.ShouldBe(rect.Height);
        Bounds.Intersect(rect).ShouldBe(rect);
    }

    [Fact]
    public void ApplyAspect_BehaeltMitteUndBleibtImBild()
    {
        var result = CropGeometry.ApplyAspect(new PixelRect(0, 0, 400, 300), 1, Bounds);
        result.Width.ShouldBe(result.Height);
        result.Height.ShouldBe(300);
        Bounds.Intersect(result).ShouldBe(result);
    }

    [Fact]
    public void Snap_RastetNurInnerhalbDerToleranzEin()
    {
        CropGeometry.Snap(103, [100, 200], 8).ShouldBe(100);
        CropGeometry.Snap(120, [100, 200], 8).ShouldBe(120);
        CropGeometry.Snap(197, [100, 200], 8).ShouldBe(200);
    }

    [Fact]
    public void BoundsOfObjects_LiefertUnionMitRandUndKlemmt()
    {
        var a = new RectangleAnnotation { Rect = new RectD(50, 60, 40, 30) };
        var b = new RectangleAnnotation { Rect = new RectD(200, 150, 30, 30) };
        var hidden = new RectangleAnnotation { Rect = new RectD(0, 0, 400, 300), IsVisible = false };
        var rect = CropGeometry.BoundsOfObjects([a, b, hidden], 10, Bounds);
        rect.ShouldNotBeNull();
        rect.Value.X.ShouldBeLessThanOrEqualTo(40);
        rect.Value.Right.ShouldBeGreaterThanOrEqualTo(240);
        Bounds.Intersect(rect.Value).ShouldBe(rect.Value);

        CropGeometry.BoundsOfObjects([], 10, Bounds).ShouldBeNull();
    }

    [Fact]
    public void DetectContent_ErkenntEinfarbigenRand()
    {
        var image = new PixelBuffer(60, 40);
        Fill(image, 255, 255, 255);
        for (var y = 10; y < 25; y++)
        {
            for (var x = 20; x < 50; x++)
            {
                Set(image, x, y, 10, 20, 30);
            }
        }

        CropGeometry.DetectContent(image).ShouldBe(new PixelRect(20, 10, 30, 15));
    }

    [Fact]
    public void DetectContent_EinfarbigesBild_LiefertNull()
    {
        var image = new PixelBuffer(10, 10);
        Fill(image, 5, 5, 5);
        CropGeometry.DetectContent(image).ShouldBeNull();
    }

    [Fact]
    public void DetectContent_OhneRand_LiefertNull()
    {
        var image = new PixelBuffer(10, 10);
        Fill(image, 255, 255, 255);
        Set(image, 0, 0, 0, 0, 0);
        Set(image, 9, 9, 0, 0, 0);
        CropGeometry.DetectContent(image).ShouldBeNull();
    }

    [Fact]
    public void DetectContent_ToleranzIgnoriertLeichtesRauschen()
    {
        var image = new PixelBuffer(30, 30);
        Fill(image, 100, 100, 100);
        Set(image, 2, 2, 102, 100, 99);
        Set(image, 15, 15, 0, 0, 0);
        CropGeometry.DetectContent(image, tolerance: 4).ShouldBe(new PixelRect(15, 15, 1, 1));
        CropGeometry.DetectContent(image).ShouldBe(new PixelRect(2, 2, 14, 14));
    }

    private static void Fill(PixelBuffer image, byte r, byte g, byte b)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                Set(image, x, y, r, g, b);
            }
        }
    }

    private static void Set(PixelBuffer image, int x, int y, byte r, byte g, byte b)
    {
        var i = (y * image.Stride) + (x * PixelBuffer.BytesPerPixel);
        image.Pixels[i] = b;
        image.Pixels[i + 1] = g;
        image.Pixels[i + 2] = r;
        image.Pixels[i + 3] = 255;
    }
}
