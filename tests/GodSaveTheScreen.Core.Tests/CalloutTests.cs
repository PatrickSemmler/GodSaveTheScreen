using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

public sealed class CalloutTests
{
    private static readonly RectD Body = new(100, 100, 200, 80);

    private static CalloutAnnotation Callout(PointD tail, CalloutShape shape = CalloutShape.Rounded) => new()
    {
        Rect = Body,
        Tail = tail,
        Shape = shape,
        CornerRadius = 10,
        TailWidth = 20,
        Text = "Hallo",
    };

    [Theory]
    [InlineData(150, 260, "unten")]
    [InlineData(150, 20, "oben")]
    [InlineData(400, 140, "rechts")]
    [InlineData(10, 140, "links")]
    public void Spitze_AnsatzLiegtAufDerSeiteZurSpitze(double x, double y, string side)
    {
        var tail = Callout(new PointD(x, y)).TailGeometry.ShouldNotBeNull();

        var a = tail.BaseStart;
        var b = tail.BaseEnd;
        a.DistanceTo(b).ShouldBe(20, 1e-6);
        switch (side)
        {
            case "unten":
                a.Y.ShouldBe(Body.Bottom - 1, 1e-6);
                b.Y.ShouldBe(Body.Bottom - 1, 1e-6);
                break;
            case "oben":
                a.Y.ShouldBe(Body.Y + 1, 1e-6);
                break;
            case "rechts":
                a.X.ShouldBe(Body.Right - 1, 1e-6);
                break;
            default:
                a.X.ShouldBe(Body.X + 1, 1e-6);
                break;
        }
    }

    [Fact]
    public void Spitze_WandertBeimZiehenUmDieEckeAufDieNachbarseite()
    {
        // Unterhalb rechts: zuerst Unterkante, weiter rechts dann rechte Kante.
        var below = Callout(new PointD(320, 300)).TailGeometry.ShouldNotBeNull();
        var right = Callout(new PointD(500, 200)).TailGeometry.ShouldNotBeNull();

        below.BaseStart.Y.ShouldBe(Body.Bottom - 1, 1e-6);
        right.BaseStart.X.ShouldBe(Body.Right - 1, 1e-6);
    }

    [Fact]
    public void Spitze_BleibtImGeradenTeilDerSeite()
    {
        // Ziel rechts, knapp über der Höhe der Unterkante: Ansatz an der rechten Seite, endet vor der Rundung.
        var tail = Callout(new PointD(400, 200)).TailGeometry.ShouldNotBeNull();

        tail.BaseStart.X.ShouldBe(Body.Right - 1, 1e-6);
        Math.Max(tail.BaseStart.Y, tail.BaseEnd.Y).ShouldBe(Body.Bottom - 10, 1e-6);
    }

    [Fact]
    public void Spitze_ImKoerper_KeineSpitzeUndBoundsNurKoerper()
    {
        var callout = Callout(new PointD(150, 120));

        callout.TailGeometry.ShouldBeNull();
        callout.Bounds.ShouldBe(Body);
    }

    [Fact]
    public void Bounds_SchliessenDieSpitzeEin()
    {
        var callout = Callout(new PointD(50, 300));

        callout.Bounds.ShouldBe(new RectD(50, 100, 250, 200));
    }

    [Fact]
    public void GebogeneSpitze_HatKontrollpunkte()
    {
        var callout = Callout(new PointD(150, 260));
        callout.TailCurved = true;

        var tail = callout.TailGeometry.ShouldNotBeNull();
        tail.ControlStart.ShouldNotBeNull();
        tail.ControlEnd.ShouldNotBeNull();
        tail.Outline().Length.ShouldBeGreaterThan(10);
    }

    [Fact]
    public void Gedankenblase_SpitzeAusKleinerWerdendenKreisen()
    {
        var tail = Callout(new PointD(150, 300), CalloutShape.Cloud).TailGeometry.ShouldNotBeNull();

        tail.IsBubbles.ShouldBeTrue();
        tail.Bubbles.Length.ShouldBe(3);
        tail.Bubbles[0].Radius.ShouldBeGreaterThan(tail.Bubbles[1].Radius);
        tail.Bubbles[1].Radius.ShouldBeGreaterThan(tail.Bubbles[2].Radius);
        tail.Bubbles[2].Center.DistanceTo(new PointD(150, 300)).ShouldBe(tail.Bubbles[2].Radius, 1e-6);
    }

    [Fact]
    public void Ellipse_AnsatzLiegtAufDemRand()
    {
        var tail = Callout(new PointD(200, 400), CalloutShape.Ellipse).TailGeometry.ShouldNotBeNull();

        // Knapp innerhalb der Ellipse (1 px nach innen gezogen).
        HitTesting.EllipseNorm(tail.BaseStart, Body).ShouldBeLessThan(1);
        HitTesting.EllipseNorm(tail.BaseStart, Body).ShouldBeGreaterThan(0.95);
    }

