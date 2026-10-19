using System.Collections.Immutable;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Tests;

public sealed class ArgbColorTests
{
    [Theory]
    [InlineData("#80FF0000", 0x80, 0xFF, 0x00, 0x00)]
    [InlineData("#FF0000", 0xFF, 0xFF, 0x00, 0x00)]
    [InlineData("0000FF", 0xFF, 0x00, 0x00, 0xFF)]
    [InlineData("#F00", 0xFF, 0xFF, 0x00, 0x00)]
    [InlineData("#8F00", 0x88, 0xFF, 0x00, 0x00)]
    public void TryParse_AkzeptiertVerbreiteteFormen(string text, int a, int r, int g, int b)
    {
        ArgbColor.TryParse(text, out var color).ShouldBeTrue();
        color.ShouldBe(new ArgbColor((byte)a, (byte)r, (byte)g, (byte)b));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    [InlineData("rot")]
    public void TryParse_LehntUngueltigesAb(string text) => ArgbColor.TryParse(text, out _).ShouldBeFalse();

    [Fact]
    public void ToString_ErgibtAarrggbb() => new ArgbColor(0x12, 0xAB, 0xCD, 0xEF).ToString().ShouldBe("#12ABCDEF");
}

public sealed class AnnotationModelTests
{
    private static RectangleAnnotation Rect(double x, double y, double w, double h, ArgbColor? fill = null) => new()
    {
        Rect = new RectD(x, y, w, h),
        Stroke = new StrokeStyle(ArgbColor.Red, 4),
        Fill = fill ?? ArgbColor.Transparent,
    };

    [Fact]
    public void Rechteck_OhneFuellung_TrifftNurDenRand()
    {
        var rect = Rect(10, 10, 100, 80);
        rect.HitTest(new PointD(10, 50), 2).ShouldBeTrue("auf dem Rand");
        rect.HitTest(new PointD(60, 50), 2).ShouldBeFalse("im Inneren");
        rect.HitTest(new PointD(300, 300), 2).ShouldBeFalse("weit außerhalb");
    }

    [Fact]
    public void Rechteck_MitFuellung_TrifftDasInnere()
    {
        var rect = Rect(10, 10, 100, 80, ArgbColor.FromRgb(0, 0, 255));
        rect.HitTest(new PointD(60, 50), 2).ShouldBeTrue();
    }

    [Fact]
    public void Ellipse_NormierteGleichung()
    {
        var ellipse = new EllipseAnnotation { Rect = new RectD(0, 0, 100, 60), Stroke = new StrokeStyle(ArgbColor.Red, 2) };
        ellipse.HitTest(new PointD(100, 30), 2).ShouldBeTrue("rechter Scheitel");
        ellipse.HitTest(new PointD(50, 30), 2).ShouldBeFalse("Mittelpunkt ohne Füllung");
        ellipse.HitTest(new PointD(2, 2), 2).ShouldBeFalse("Ecke der Bounding-Box liegt außerhalb der Ellipse");

        ellipse.Fill = ArgbColor.White;
        ellipse.HitTest(new PointD(50, 30), 2).ShouldBeTrue();
    }

    [Fact]
    public void Linie_AbstandZumSegment()
    {
        var line = new LineAnnotation { Start = new PointD(0, 0), End = new PointD(100, 0), Stroke = new StrokeStyle(ArgbColor.Red, 4) };
        line.HitTest(new PointD(50, 3), 2).ShouldBeTrue();
        line.HitTest(new PointD(50, 10), 2).ShouldBeFalse();
        line.HitTest(new PointD(120, 0), 2).ShouldBeFalse("jenseits des Endpunkts");
    }

    [Fact]
    public void Polygon_EvenOdd_UndRand()
    {
        var polygon = new PolygonAnnotation
        {
            Points = [new PointD(0, 0), new PointD(100, 0), new PointD(100, 100), new PointD(0, 100)],
            Stroke = new StrokeStyle(ArgbColor.Red, 2),
        };
        polygon.HitTest(new PointD(50, 50), 2).ShouldBeFalse("ohne Füllung nur Rand");
        polygon.HitTest(new PointD(50, 0), 2).ShouldBeTrue();

        polygon.Fill = ArgbColor.White;
        polygon.HitTest(new PointD(50, 50), 2).ShouldBeTrue();
        polygon.HitTest(new PointD(150, 50), 2).ShouldBeFalse();
    }

    [Fact]
    public void Dreieck_FuellungUndFlip()
    {
        var triangle = new TriangleAnnotation { Rect = new RectD(0, 0, 100, 100), Fill = ArgbColor.White, Stroke = new StrokeStyle(ArgbColor.Red, 1) };
        triangle.HitTest(new PointD(50, 80), 1).ShouldBeTrue("unten mittig liegt im Dreieck");
        triangle.HitTest(new PointD(5, 5), 1).ShouldBeFalse("obere linke Ecke liegt außerhalb");

        triangle.IsFlipped = true;
        triangle.HitTest(new PointD(5, 5), 1).ShouldBeTrue("gespiegelt zeigt die Spitze nach unten");
    }

