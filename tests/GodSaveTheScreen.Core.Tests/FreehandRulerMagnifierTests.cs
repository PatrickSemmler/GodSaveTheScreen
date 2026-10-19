using System.Collections.Immutable;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Tests;

public sealed class StrokeAnnotationTests
{
    private static StrokeAnnotation Stroke(double thickness, params (double X, double Y)[] points) => new()
    {
        Stroke = new StrokeStyle(ArgbColor.Red, thickness),
        Points = [.. points.Select(p => new StrokePoint(p.X, p.Y))],
    };

    [Fact]
    public void Simplify_GeradeLinie_BleibtBeiZweiPunkten()
    {
        var points = Enumerable.Range(0, 50).Select(i => new StrokePoint(i, i * 0.5)).ToList();

        var reduced = StrokeSimplifier.Reduce(points);

        reduced.Length.ShouldBe(2);
        reduced[0].Point.ShouldBe(new PointD(0, 0));
        reduced[^1].Point.ShouldBe(new PointD(49, 24.5));
    }

    [Fact]
    public void Simplify_BehaeltEckpunkte()
    {
        var points = new List<StrokePoint>();
        for (var i = 0; i <= 20; i++)
        {
            points.Add(new StrokePoint(i, 0));
        }

        for (var i = 1; i <= 20; i++)
        {
            points.Add(new StrokePoint(20, i));
        }

        var reduced = StrokeSimplifier.Reduce(points);

        reduced.Length.ShouldBe(3);
        reduced[1].Point.ShouldBe(new PointD(20, 0));
    }

    [Fact]
    public void Simplify_BehaeltDruckwerteDerStuetzpunkte()
    {
        var points = new List<StrokePoint> { new(0, 0, 0.2), new(5, 10, 0.9), new(10, 0, 0.4) };

        var reduced = StrokeSimplifier.Reduce(points);

        reduced.Select(p => p.Pressure).ShouldBe([0.2, 0.9, 0.4]);
    }

    [Fact]
    public void Bounds_UmfasstAllePunkte_VisualMarginDieHalbeDicke()
    {
        var stroke = Stroke(10, (10, 20), (50, 5), (30, 40));

        stroke.Bounds.ShouldBe(new RectD(10, 5, 40, 35));
        stroke.VisualMargin.ShouldBe(6);
    }

    [Fact]
    public void HitTest_AbstandZumSegment_BisHalbeDickePlusToleranz()
    {
        var stroke = Stroke(10, (0, 0), (100, 0));

        stroke.HitTest(new PointD(50, 4), 0).ShouldBeTrue();
        stroke.HitTest(new PointD(50, 6), 0).ShouldBeFalse();
        stroke.HitTest(new PointD(50, 8), 3).ShouldBeTrue();
        stroke.HitTest(new PointD(120, 0), 5).ShouldBeFalse();
    }

    [Fact]
    public void HitTest_EinzelnerPunkt_IstEinKreis()
    {
        var stroke = Stroke(10, (20, 20));

        stroke.HitTest(new PointD(23, 20), 0).ShouldBeTrue();
        stroke.HitTest(new PointD(30, 20), 0).ShouldBeFalse();
    }

    [Fact]
    public void Translate_VerschiebtAllePunkteUndBehaeltDruck()
    {
        var stroke = new StrokeAnnotation { Points = [new StrokePoint(1, 2, 0.5), new StrokePoint(3, 4, 0.7)] };

        stroke.Translate(new VectorD(10, 20));

        stroke.Points.Select(p => (p.X, p.Y, p.Pressure)).ShouldBe([(11.0, 22.0, 0.5), (13.0, 24.0, 0.7)]);
    }

    [Fact]
    public void ScaleSizes_SkaliertDieDicke()
    {
        var stroke = Stroke(8, (0, 0), (10, 10));

        stroke.ScaleSizes(2);

        stroke.Stroke.Thickness.ShouldBe(16);
    }

