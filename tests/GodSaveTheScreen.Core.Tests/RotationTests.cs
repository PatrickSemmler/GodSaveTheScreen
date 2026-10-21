using System.Collections.Immutable;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Tests;

/// <summary>Drehung von Formen, Polygonen, Freihandstrichen und Text im Modell.</summary>
public sealed class RotationTests
{
    private static void ShouldBeNear(PointD actual, PointD expected)
    {
        actual.X.ShouldBe(expected.X, 1e-6);
        actual.Y.ShouldBe(expected.Y, 1e-6);
    }

    [Fact]
    public void Rechteck_Gedreht_BoundsIstHuelle_RectBleibt()
    {
        var box = new RectangleAnnotation { Rect = new RectD(0, 0, 40, 20), Rotation = 90 };

        box.Rect.ShouldBe(new RectD(0, 0, 40, 20));
        var b = box.Bounds;
        b.X.ShouldBe(10, 1e-9);
        b.Y.ShouldBe(-10, 1e-9);
        b.Width.ShouldBe(20, 1e-9);
        b.Height.ShouldBe(40, 1e-9);
    }

    [Fact]
    public void Drehung_WirdNormalisiert()
    {
        var box = new EllipseAnnotation { Rotation = -30 };

        box.Rotation.ShouldBe(330);
    }

    [Fact]
    public void Rechteck_Gedreht_TrefferImGedrehtenRahmen()
    {
        var box = new RectangleAnnotation { Rect = new RectD(0, 0, 40, 20), Fill = ArgbColor.Black, Rotation = 90 };

        // Mitte (20, 10); gedreht reicht der Rahmen senkrecht von −10 bis 30, waagerecht von 10 bis 30.
        box.HitTest(new PointD(20, -5), 0).ShouldBeTrue();
        box.HitTest(new PointD(2, 10), 0).ShouldBeFalse();
    }

    [Fact]
    public void Ellipse_UndDreieck_Gedreht_TrefferImGedrehtenSystem()
    {
        var ellipse = new EllipseAnnotation { Rect = new RectD(0, 0, 40, 10), Fill = ArgbColor.Black, Rotation = 90 };
        var triangle = new TriangleAnnotation { Rect = new RectD(0, 0, 40, 40), Fill = ArgbColor.Black, Rotation = 180 };

        ellipse.HitTest(new PointD(20, 22), 0).ShouldBeTrue();
        ellipse.HitTest(new PointD(35, 5), 0).ShouldBeFalse();

        // Gleichschenklig, Spitze oben; um 180° gedreht zeigt die Spitze nach unten.
        triangle.HitTest(new PointD(20, 38), 0).ShouldBeTrue();
        triangle.HitTest(new PointD(3, 3), 0).ShouldBeTrue();
        triangle.HitTest(new PointD(3, 37), 0).ShouldBeFalse();
    }

