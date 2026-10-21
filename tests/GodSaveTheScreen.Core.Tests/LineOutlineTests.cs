using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

/// <summary>Kontur von Linie/Pfeil im Modell und im Projektformat.</summary>
public sealed class LineOutlineTests
{
    private static LineAnnotation Line() => new() { Start = new PointD(10, 10), End = new PointD(90, 10), Stroke = new StrokeStyle(ArgbColor.Black, 4) };

    [Fact]
    public void Standard_KeineKontur()
    {
        var line = Line();

        line.Outline.ShouldBe(LineAnnotation.NoOutline);
        line.Outline.IsVisible.ShouldBeFalse();
        new ShapeStyle().Outline.ShouldBe(LineAnnotation.NoOutline);
    }

    [Fact]
    public void VisualMargin_WaechstUmKonturUndUnschaerfe()
    {
        var line = Line();
        var margin = line.VisualMargin;

        line.OutlineBlur = 5;
        line.VisualMargin.ShouldBe(margin, "ohne Kontur wirkt die Unschärfe nicht");

        line.Outline = new StrokeStyle(ArgbColor.White, 3);
        line.VisualMargin.ShouldBe(margin + 3 + 5);
    }

    [Fact]
    public void Serializer_Roundtrip_KonturUndUnschaerfe()
    {
        var line = Line();
        line.Outline = new StrokeStyle(ArgbColor.Red, 2.5);
        line.OutlineBlur = 4;

        var restored = AnnotationSerializer.Deserialize(AnnotationSerializer.Serialize([line])).Single().ShouldBeOfType<LineAnnotation>();

        restored.Outline.ShouldBe(line.Outline);
        restored.OutlineBlur.ShouldBe(4);
    }

    [Fact]
    public void Serializer_OhneKontur_SchreibtKeineFelder()
    {
        var json = JsonNode.Parse(AnnotationSerializer.Serialize([Line()]))!.AsArray().Single()!.AsObject();

        json.ContainsKey("Outline").ShouldBeFalse();
        json.ContainsKey("OutlineBlur").ShouldBeFalse();
    }

    [Fact]
    public void Serializer_AlteDatenOhneKontur_KeineKontur()
    {
        var restored = AnnotationSerializer.Deserialize(AnnotationSerializer.Serialize([Line()])).Single().ShouldBeOfType<LineAnnotation>();

        restored.Outline.ShouldBe(LineAnnotation.NoOutline);
        restored.OutlineBlur.ShouldBe(0);
    }

    [Fact]
    public void CopyFrom_UebernimmtKontur()
    {
        var line = Line();
        line.Outline = new StrokeStyle(ArgbColor.Red, 2);
        line.OutlineBlur = 3;

        var copy = AnnotationSerializer.DeserializeAsCopies(AnnotationSerializer.Serialize([line])).Single().ShouldBeOfType<LineAnnotation>();

        copy.Outline.ShouldBe(line.Outline);
        copy.OutlineBlur.ShouldBe(3);
    }
}