    [Fact]
    public void HasPressure_NurMitDruckwertenUnterEins()
    {
        var stroke = Stroke(4, (0, 0), (10, 10));
        stroke.HasPressure.ShouldBeFalse();

        stroke.Points = [new StrokePoint(0, 0, 0.3), new StrokePoint(5, 5, 0.8)];
        stroke.HasPressure.ShouldBeTrue();

        stroke.UsePressure = false;
        stroke.HasPressure.ShouldBeFalse();
    }

    [Fact]
    public void Serializer_Roundtrip_StrichBlurUndLupe()
    {
        var originals = new Annotation[]
        {
            new StrokeAnnotation
            {
                Points = [new StrokePoint(1, 2, 0.5), new StrokePoint(30, 40)],
                IsHighlighter = true,
                Smoothing = 0.25,
                UsePressure = false,
                Stroke = new StrokeStyle(ArgbColor.FromRgb(255, 235, 59), 16, Cap: StrokeCapKind.Square),
                Opacity = 0.4,
            },
            new BlurAnnotation
            {
                Rect = new RectD(5, 6, 70, 80),
                Mode = BlurMode.GaussianBlur,
                Strength = 22,
                Shape = RegionShape.Ellipse,
                FeatherEdge = 6,
                RedactColor = ArgbColor.White,
            },
            new MagnifierAnnotation
            {
                SourceRect = new RectD(10, 10, 40, 30),
                TargetCenter = new PointD(120, 90),
                Zoom = 3,
                Shape = MagnifierShape.Rectangle,
                ShowConnector = false,
                BaseImageOnly = true,
                SmoothScaling = false,
            },
        };

        var restored = AnnotationSerializer.Deserialize(AnnotationSerializer.Serialize(originals));

        restored.Count.ShouldBe(3);
        var stroke = restored[0].ShouldBeOfType<StrokeAnnotation>();
        stroke.Points.ShouldBe(((StrokeAnnotation)originals[0]).Points);
        stroke.IsHighlighter.ShouldBeTrue();
        stroke.Smoothing.ShouldBe(0.25);
        stroke.UsePressure.ShouldBeFalse();
        stroke.Stroke.ShouldBe(((StrokeAnnotation)originals[0]).Stroke);
        stroke.Opacity.ShouldBe(0.4);

        var blur = restored[1].ShouldBeOfType<BlurAnnotation>();
        blur.Rect.ShouldBe(new RectD(5, 6, 70, 80));
        blur.Mode.ShouldBe(BlurMode.GaussianBlur);
        blur.Strength.ShouldBe(22);
        blur.Shape.ShouldBe(RegionShape.Ellipse);
        blur.FeatherEdge.ShouldBe(6);
        blur.RedactColor.ShouldBe(ArgbColor.White);

        var magnifier = restored[2].ShouldBeOfType<MagnifierAnnotation>();
        magnifier.SourceRect.ShouldBe(new RectD(10, 10, 40, 30));
        magnifier.TargetCenter.ShouldBe(new PointD(120, 90));
        magnifier.Zoom.ShouldBe(3);
        magnifier.Shape.ShouldBe(MagnifierShape.Rectangle);
        magnifier.ShowConnector.ShouldBeFalse();
        magnifier.BaseImageOnly.ShouldBeTrue();
        magnifier.SmoothScaling.ShouldBeFalse();
    }

    [Fact]
    public void Clone_KopiertPunkteUndOptionen()
    {
        var original = new StrokeAnnotation { Points = [new StrokePoint(1, 1, 0.5), new StrokePoint(2, 2)], IsHighlighter = true, Smoothing = 0.9 };

        var copy = (StrokeAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Points.ShouldBe(original.Points);
        copy.IsHighlighter.ShouldBeTrue();
        copy.Smoothing.ShouldBe(0.9);
    }
}

public sealed class RulerGeometryTests
{
    private static RulerPlacement Ruler(double angle) => new(new PointD(100, 100), angle, 400, 80);

