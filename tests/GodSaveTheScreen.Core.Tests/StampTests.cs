using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

public sealed class StampTests
{
    private static StampAnnotation Stamp(string text = "OK", double fontSize = 40, StampFrameShape shape = StampFrameShape.Rectangle) => new()
    {
        Text = text,
        FontSize = fontSize,
        FrameThickness = 5,
        FrameShape = shape,
        Center = new PointD(200, 100),
    };

    [Fact]
    public void Rahmen_PasstSichAnDenTextAn()
    {
        var shortText = Stamp("OK");
        var longText = Stamp("VERTRAULICH");
        var twoLines = Stamp("OK\nOK");

        longText.Rect.Width.ShouldBeGreaterThan(shortText.Rect.Width);
        twoLines.Rect.Height.ShouldBeGreaterThan(shortText.Rect.Height);

        var metrics = longText.Metrics;
        var (padX, padY) = StampAnnotation.Padding(40);
        metrics.Size.Width.ShouldBe(metrics.TextSize.Width + (2 * padX) + 10, 1e-9);
        metrics.Size.Height.ShouldBe(metrics.TextSize.Height + (2 * padY) + 10, 1e-9);
        longText.Rect.Center.ShouldBe(longText.Center);
    }

    [Fact]
    public void Rahmenformen_DoppelrahmenUndOvalSindGroesser()
    {
        var rect = Stamp(shape: StampFrameShape.Rectangle).Rect;

        Stamp(shape: StampFrameShape.DoubleLine).Rect.Width.ShouldBeGreaterThan(rect.Width);
        Stamp(shape: StampFrameShape.Oval).Rect.Width.ShouldBeGreaterThan(rect.Width);
        Stamp(shape: StampFrameShape.Rounded).Rect.ShouldBe(rect);
        Stamp(shape: StampFrameShape.DoubleLine).Metrics.InnerThickness.ShouldBe(2.5);
    }

    [Fact]
    public void ScaleSizes_SkaliertSchriftUndRahmenGemeinsam()
    {
        var stamp = Stamp("GENEHMIGT");
        var before = stamp.Rect;

        stamp.ScaleSizes(2);

        stamp.FontSize.ShouldBe(80);
        stamp.FrameThickness.ShouldBe(10);
        stamp.Rect.Width.ShouldBe(before.Width * 2, 1e-6);
        stamp.Rect.Height.ShouldBe(before.Height * 2, 1e-6);
        stamp.Center.ShouldBe(new PointD(200, 100));
    }

    [Fact]
    public void ScaleSizes_BleibtInnerhalbDerGrenzen()
    {
        var stamp = Stamp(fontSize: 10);

        stamp.ScaleSizes(0.01);

        stamp.FontSize.ShouldBe(StampAnnotation.MinFontSize);
    }

    [Fact]
    public void Drehung_WirdNormalisiertUndVorzeichenbehaftetGeliefert()
    {
        var stamp = Stamp();

        stamp.Rotation = -8;

        stamp.Rotation.ShouldBe(352);
        stamp.SignedRotation.ShouldBe(-8, 1e-9);
        stamp.Bounds.Width.ShouldBeGreaterThan(stamp.Rect.Width);
    }

    [Fact]
    public void HitTest_BeruecksichtigtDieDrehung()
    {
        var stamp = Stamp("VERTRAULICH");
        var corner = new PointD(stamp.Rect.Right - 2, stamp.Rect.Y + 2);
        stamp.HitTest(corner, 0).ShouldBeTrue();

        stamp.Rotation = 90;

        stamp.HitTest(corner, 0).ShouldBeFalse();
        stamp.HitTest(stamp.Center, 0).ShouldBeTrue();
    }

    [Fact]
    public void MapPoints_VerschiebtNurDieMitte()
    {
        var stamp = Stamp();
        var size = stamp.Rect.Width;

        stamp.Translate(new VectorD(10, -5));

        stamp.Center.ShouldBe(new PointD(210, 95));
        stamp.Rect.Width.ShouldBe(size);
    }

    [Fact]
    public void Clone_UebernimmtAlleEigenschaften()
    {
        var original = Stamp("ENTWURF");
        original.Rotation = -12;
        original.Seed = 4711;
        original.Wear = 80;
        original.InkBlend = true;
        original.Color = ArgbColor.FromRgb(0, 0, 200);
        original.FrameShape = StampFrameShape.Oval;

        var copy = original.Clone().ShouldBeOfType<StampAnnotation>();

        copy.Id.ShouldNotBe(original.Id);
        copy.Text.ShouldBe("ENTWURF");
        copy.Rotation.ShouldBe(original.Rotation);
        copy.Seed.ShouldBe(4711);
        copy.Wear.ShouldBe(80);
        copy.InkBlend.ShouldBeTrue();
        copy.Color.ShouldBe(original.Color);
        copy.FrameShape.ShouldBe(StampFrameShape.Oval);
        copy.Rect.ShouldBe(original.Rect);
    }

    [Fact]
    public void Json_Roundtrip()
    {
        var original = Stamp("ERLEDIGT\nHEUTE");
        original.Rotation = 350;
        original.Seed = 99;
        original.Wear = 33;
        original.FontWeight = 700;
        original.FontStyle = FontStyleKind.Italic;
        original.InkBlend = true;
        original.Opacity = 0.7;
        original.FrameShape = StampFrameShape.DoubleLine;

        var json = AnnotationSerializer.Serialize([original]);
        var restored = AnnotationSerializer.Deserialize(json).ShouldHaveSingleItem().ShouldBeOfType<StampAnnotation>();

        json.ShouldContain("\"type\":\"stamp\"");
        restored.Id.ShouldBe(original.Id);
        restored.Text.ShouldBe(original.Text);
        restored.Center.ShouldBe(original.Center);
        restored.Rotation.ShouldBe(350);
        restored.Seed.ShouldBe(99);
        restored.Wear.ShouldBe(33);
        restored.FontWeight.ShouldBe(700);
        restored.FontStyle.ShouldBe(FontStyleKind.Italic);
        restored.InkBlend.ShouldBeTrue();
        restored.Opacity.ShouldBe(0.7);
        restored.FrameShape.ShouldBe(StampFrameShape.DoubleLine);
        restored.FrameThickness.ShouldBe(5);
    }

    [Fact]
    public void Einstellungen_FehlendeStempelwerteWerdenErgaenzt()
    {
        var settings = new AppSettings { StampDefaults = null! }.Normalized();

        settings.StampDefaults.ShouldBe(new StampStyle());
        settings.StampDefaults.Rotation.ShouldBe(-8);
        settings.StampDefaults.Wear.ShouldBe(50);
    }
}