    [Fact]
    public void Polygon_Gedreht_BoundsUmschliesstGedrehtePunkte()
    {
        var polygon = new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(40, 0), new PointD(40, 20)], Rotation = 90 };

        polygon.Center.ShouldBe(new PointD(20, 10));
        var world = polygon.WorldPoints.ToList();
        ShouldBeNear(world[0], new PointD(30, -10));
        ShouldBeNear(world[2], new PointD(10, 30));
        polygon.Bounds.Width.ShouldBe(20, 1e-9);
        polygon.Bounds.Height.ShouldBe(40, 1e-9);
        polygon.HitTest(new PointD(30, -10), 1).ShouldBeTrue();
        polygon.HitTest(new PointD(0, 0), 1).ShouldBeFalse();
    }

    [Fact]
    public void Strich_Gedreht_TrefferUndBounds()
    {
        var stroke = new StrokeAnnotation
        {
            Points = ImmutableArray.Create(new StrokePoint(0, 0), new StrokePoint(40, 0)),
            Stroke = new StrokeStyle(ArgbColor.Black, 2),
            Rotation = 90,
        };

        stroke.Bounds.X.ShouldBe(20, 1e-9);
        stroke.Bounds.Height.ShouldBe(40, 1e-9);
        stroke.HitTest(new PointD(20, 15), 0).ShouldBeTrue();
        stroke.HitTest(new PointD(5, 0), 0).ShouldBeFalse();
    }

    [Fact]
    public void Text_Gedreht_RectBleibt_BoundsIstHuelle_Treffer()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100) };
        var rect = text.Rect;

        text.Rotation = 90;

        text.Rect.ShouldBe(rect);
        text.Bounds.Width.ShouldBe(rect.Height, 1e-9);
        text.Bounds.Height.ShouldBe(rect.Width, 1e-9);
        var outside = new PointD(rect.Right - 1, rect.Y + 1);
        text.HitTest(outside, 0).ShouldBeFalse();
        text.HitTest(text.Center, 0).ShouldBeTrue();
    }

    [Fact]
    public void VisualMargin_WaechstMitDrehung()
    {
        var box = new RectangleAnnotation { Rect = new RectD(0, 0, 40, 20), Stroke = new StrokeStyle(ArgbColor.Black, 4) };
        var straight = box.VisualMargin;

        box.Rotation = 45;

        box.VisualMargin.ShouldBe(straight * Math.Sqrt(2), 1e-9);
    }

    [Fact]
    public void Serialisierung_DrehungNurWennGesetzt_UndRundreise()
    {
        var straight = new RectangleAnnotation { Rect = new RectD(0, 0, 10, 10) };
        var rotated = new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(10, 0), new PointD(0, 10)], Rotation = 30 };
        var text = new TextAnnotation { Text = "A", Rotation = 200 };
        var stroke = new StrokeAnnotation { Points = ImmutableArray.Create(new StrokePoint(0, 0), new StrokePoint(5, 5)), Rotation = 15 };

        AnnotationSerializer.Serialize([straight]).ShouldNotContain("Rotation", Case.Insensitive);
        var restored = AnnotationSerializer.Deserialize(AnnotationSerializer.Serialize([straight, rotated, text, stroke]));

        ((RectangleAnnotation)restored[0]).Rotation.ShouldBe(0);
        ((PolygonAnnotation)restored[1]).Rotation.ShouldBe(30);
        ((TextAnnotation)restored[2]).Rotation.ShouldBe(200);
        ((StrokeAnnotation)restored[3]).Rotation.ShouldBe(15);
    }

    [Fact]
    public void Clone_UebernimmtDrehung()
    {
        var box = new TriangleAnnotation { Rect = new RectD(0, 0, 10, 10), Rotation = 45 };

        ((TriangleAnnotation)box.Clone()).Rotation.ShouldBe(45);
    }

    [Fact]
    public void PreserveFrame_TextWaechst_LinkeObereEckeBleibtImBild()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 30 };
        var corner = text.ToWorld(text.Rect.TopLeft);

        RotationMath.PreserveFrame(text, () => text.Text = "Hallo Welt\nzweite Zeile");

        ShouldBeNear(text.ToWorld(text.Rect.TopLeft), corner);
    }

    [Fact]
    public void PreserveFrame_HinUndZurueck_ErgibtAusgangslage()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 30 };

        RotationMath.PreserveFrame(text, () => text.FontSize = 60);
        RotationMath.PreserveFrame(text, () => text.FontSize = 24);

        ShouldBeNear(text.Position, new PointD(100, 100));
    }

    [Fact]
    public void PreserveFrame_OhneDrehung_FuehrtNurAus()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100) };

        RotationMath.PreserveFrame(text, () => text.Text = "Hallo Welt");

        text.Position.ShouldBe(new PointD(100, 100));
    }

    [Fact]
    public void Realign_PolygonPunktVerschoben_UebrigePunkteBleibenImBild()
    {
        var polygon = new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(40, 0), new PointD(40, 20)], Rotation = 60 };
        var center = polygon.Center;
        var fixedWorld = polygon.WorldPoints.Take(2).ToList();
        var target = new PointD(90, 70);

        polygon.Points = polygon.Points.SetItem(2, polygon.ToLocal(target));
        RotationMath.Realign(polygon, center);

        var world = polygon.WorldPoints.ToList();
        ShouldBeNear(world[0], fixedWorld[0]);
        ShouldBeNear(world[1], fixedWorld[1]);
        ShouldBeNear(world[2], target);
    }
}