    [Fact]
    public void Snap_Waagerecht_ObereKante_VersetztUmHalbeStrichbreiteNachAussen()
    {
        var snapped = RulerGeometry.Snap(new PointD(130, 55), Ruler(0), RulerSide.Top, 4);

        snapped.X.ShouldBe(130, 1e-9);
        snapped.Y.ShouldBe(58, 1e-9);
    }

    [Fact]
    public void Snap_Waagerecht_UntereKante()
    {
        var snapped = RulerGeometry.Snap(new PointD(130, 150), Ruler(0), RulerSide.Bottom, 4);

        snapped.X.ShouldBe(130, 1e-9);
        snapped.Y.ShouldBe(142, 1e-9);
    }

    [Fact]
    public void Snap_Senkrecht_ObereKanteLiegtLinks()
    {
        // 90°: Achse zeigt nach unten, die Normale (-1, 0) nach links; Top liegt auf der negativen Normalen, also rechts: x = 100 + 40 + 2.
        var snapped = RulerGeometry.Snap(new PointD(150, 80), Ruler(90), RulerSide.Top, 4);

        snapped.X.ShouldBe(142, 1e-9);
        snapped.Y.ShouldBe(80, 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(90)]
    [InlineData(-23.5)]
    public void Snap_AllePunkteLiegenAufEinerGeradenParallelZurKante(double angle)
    {
        var ruler = Ruler(angle);
        var points = new[] { new PointD(10, 20), new PointD(200, 300), new PointD(-50, 120), new PointD(333, 7) };

        var snapped = points.Select(p => RulerGeometry.Snap(p, ruler, RulerSide.Bottom, 6)).ToList();

        var distances = snapped.Select(p => RulerGeometry.SignedDistance(p, ruler)).ToList();
        foreach (var d in distances)
        {
            d.ShouldBe((ruler.Width / 2) + 3, 1e-9);
        }

        // Die Projektion verändert nur die Querkoordinate: die Position entlang der Achse bleibt erhalten.
        for (var i = 0; i < points.Length; i++)
        {
            RulerGeometry.AlongAxis(snapped[i], ruler).ShouldBe(RulerGeometry.AlongAxis(points[i], ruler), 1e-9);
        }
    }

    [Fact]
    public void NearestEdge_FindetKanteInnerhalbDerDistanz()
    {
        var ruler = Ruler(0);

        RulerGeometry.NearestEdge(new PointD(100, 55), ruler, 24).ShouldBe(RulerSide.Top);
        RulerGeometry.NearestEdge(new PointD(100, 150), ruler, 24).ShouldBe(RulerSide.Bottom);
        RulerGeometry.NearestEdge(new PointD(100, 100), ruler, 24).ShouldBeNull();
        RulerGeometry.NearestEdge(new PointD(100, 5), ruler, 24).ShouldBeNull();
        RulerGeometry.NearestEdge(new PointD(600, 55), ruler, 24).ShouldBeNull();
    }

    [Fact]
    public void Contains_PruefBandUndLaenge()
    {
        var ruler = Ruler(0);

        RulerGeometry.Contains(new PointD(100, 100), ruler).ShouldBeTrue();
        RulerGeometry.Contains(new PointD(100, 145), ruler).ShouldBeFalse();
        RulerGeometry.Contains(new PointD(310, 100), ruler).ShouldBeFalse();
    }
}

public sealed class MagnifierGeometryTests
{
    [Fact]
    public void TargetRect_IstQuellgroesseMalZoom_ZentriertAmZiel()
    {
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(10, 10, 40, 20),
            TargetCenter = new PointD(200, 100),
            Zoom = 3,
        };

        magnifier.TargetRect.ShouldBe(new RectD(140, 70, 120, 60));
    }

    [Fact]
    public void Bounds_VereintQuelleUndZiel()
    {
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(10, 10, 40, 20),
            TargetCenter = new PointD(200, 100),
            Zoom = 2,
        };

        magnifier.Bounds.ShouldBe(new RectD(10, 10, 230, 110));
    }

