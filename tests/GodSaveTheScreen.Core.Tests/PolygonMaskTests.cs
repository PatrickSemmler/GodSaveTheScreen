using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Tests;

public sealed class PolygonMaskTests
{
    private static PixelBuffer Opaque(int width, int height)
    {
        var buffer = new PixelBuffer(width, height);
        for (var i = 0; i < buffer.Pixels.Length; i += PixelBuffer.BytesPerPixel)
        {
            buffer.Pixels[i] = 10;
            buffer.Pixels[i + 1] = 20;
            buffer.Pixels[i + 2] = 30;
            buffer.Pixels[i + 3] = 255;
        }

        return buffer;
    }

    private static byte Alpha(PixelBuffer b, int x, int y) => b.Pixels[(((y * b.Width) + x) * PixelBuffer.BytesPerPixel) + 3];

    [Fact]
    public void BoundingBox_Punkte_UmschliesstAlleMitAufgerundetemEnde()
    {
        var box = PolygonMask.BoundingBox([new PointD(2.2, 3.7), new PointD(10.1, 4), new PointD(6, 9.5)]);

        box.ShouldBe(new PixelRect(2, 3, 9, 7));
    }

    [Fact]
    public void BoundingBox_Leer_IstLeer() => PolygonMask.BoundingBox([]).IsEmpty.ShouldBeTrue();

    [Fact]
    public void Apply_Konvex_Dreieck_InnenOpakAussenTransparent()
    {
        var source = Opaque(20, 20);
        var result = PolygonMask.Apply(source, [new PointD(0, 0), new PointD(20, 0), new PointD(0, 20)]);

        result.Width.ShouldBe(20);
        Alpha(result, 2, 2).ShouldBe((byte)255);
        Alpha(result, 17, 17).ShouldBe((byte)0);
    }

    [Fact]
    public void Apply_Rechteck_SchneidetBoundingBoxAusUndBehaeltFarbe()
    {
        var source = Opaque(30, 30);
        var result = PolygonMask.Apply(source, [new PointD(5, 6), new PointD(15, 6), new PointD(15, 16), new PointD(5, 16)]);

        result.Width.ShouldBe(10);
        result.Height.ShouldBe(10);
        Alpha(result, 0, 0).ShouldBe((byte)255);
        Alpha(result, 9, 9).ShouldBe((byte)255);
        result.Pixels[0].ShouldBe((byte)10);
    }

    [Fact]
    public void Apply_Konkav_UFoermig_AussparungIstTransparent()
    {
        // U-Form: Aussparung oben in der Mitte (x 6..14, y 0..10).
        PointD[] u = [new(0, 0), new(6, 0), new(6, 10), new(14, 10), new(14, 0), new(20, 0), new(20, 20), new(0, 20)];
        var result = PolygonMask.Apply(Opaque(20, 20), u);

        Alpha(result, 10, 5).ShouldBe((byte)0);
        Alpha(result, 10, 15).ShouldBe((byte)255);
        Alpha(result, 2, 5).ShouldBe((byte)255);
    }

    [Fact]
    public void Apply_Selbstueberschneidend_NutztEvenOdd()
    {
        // Fünfzack-Stern: das Zentrum liegt in zwei Windungen und ist bei Even-Odd leer.
        var points = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var angle = (-Math.PI / 2) + (i * 4 * Math.PI / 5);
                return new PointD(50 + (40 * Math.Cos(angle)), 50 + (40 * Math.Sin(angle)));
            })
            .ToList();
        var result = PolygonMask.Apply(Opaque(100, 100), points);
        var box = PolygonMask.BoundingBox(points);

        Alpha(result, 50 - box.X, 50 - box.Y).ShouldBe((byte)0);
        // Spitze oben (in einem Zacken) ist gefüllt.
        Alpha(result, 50 - box.X, 20 - box.Y).ShouldBe((byte)255);
    }

    [Fact]
    public void Apply_MitKantenglaettung_ErzeugtTeiltransparenteRandpixel()
    {
        var result = PolygonMask.Apply(Opaque(10, 10), [new PointD(0, 0), new PointD(10, 0), new PointD(0, 10)], antiAlias: true);

        var edge = Alpha(result, 5, 4);
        edge.ShouldBeGreaterThan((byte)0);
        edge.ShouldBeLessThan((byte)255);
        Alpha(result, 1, 1).ShouldBe((byte)255);
    }

    [Fact]
    public void Apply_WenigerAlsDreiPunkte_LiefertLeerenPuffer()
    {
        PolygonMask.Apply(Opaque(10, 10), [new PointD(1, 1), new PointD(5, 5)]).Width.ShouldBe(0);
    }

    [Fact]
    public void Apply_PolygonAusserhalbDesBilds_WirdGeklemmt()
    {
        var result = PolygonMask.Apply(Opaque(10, 10), [new PointD(-5, -5), new PointD(5, -5), new PointD(5, 5), new PointD(-5, 5)]);

        result.Width.ShouldBe(5);
        result.Height.ShouldBe(5);
    }

    [Fact]
    public void CornerMask_Rundet_EckenTransparentMitteBleibt()
    {
        var image = Opaque(40, 40);
        CornerMask.Apply(image, 8);

        Alpha(image, 0, 0).ShouldBe((byte)0);
        Alpha(image, 39, 39).ShouldBe((byte)0);
        Alpha(image, 20, 20).ShouldBe((byte)255);
        Alpha(image, 20, 0).ShouldBe((byte)255);
    }
}
