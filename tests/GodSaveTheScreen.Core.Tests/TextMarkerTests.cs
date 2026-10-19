using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

public sealed class MarkerNumberingTests
{
    private static MarkerAnnotation Marker(int number, long created = 0) => new() { Number = number, Created = created };

    [Fact]
    public void NextNumber_OhneMarker_LiefertStartwert()
    {
        MarkerNumbering.NextNumber([]).ShouldBe(1);
        MarkerNumbering.NextNumber([new RectangleAnnotation()], start: 5).ShouldBe(5);
    }

    [Fact]
    public void NextNumber_NimmtGroessteNummerPlusEins_AuchBeiLuecken()
    {
        MarkerNumbering.NextNumber([Marker(1), Marker(2), Marker(5)]).ShouldBe(6);
    }

    [Fact]
    public void Renumber_ZOrder_VergibtLueckenloseNummernInListenreihenfolge()
    {
        var a = Marker(7);
        var b = Marker(2);
        var c = Marker(9);

        var result = MarkerNumbering.Renumber([a, new RectangleAnnotation(), b, c], MarkerOrder.ZOrder);

        result.ShouldBe([(a, 1), (b, 2), (c, 3)]);
    }

    [Fact]
    public void Renumber_Erstellreihenfolge_UndStartwert()
    {
        var first = Marker(5, created: 100);
        var second = Marker(1, created: 200);
        var third = Marker(3, created: 300);

        var result = MarkerNumbering.Renumber([third, first, second], MarkerOrder.Creation, start: 10);

        result.ShouldBe([(first, 10), (second, 11), (third, 12)]);
    }
}

public sealed class TextMarkerModelTests
{
    [Fact]
    public void Text_Clone_KopiertAlleSchriftwerteMitNeuerId()
    {
        var original = new TextAnnotation
        {
            Text = "Hallo\nWelt",
            Position = new PointD(5, 6),
            FontFamilyName = "Arial",
            FontWeight = 700,
            FontStyle = FontStyleKind.Italic,
            FontStretch = 3,
            FontSize = 31,
            Fill = ArgbColor.FromRgb(1, 2, 3),
            Stroke = new StrokeStyle(ArgbColor.White, 3),
            TextAlignment = TextAlignmentKind.Center,
            Background = new ArgbColor(200, 9, 9, 9),
            BackgroundPadding = 11,
            BackgroundCornerRadius = 2,
            Decorations = TextDecorationKind.Underline | TextDecorationKind.Strikethrough,
            WrapWidth = 150,
        };

        var copy = (TextAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Text.ShouldBe(original.Text);
        copy.FontFamilyName.ShouldBe("Arial");
        copy.FontWeight.ShouldBe(700);
        copy.FontStyle.ShouldBe(FontStyleKind.Italic);
        copy.FontStretch.ShouldBe(3);
        copy.FontSize.ShouldBe(31);
        copy.Stroke.ShouldBe(original.Stroke);
        copy.Decorations.ShouldBe(TextDecorationKind.Underline | TextDecorationKind.Strikethrough);
        copy.WrapWidth.ShouldBe(150);
        copy.Background.ShouldBe(original.Background);
    }

    [Fact]
    public void Marker_Clone_KopiertSpitzeUndFarben()
    {
        var original = new MarkerAnnotation
        {
            Center = new PointD(10, 20),
            Number = 4,
            Shape = MarkerShape.Rounded,
            Size = 40,
            PointerTarget = new PointD(80, 90),
            TextColor = ArgbColor.Black,
        };

        var copy = (MarkerAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Number.ShouldBe(4);
        copy.Shape.ShouldBe(MarkerShape.Rounded);
        copy.PointerTarget.ShouldBe(new PointD(80, 90));
        copy.TextColor.ShouldBe(ArgbColor.Black);
        copy.Created.ShouldBe(original.Created);
    }

    [Fact]
    public void Text_KonturVergroessertBoundsUmDieHalbeStaerke()
    {
        var text = new TextAnnotation { Text = "Test", Position = new PointD(100, 100), WrapWidth = 80, FontSize = 20 };
        var without = text.Bounds;

        text.Stroke = new StrokeStyle(ArgbColor.White, 6);

        text.Bounds.X.ShouldBe(without.X - 3, 1e-9);
        text.Bounds.Y.ShouldBe(without.Y - 3, 1e-9);
        text.Bounds.Width.ShouldBe(without.Width + 6, 1e-9);
        text.Bounds.Height.ShouldBe(without.Height + 6, 1e-9);
    }

    [Fact]
    public void Text_HintergrundFuegtInnenabstandHinzu_UndAenderungenMeldenBounds()
    {
        var text = new TextAnnotation { Text = "Test", Position = new PointD(50, 50), WrapWidth = 80, BackgroundPadding = 10 };
        var without = text.Bounds;
        var changed = new List<string?>();
        text.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        text.Background = ArgbColor.FromRgb(255, 255, 0);

        text.Bounds.Width.ShouldBe(without.Width + 20, 1e-9);
        changed.ShouldContain(nameof(TextAnnotation.Bounds));
    }

    [Fact]
    public void Text_LayoutWirdBeiTextaenderungNeuBerechnet()
    {
        var text = new TextAnnotation { Text = "A", FontSize = 20 };
        var oneLine = text.Bounds.Height;

        text.Text = "A\nB\nC";

        text.Bounds.Height.ShouldBeGreaterThan(oneLine * 2.5);
    }

    [Fact]
    public void Text_ScaleSizes_SkaliertSchriftUndUmbruch()
    {
        var text = new TextAnnotation { FontSize = 20, WrapWidth = 100, BackgroundPadding = 4 };

        text.ScaleSizes(2);

        text.FontSize.ShouldBe(40);
        text.WrapWidth.ShouldBe(200);
        text.BackgroundPadding.ShouldBe(8);
    }

    [Fact]
    public void Text_HitTest_ImBoundsRahmen()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(10, 10), WrapWidth = 100, FontSize = 20 };

        text.HitTest(new PointD(50, 20), 2).ShouldBeTrue();
        text.HitTest(new PointD(400, 400), 2).ShouldBeFalse();
    }