    [Fact]
    public void Clone_ErzeugtNeueIdUndTiefeKopie()
    {
        var original = new PolygonAnnotation
        {
            Name = "Poly",
            Opacity = 0.5,
            Points = [new PointD(1, 2), new PointD(3, 4), new PointD(5, 0)],
            Stroke = new StrokeStyle(ArgbColor.Red, 7, DashKind.Dot),
        };

        var copy = (PolygonAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Name.ShouldBe("Poly");
        copy.Opacity.ShouldBe(0.5);
        copy.Points.ShouldBe(original.Points);
        copy.Stroke.ShouldBe(original.Stroke);

        copy.Translate(new VectorD(10, 10));
        original.Points[0].ShouldBe(new PointD(1, 2), "die Kopie darf das Original nicht verändern");
    }

    [Fact]
    public void CopyFrom_UebernimmtZustandBehaeltId()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(50, 50, 20, 20, ArgbColor.White);
        var id = a.Id;

        a.CopyFrom(b);

        a.Id.ShouldBe(id);
        a.Rect.ShouldBe(b.Rect);
        a.Fill.ShouldBe(ArgbColor.White);
    }

    [Fact]
    public void MapPoints_SkaliertGeometrie()
    {
        var rect = Rect(10, 10, 20, 20);
        rect.MapPoints(p => new PointD(p.X * 2, p.Y * 3));
        rect.Rect.ShouldBe(new RectD(20, 30, 40, 60));
    }

    [Fact]
    public void MapPoints_SpiegelungErgibtNormalisiertesRechteck()
    {
        var rect = Rect(10, 10, 20, 20);
        rect.MapPoints(p => new PointD(-p.X, p.Y));
        rect.Bounds.ShouldBe(new RectD(-30, 10, 20, 20));
    }

    [Fact]
    public void Serializer_Roundtrip_AllerFormen()
    {
        Annotation[] originals =
        [
            Rect(1, 2, 30, 40, ArgbColor.FromRgb(1, 2, 3)),
            new EllipseAnnotation { Rect = new RectD(5, 5, 10, 20), Opacity = 0.4, Name = "E" },
            new TriangleAnnotation { Rect = new RectD(0, 0, 9, 9), Kind = TriangleKind.Right, IsFlipped = true },
            new LineAnnotation { Start = new PointD(1, 1), End = new PointD(9, 9), EndCap = LineEndStyle.Arrow, CapSize = 5 },
            new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(5, 0), new PointD(2, 4)], IsClosed = false, IsLocked = true },
        ];

        var json = AnnotationSerializer.Serialize(originals);
        var restored = AnnotationSerializer.Deserialize(json);

        restored.Count.ShouldBe(originals.Length);
        for (var i = 0; i < originals.Length; i++)
        {
            restored[i].GetType().ShouldBe(originals[i].GetType());
            restored[i].Id.ShouldBe(originals[i].Id);
            restored[i].Bounds.ShouldBe(originals[i].Bounds);
            restored[i].Opacity.ShouldBe(originals[i].Opacity);
            restored[i].IsLocked.ShouldBe(originals[i].IsLocked);
        }

        ((PolygonAnnotation)restored[4]).IsClosed.ShouldBeFalse();
        ((LineAnnotation)restored[3]).EndCap.ShouldBe(LineEndStyle.Arrow);
        ((RectangleAnnotation)restored[0]).Fill.ShouldBe(ArgbColor.FromRgb(1, 2, 3));
    }

    [Fact]
    public void Serializer_Diskriminatoren_SindStabil()
    {
        // Diese Kennungen sind Teil des Dateiformats und dürfen sich nie ändern.
        var json = AnnotationSerializer.Serialize(
        [
            new RectangleAnnotation(),
            new EllipseAnnotation(),
            new TriangleAnnotation(),
            new LineAnnotation(),
            new PolygonAnnotation { Points = ImmutableArray<PointD>.Empty },
        ]);

        foreach (var tag in new[] { "\"type\":\"rect\"", "\"type\":\"ellipse\"", "\"type\":\"triangle\"", "\"type\":\"line\"", "\"type\":\"polygon\"" })
        {
            json.ShouldContain(tag);
        }

        json.ShouldNotContain("Bounds");
    }

    [Fact]
    public void DeserializeAsCopies_VergibtNeueIds()
    {
        var original = Rect(0, 0, 5, 5);
        var copies = AnnotationSerializer.DeserializeAsCopies(AnnotationSerializer.Serialize([original]));
        copies[0].Id.ShouldNotBe(original.Id);
    }
}
