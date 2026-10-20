using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

/// <summary>Schatten (Formen, Text, Marker, Emoji) und weiche Textkontur im Modell.</summary>
public sealed class ShadowTests
{
    private static RectangleAnnotation Box() => new() { Rect = new RectD(10, 10, 20, 20) };

    [Fact]
    public void VisualBounds_MitSchatten_WaechstNachRechtsUnten()
    {
        var box = Box();
        var without = box.VisualBounds;

        box.Shadow = new ShadowStyle(ArgbColor.Black, Size: 5, Blur: 3);

        // Rand 4 (Strich) → 6..34; Schatten um 5 versetzt und 3 weich → bis 42.
        without.ShouldBe(new RectD(6, 6, 28, 28));
        box.VisualBounds.ShouldBe(new RectD(6, 6, 36, 36));
    }

    [Fact]
    public void VisualBounds_TransparenterSchatten_BleibtUnveraendert()
    {
        var box = Box();
        var without = box.VisualBounds;

        box.Shadow = ShadowStyle.None with { Size = 50, Blur = 50 };

        box.VisualBounds.ShouldBe(without);
    }

    [Fact]
    public void Text_KonturUnschaerfe_VergroessertNurVisualMargin()
    {
        var text = new TextAnnotation { Text = "A", Stroke = new StrokeStyle(ArgbColor.White, 4) };
        var bounds = text.Bounds;
        var margin = text.VisualMargin;

        text.OutlineBlur = 6;

        text.Bounds.ShouldBe(bounds);
        text.VisualMargin.ShouldBe(margin + 6);
    }

    [Fact]
    public void Text_KonturUnschaerfeOhneKontur_OhneWirkung()
    {
        var text = new TextAnnotation { Text = "A", OutlineBlur = 6 };

        text.VisualMargin.ShouldBe(1);
    }

    [Fact]
    public void Serializer_Roundtrip_SchattenUndKonturUnschaerfe()
    {
        var shadow = new ShadowStyle(new ArgbColor(128, 10, 20, 30), 7, 9);
        Annotation[] originals =
        [
            new RectangleAnnotation { Rect = new RectD(1, 2, 3, 4), Shadow = shadow },
            new LineAnnotation { Start = new PointD(0, 0), End = new PointD(10, 10), Shadow = shadow },
            new TextAnnotation { Text = "Hallo", Stroke = new StrokeStyle(ArgbColor.White, 3), OutlineBlur = 4.5, Shadow = shadow },
            new MarkerAnnotation { Center = new PointD(5, 5), Shadow = shadow },
            new EmojiAnnotation { Rect = new RectD(0, 0, 32, 32), Shadow = shadow },
        ];

        var restored = AnnotationSerializer.Deserialize(AnnotationSerializer.Serialize(originals));

        restored.Count.ShouldBe(originals.Length);
        restored.Cast<IShadowAnnotation>().ShouldAllBe(a => a.Shadow == shadow);
        restored.OfType<TextAnnotation>().Single().OutlineBlur.ShouldBe(4.5);
    }

    [Fact]
    public void Serializer_AlteDatenOhneSchatten_KeinSchatten()
    {
        var json = AnnotationSerializer.Serialize([Box(), new TextAnnotation { Text = "A" }]);
        var array = JsonNode.Parse(json)!.AsArray();
        foreach (var item in array.OfType<JsonObject>())
        {
            foreach (var key in item.Select(p => p.Key).Where(k => k.Equals("shadow", StringComparison.OrdinalIgnoreCase) || k.Equals("outlineBlur", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                item.Remove(key);
            }
        }

        var restored = AnnotationSerializer.Deserialize(array.ToJsonString());

        restored.Cast<IShadowAnnotation>().ShouldAllBe(a => a.Shadow == ShadowStyle.None);
        restored.OfType<TextAnnotation>().Single().OutlineBlur.ShouldBe(0);
    }

    [Fact]
    public void Clone_UebernimmtSchattenUndKonturUnschaerfe()
    {
        var text = new TextAnnotation { Text = "A", OutlineBlur = 3, Shadow = new ShadowStyle(ArgbColor.Black) };

        var copy = (TextAnnotation)text.Clone();

        copy.OutlineBlur.ShouldBe(3);
        copy.Shadow.ShouldBe(text.Shadow);
    }

    [Fact]
    public void Settings_Normalized_ErsetztFehlendenSchatten()
    {
        var settings = new AppSettings
        {
            TextDefaults = new TextStyle { Shadow = null! },
            MarkerDefaults = new MarkerStyle { Shadow = null! },
            EmojiDefaults = new EmojiStyle { Shadow = null! },
            ShapeDefaults = new ShapeDefaultsMap { ["rectangle"] = new ShapeStyle { Shadow = null! } },
        };

        var normalized = settings.Normalized();

        normalized.TextDefaults.Shadow.ShouldBe(ShadowStyle.None);
        normalized.MarkerDefaults.Shadow.ShouldBe(ShadowStyle.None);
        normalized.EmojiDefaults.Shadow.ShouldBe(ShadowStyle.None);
        normalized.ShapeDefaults["rectangle"].Shadow.ShouldBe(ShadowStyle.None);
    }

    [Fact]
    public void Settings_StandardOhneSchatten()
    {
        var settings = new AppSettings();

        settings.TextDefaults.Shadow.IsVisible.ShouldBeFalse();
        settings.TextDefaults.OutlineBlur.ShouldBe(0);
        settings.MarkerDefaults.Shadow.IsVisible.ShouldBeFalse();
        new ShapeStyle().Shadow.ShouldBe(ShadowStyle.None);
    }
}