    [Fact]
    public void Marker_Bounds_UmfassenKoerperUndSpitze()
    {
        var marker = new MarkerAnnotation { Center = new PointD(50, 50), Size = 20 };
        marker.Bounds.ShouldBe(new RectD(40, 40, 20, 20));

        marker.PointerTarget = new PointD(100, 90);

        marker.Bounds.Right.ShouldBe(100);
        marker.Bounds.Bottom.ShouldBe(90);
    }

    [Fact]
    public void Marker_HitTest_KreisUndSpitze()
    {
        var marker = new MarkerAnnotation { Center = new PointD(50, 50), Size = 20, PointerTarget = new PointD(150, 50) };

        marker.HitTest(new PointD(55, 50), 2).ShouldBeTrue("im Kreis");
        marker.HitTest(new PointD(100, 50), 2).ShouldBeTrue("auf der Spitze");
        marker.HitTest(new PointD(100, 90), 2).ShouldBeFalse();
    }

    [Fact]
    public void Marker_MapPointsUndScaleSizes()
    {
        var marker = new MarkerAnnotation { Center = new PointD(10, 10), Size = 20, PointerTarget = new PointD(30, 10) };

        marker.MapPoints(p => new PointD(p.X * 2, p.Y * 2));
        marker.ScaleSizes(1.5);

        marker.Center.ShouldBe(new PointD(20, 20));
        marker.PointerTarget.ShouldBe(new PointD(60, 20));
        marker.Size.ShouldBe(30);
    }

    [Fact]
    public void Serializer_Roundtrip_TextUndMarker()
    {
        var text = new TextAnnotation { Text = "Zeile 1\nZeile 2", FontFamilyName = "Consolas", FontWeight = 700, WrapWidth = 120, Decorations = TextDecorationKind.Underline };
        var marker = new MarkerAnnotation { Center = new PointD(5, 6), Number = 12, PointerTarget = new PointD(50, 60), Shape = MarkerShape.Square };

        var json = AnnotationSerializer.Serialize([text, marker]);
        var restored = AnnotationSerializer.Deserialize(json);

        json.ShouldContain("\"type\":\"text\"");
        json.ShouldContain("\"type\":\"marker\"");
        var rt = restored[0].ShouldBeOfType<TextAnnotation>();
        rt.Text.ShouldBe("Zeile 1\nZeile 2");
        rt.FontFamilyName.ShouldBe("Consolas");
        rt.WrapWidth.ShouldBe(120);
        rt.Decorations.ShouldBe(TextDecorationKind.Underline);
        var rm = restored[1].ShouldBeOfType<MarkerAnnotation>();
        rm.Number.ShouldBe(12);
        rm.PointerTarget.ShouldBe(new PointD(50, 60));
        rm.Shape.ShouldBe(MarkerShape.Square);
        rm.Id.ShouldBe(marker.Id);
    }

    [Fact]
    public void Einstellungen_TextUndMarkerStandardwerte_HabenSinnvolleWerte()
    {
        var settings = new AppSettings();
        settings.TextDefaults.FontFamilyName.ShouldNotBeNullOrEmpty();
        settings.TextDefaults.FontSize.ShouldBeGreaterThan(0);
        settings.MarkerDefaults.Size.ShouldBeGreaterThan(0);
        settings.MarkerStartNumber.ShouldBe(1);
    }
}