    [Fact]
    public void DefaultTargetCenter_LiegtRechtsUntenUndWirdImBildGeklemmt()
    {
        var source = new RectD(10, 10, 20, 20);

        var inside = MagnifierGeometry.DefaultTargetCenter(source, 2, 400, 300);
        inside.X.ShouldBeGreaterThan(30);
        inside.Y.ShouldBeGreaterThan(30);

        var clamped = MagnifierGeometry.DefaultTargetCenter(new RectD(360, 260, 30, 30), 3, 400, 300);
        (clamped.X + (30 * 3 / 2.0)).ShouldBeLessThanOrEqualTo(400);
        (clamped.Y + (30 * 3 / 2.0)).ShouldBeLessThanOrEqualTo(300);
    }

    [Fact]
    public void ConnectorTangents_BeruehrenBeideKreise()
    {
        var c1 = new PointD(0, 0);
        var c2 = new PointD(100, 30);

        var tangents = MagnifierGeometry.ConnectorTangents(c1, 10, c2, 25);

        tangents.ShouldNotBeNull();
        foreach (var (from, to) in new[] { tangents.Value.First, tangents.Value.Second })
        {
            from.DistanceTo(c1).ShouldBe(10, 1e-9);
            to.DistanceTo(c2).ShouldBe(25, 1e-9);

            // Tangente steht senkrecht auf dem Radius in beiden Berührpunkten.
            var direction = to - from;
            var r1 = from - c1;
            var r2 = to - c2;
            ((direction.X * r1.X) + (direction.Y * r1.Y)).ShouldBe(0, 1e-6);
            ((direction.X * r2.X) + (direction.Y * r2.Y)).ShouldBe(0, 1e-6);
        }
    }

    [Fact]
    public void ConnectorTangents_UmschlossenerKreis_LiefertNull()
    {
        MagnifierGeometry.ConnectorTangents(new PointD(0, 0), 50, new PointD(5, 5), 10).ShouldBeNull();
    }

    [Fact]
    public void HitTest_ZielUndQuellrahmen()
    {
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(10, 10, 40, 40),
            TargetCenter = new PointD(200, 100),
            Zoom = 2,
            Shape = MagnifierShape.Circle,
        };

        magnifier.HitTest(new PointD(200, 100), 0).ShouldBeTrue();
        magnifier.HitTest(new PointD(10, 30), 2).ShouldBeTrue();   // Quellrahmen
        magnifier.HitTest(new PointD(30, 30), 2).ShouldBeFalse();  // Quellinneres
        magnifier.HitTest(new PointD(160, 60), 0).ShouldBeFalse(); // Ecke des Zielrechtecks, außerhalb des Kreises
    }
}

public sealed class BlurAnnotationTests
{
    [Fact]
    public void HitTest_RechteckUndEllipse()
    {
        var rect = new BlurAnnotation { Rect = new RectD(10, 10, 100, 60) };
        rect.HitTest(new PointD(12, 12), 0).ShouldBeTrue();
        rect.HitTest(new PointD(200, 12), 0).ShouldBeFalse();

        var ellipse = new BlurAnnotation { Rect = new RectD(10, 10, 100, 60), Shape = RegionShape.Ellipse };
        ellipse.HitTest(new PointD(60, 40), 0).ShouldBeTrue();
        ellipse.HitTest(new PointD(12, 12), 0).ShouldBeFalse();
    }

    [Fact]
    public void MapPoints_SkaliertDasRechteck()
    {
        var blur = new BlurAnnotation { Rect = new RectD(10, 10, 20, 20) };

        blur.MapPoints(p => new PointD(p.X * 2, p.Y * 2));

        blur.Rect.ShouldBe(new RectD(20, 20, 40, 40));
    }
}

public sealed class ImmutableHelperTests
{
    [Fact]
    public void Points_SindStandardmaessigLeerUndNichtDefault()
    {
        var stroke = new StrokeAnnotation();

        stroke.Points.IsDefault.ShouldBeFalse();
        stroke.Points.ShouldBe(ImmutableArray<StrokePoint>.Empty);
        stroke.Bounds.ShouldBe(RectD.Empty);
    }
}