    [Fact]
    public void HitTest_KoerperUndSpitze()
    {
        var callout = Callout(new PointD(150, 260));

        callout.HitTest(new PointD(200, 140), 0).ShouldBeTrue();
        callout.HitTest(new PointD(154, 250), 0).ShouldBeTrue();
        callout.HitTest(new PointD(250, 255), 1).ShouldBeFalse();

        // Abgerundete Ecke: Eckpunkt selbst liegt außerhalb.
        callout.HitTest(new PointD(100.5, 100.5), 0).ShouldBeFalse();
    }

    [Fact]
    public void MapPoints_VerschiebtKoerperUndSpitze()
    {
        var callout = Callout(new PointD(150, 260));

        callout.Translate(new VectorD(10, -5));

        callout.Rect.ShouldBe(new RectD(110, 95, 200, 80));
        callout.Tail.ShouldBe(new PointD(160, 255));
    }

    [Fact]
    public void ScaleSizes_SkaliertSchriftRahmenUndSpitze()
    {
        var callout = Callout(new PointD(150, 260));
        callout.Border = new StrokeStyle(ArgbColor.Black, 2);

        callout.ScaleSizes(2);

        callout.FontSize.ShouldBe(40);
        callout.TailWidth.ShouldBe(40);
        callout.CornerRadius.ShouldBe(20);
        callout.Padding.ShouldBe(20);
        callout.Border.Thickness.ShouldBe(4);
    }

    [Fact]
    public void TextArea_IstKoerperOhneInnenabstand()
    {
        var callout = Callout(new PointD(150, 260));

        callout.TextArea.ShouldBe(new RectD(110, 110, 180, 60));
        Callout(new PointD(150, 260), CalloutShape.Ellipse).TextArea.Width.ShouldBeLessThan(180);
    }

    [Fact]
    public void FitHeightToText_WaechstNachUntenBisDerTextPasst()
    {
        var callout = Callout(new PointD(150, 260));
        callout.Text = "1\n2\n3\n4\n5\n6";

        callout.FitHeightToText().ShouldBeTrue();

        callout.Rect.Y.ShouldBe(Body.Y);
        callout.Rect.Width.ShouldBe(Body.Width);
        callout.TextArea.Height.ShouldBeGreaterThanOrEqualTo(callout.CreateTextBlock().LayoutSize.Height - 0.5);
        callout.FitHeightToText().ShouldBeFalse();
    }

    [Fact]
    public void VisualMargin_RahmenAussenPlusLeuchten()
    {
        var callout = Callout(new PointD(150, 260));
        callout.Border = new StrokeStyle(ArgbColor.Black, 4);
        callout.BorderBlur = 6;

        callout.VisualMargin.ShouldBe(11);
    }

    [Fact]
    public void Serializer_Roundtrip()
    {
        var original = Callout(new PointD(150, 260), CalloutShape.Cloud);
        original.TailCurved = true;
        original.Border = new StrokeStyle(ArgbColor.FromRgb(1, 2, 3), 3);
        original.BorderBlur = 5;
        original.Shadow = new ShadowStyle(ArgbColor.Black, 2, 3);
        original.TextAlignment = TextAlignmentKind.Left;
        original.Decorations = TextDecorationKind.Underline;

        var json = AnnotationSerializer.Serialize([original]);
        var restored = AnnotationSerializer.Deserialize(json).ShouldHaveSingleItem().ShouldBeOfType<CalloutAnnotation>();

        json.ShouldContain("callout");
        restored.Rect.ShouldBe(original.Rect);
        restored.Tail.ShouldBe(original.Tail);
        restored.Shape.ShouldBe(CalloutShape.Cloud);
        restored.TailCurved.ShouldBeTrue();
        restored.Border.ShouldBe(original.Border);
        restored.BorderBlur.ShouldBe(5);
        restored.Shadow.ShouldBe(original.Shadow);
        restored.TextAlignment.ShouldBe(TextAlignmentKind.Left);
        restored.Decorations.ShouldBe(TextDecorationKind.Underline);
        restored.Text.ShouldBe("Hallo");
    }

    [Fact]
    public void Einstellungen_FehlendeSprechblasenwerte_WerdenErgaenzt()
    {
        var settings = new AppSettings { CalloutDefaults = null! }.Normalized();

        settings.CalloutDefaults.ShouldBe(new CalloutStyle());

        var broken = new CalloutStyle { Border = null!, Shadow = null!, FontFamilyName = null!, FontSize = 0 }.Normalized();
        broken.Border.ShouldBe(new CalloutStyle().Border);
        broken.Shadow.ShouldBe(ShadowStyle.None);
        broken.FontFamilyName.ShouldBe("Segoe UI");
        broken.FontSize.ShouldBe(20);
    }

    [Fact]
    public void Drehung_ZielpunktBleibt_SpitzeImLokalenSystem()
    {
        var callout = Callout(new PointD(150, 260));
        callout.Rotation = 30;

        callout.Tail.ShouldBe(new PointD(150, 260));
        callout.Center.ShouldBe(new PointD(200, 140));
        var tail = callout.TailGeometry.ShouldNotBeNull();
        tail.Tip.ShouldBe(callout.LocalTail);
        var world = RotationMath.Rotate(tail.Tip, callout.Center, 30);
        world.X.ShouldBe(150, 1e-9);
        world.Y.ShouldBe(260, 1e-9);
    }

