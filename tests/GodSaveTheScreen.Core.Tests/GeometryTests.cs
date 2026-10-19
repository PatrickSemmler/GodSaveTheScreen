using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void Normalize_NegativeGroesse_WirdPositiv()
    {
        new RectD(10, 10, -4, -6).Normalize().ShouldBe(new RectD(6, 4, 4, 6));
    }

    [Fact]
    public void FromPoints_Rueckwaerts_NormalisiertAutomatisch()
    {
        RectD.FromPoints(new PointD(10, 10), new PointD(2, 4)).ShouldBe(new RectD(2, 4, 8, 6));
    }

    [Fact]
    public void Intersect_Ueberlappend_LiefertSchnittmenge()
    {
        new RectD(0, 0, 10, 10).Intersect(new RectD(5, 5, 10, 10)).ShouldBe(new RectD(5, 5, 5, 5));
    }

    [Fact]
    public void Intersect_Disjunkt_LiefertLeer()
    {
        new RectD(0, 0, 4, 4).Intersect(new RectD(10, 10, 4, 4)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Contains_RechteRandExklusiv()
    {
        var r = new RectD(0, 0, 10, 10);
        r.Contains(new PointD(0, 0)).ShouldBeTrue();
        r.Contains(new PointD(10, 5)).ShouldBeFalse();
    }

    [Fact]
    public void Inflate_VergroessertAllseitig()
    {
        new RectD(5, 5, 10, 10).Inflate(2, 3).ShouldBe(new RectD(3, 2, 14, 16));
    }

    [Fact]
    public void PixelRect_Intersect_KlemmtAuf()
    {
        new PixelRect(-5, -5, 10, 10).Intersect(new PixelRect(0, 0, 8, 8)).ShouldBe(new PixelRect(0, 0, 5, 5));
    }

    [Fact]
    public void Crop_InnerhalbDesPuffers_KopiertPixel()
    {
        var buffer = new PixelBuffer(4, 4);
        buffer.Pixels[((1 * 4) + 2) * 4] = 99; // Pixel (2,1), Kanal B

        var crop = buffer.Crop(new PixelRect(2, 1, 2, 2));

        crop.Width.ShouldBe(2);
        crop.Height.ShouldBe(2);
        crop.Pixels[0].ShouldBe((byte)99);
    }

    [Fact]
    public void Crop_UeberDenRand_WirdGeklemmt()
    {
        new PixelBuffer(4, 4).Crop(new PixelRect(2, 2, 10, 10)).Width.ShouldBe(2);
    }

    [Fact]
    public void Crop_LeereAuswahl_LiefertLeerenPuffer()
    {
        var crop = new PixelBuffer(4, 4).Crop(new PixelRect(1, 1, 0, 0));
        crop.Width.ShouldBe(0);
        crop.Height.ShouldBe(0);
    }

    [Fact]
    public void Clone_KopiertPixelUnabhaengig()
    {
        var a = new PixelBuffer(1, 1);
        var b = a.Clone();
        b.Pixels[0] = 5;
        a.Pixels[0].ShouldBe((byte)0);
    }
}