    [Fact]
    public void Drehung_WirdNormalisiert()
    {
        var callout = Callout(new PointD(150, 260));

        callout.Rotation = -30;

        callout.Rotation.ShouldBe(330);
        callout.SignedRotation.ShouldBe(-30);
    }

    [Fact]
    public void Drehung_TrefferpruefungUndGrenzenFolgenDemGedrehtenKoerper()
    {
        // Zielpunkt in der Mitte: keine Spitze, nur der Körper (200 × 80 um (200, 140)).
        var callout = Callout(new PointD(200, 140));
        callout.Rotation = 90;

        callout.HitTest(new PointD(200, 60), 0).ShouldBeTrue();
        callout.HitTest(new PointD(120, 140), 0).ShouldBeFalse();
        var bounds = callout.Bounds;
        bounds.X.ShouldBe(160, 1e-9);
        bounds.Y.ShouldBe(40, 1e-9);
        bounds.Width.ShouldBe(80, 1e-9);
        bounds.Height.ShouldBe(200, 1e-9);
    }

    [Fact]
    public void Drehung_GrenzenEnthaltenDenZielpunkt()
    {
        var callout = Callout(new PointD(150, 400));
        callout.Rotation = 45;

        callout.Bounds.Bottom.ShouldBe(400, 1e-9);
        callout.Bounds.X.ShouldBeLessThanOrEqualTo(150);
        callout.HitTest(new PointD(150, 400), 2).ShouldBeTrue();
    }

    [Fact]
    public void BiegungUmkehren_SpiegeltKontrollpunkteAnDerGeradenSpitze()
    {
        var normal = Callout(new PointD(150, 260));
        normal.TailCurved = true;
        var reversed = Callout(new PointD(150, 260));
        reversed.TailCurved = true;
        reversed.TailCurveReversed = true;

        var a = normal.TailGeometry.ShouldNotBeNull();
        var b = reversed.TailGeometry.ShouldNotBeNull();
        var c1 = a.ControlStart.ShouldNotBeNull();
        var c2 = b.ControlStart.ShouldNotBeNull();
        c1.ShouldNotBe(c2);

        // Beide Kontrollpunkte liegen symmetrisch zur Mitte der geraden Seite.
        var mid = new PointD((a.BaseStart.X + a.Tip.X) / 2, (a.BaseStart.Y + a.Tip.Y) / 2);
        ((c1.X + c2.X) / 2).ShouldBe(mid.X, 1e-9);
        ((c1.Y + c2.Y) / 2).ShouldBe(mid.Y, 1e-9);
    }

    [Fact]
    public void BiegungUmkehren_OhneBiegung_KeineWirkung()
    {
        var callout = Callout(new PointD(150, 260));
        callout.TailCurveReversed = true;

        callout.TailGeometry.ShouldNotBeNull().ControlStart.ShouldBeNull();
    }

    [Fact]
    public void Gedreht_FitHeightToText_OberkanteBleibtStehen()
    {
        var callout = Callout(new PointD(150, 400));
        callout.Rect = new RectD(100, 100, 120, 30);
        callout.Text = "Ein langer Text, der in die kleine Blase nicht hineinpasst und umbrochen wird";
        callout.Rotation = 60;
        var topLeft = RotationMath.Rotate(callout.Rect.TopLeft, callout.Center, 60);
        var topRight = RotationMath.Rotate(new PointD(callout.Rect.Right, callout.Rect.Y), callout.Center, 60);

        callout.FitHeightToText().ShouldBeTrue();

        callout.Rect.Height.ShouldBeGreaterThan(30);
        callout.Rect.Width.ShouldBe(120, 1e-9);
        var newTopLeft = RotationMath.Rotate(callout.Rect.TopLeft, callout.Center, 60);
        var newTopRight = RotationMath.Rotate(new PointD(callout.Rect.Right, callout.Rect.Y), callout.Center, 60);
        newTopLeft.X.ShouldBe(topLeft.X, 1e-6);
        newTopLeft.Y.ShouldBe(topLeft.Y, 1e-6);
        newTopRight.X.ShouldBe(topRight.X, 1e-6);
        newTopRight.Y.ShouldBe(topRight.Y, 1e-6);
    }

    [Fact]
    public void Serializer_Roundtrip_DrehungUndBiegungsumkehr()
    {
        var original = Callout(new PointD(150, 260));
        original.TailCurved = true;
        original.TailCurveReversed = true;
        original.Rotation = 25;

        var json = AnnotationSerializer.Serialize([original]);
        var restored = AnnotationSerializer.Deserialize(json).ShouldHaveSingleItem().ShouldBeOfType<CalloutAnnotation>();

        restored.Rotation.ShouldBe(25);
        restored.TailCurveReversed.ShouldBeTrue();
        restored.Clone().ShouldBeOfType<CalloutAnnotation>().Rotation.ShouldBe(25);
    }
}
