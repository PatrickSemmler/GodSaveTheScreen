using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Kontur von Linie/Pfeil: Darstellung, Werkzeug-Vorgaben, Eigenschaften-Panel und Vorlagen.</summary>
public sealed class LineOutlineTests
{
    private static readonly (byte, byte, byte, byte) White = (255, 255, 255, 255);
    private static readonly (byte, byte, byte, byte) Black = (0, 0, 0, 255);
    private static readonly (byte, byte, byte, byte) Red = (255, 0, 0, 255);

    /// <summary>Waagerechte schwarze Linie y = 40, Stärke 4 (y 38..42).</summary>
    private static LineAnnotation Line(double outline = 0, DashKind dash = DashKind.Solid) => new()
    {
        Start = new PointD(10, 40),
        End = new PointD(90, 40),
        Stroke = new StrokeStyle(ArgbColor.Black, 4, dash),
        Outline = new StrokeStyle(ArgbColor.Red, outline),
    };

    private static PixelBuffer Render(Annotation annotation)
    {
        var document = new ImageDocument(PixelTools.Solid(120, 80, 255, 255, 255));
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, annotation));
        return new DocumentExporter().Render(document);
    }

    private static int CountNonWhite(PixelBuffer buffer)
    {
        var count = 0;
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var (r, g, b, _) = PixelTools.Get(buffer, x, y);
                if (r < 250 || g < 250 || b < 250)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void Activate(EditorHarness h, ToolBase tool)
    {
        h.Context.Selection.Clear();
        h.ViewModel.Activate(tool);
    }

    [StaFact]
    public void Render_Kontur_LiegtUmDieLinie()
    {
        var without = Render(Line());
        var with = Render(Line(outline: 3));

        PixelTools.Get(without, 50, 43).ShouldBe(White);
        PixelTools.Get(with, 50, 40).ShouldBe(Black);
        PixelTools.Get(with, 50, 43).ShouldBe(Red);
        PixelTools.Get(with, 50, 36).ShouldBe(Red);
        PixelTools.Get(with, 50, 47).ShouldBe(White);
    }

    [StaFact]
    public void Render_Gestrichelt_KonturFolgtDenStrichen()
    {
        // Strich 4 × 4 = 16 px, Lücke 2 × 4 = 8 px: erster Strich 10..26, Lücke 26..34.
        var result = Render(Line(outline: 2, dash: DashKind.Dash));

        PixelTools.Get(result, 18, 40).ShouldBe(Black);
        PixelTools.Get(result, 18, 43).ShouldBe(Red);
        PixelTools.Get(result, 30, 40).ShouldBe(White);
        PixelTools.Get(result, 30, 43).ShouldBe(White);
    }

    [StaFact]
    public void Render_Pfeil_KonturUmfasstDieSpitze()
    {
        // Start weit genug innen, damit die Zeichenfläche nicht um den Rand der Spitze wächst (Koordinaten bleiben).
        var line = Line(outline: 3);
        line.Start = new PointD(30, 40);
        line.EndCap = LineEndStyle.Arrow;

        var result = Render(line);

        result.Width.ShouldBe(120);

        PixelTools.Get(result, 84, 40).ShouldBe(Black);
        PixelTools.Get(result, 91, 40).ShouldBe(Red);
    }

    [StaFact]
    public void Render_WeicheKontur_LaeuftAus_LinieBleibtScharf()
    {
        var sharp = Line(outline: 3);
        var soft = Line(outline: 3);
        soft.OutlineBlur = 6;

        var sharpResult = Render(sharp);
        var softResult = Render(soft);

        CountNonWhite(softResult).ShouldBeGreaterThan(CountNonWhite(sharpResult));
        PixelTools.Get(softResult, 50, 40).ShouldBe(Black);
        SoftLayerCache.CountFor(soft).ShouldBe(1);

        soft.OutlineBlur = 2;

        SoftLayerCache.CountFor(soft).ShouldBe(0);
    }

    [StaFact]
    public void Render_Schatten_SchliesstKonturEin()
    {
        var plain = Line();
        plain.Shadow = new ShadowStyle(ArgbColor.Black, Size: 10, Blur: 0);
        var outlined = Line(outline: 3);
        outlined.Shadow = plain.Shadow;

        // Schatten der Linie: y 48..52; mit Kontur: y 45..55.
        PixelTools.Get(Render(plain), 60, 54).ShouldBe(White);
        PixelTools.Get(Render(outlined), 60, 54).ShouldBe(Black);
    }

    [StaFact]
    public void Panel_PfeilWerkzeug_BearbeitetStandardkontur()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<ArrowTool>());
        var props = h.ViewModel.Properties;

        props.ShowCaps.ShouldBeTrue();
        props.OutlineColor = ArgbColor.Red;
        props.OutlineThickness = 3;
        props.OutlineBlur = 500;

        var style = h.Defaults.GetShape(ArrowTool.ToolId, new());
        style.Outline.ShouldBe(new StrokeStyle(ArgbColor.Red, 3));
        style.OutlineBlur.ShouldBe(TextAnnotation.MaxOutlineBlur);
        h.Defaults.GetShape(LineTool.ToolId, new()).Outline.ShouldBe(LineAnnotation.NoOutline);

        var tool = h.Tools.Get<ArrowTool>();
        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(90, 10));

        var line = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<LineAnnotation>();
        line.Outline.ShouldBe(new StrokeStyle(ArgbColor.Red, 3));
        line.OutlineBlur.ShouldBe(TextAnnotation.MaxOutlineBlur);
    }

    [StaFact]
    public void Panel_Auswahl_SetztKonturMitUndo()
    {
        var h = new EditorHarness();
        var line = Line();
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, line));
        h.Context.Selection.Set(line);
        var props = h.ViewModel.Properties;

        props.OutlineThickness.ShouldBe(0);
        props.OutlineThickness = 4;

        line.Outline.Thickness.ShouldBe(4);
        line.Outline.Color.ShouldBe(ArgbColor.Red);

        h.Document.Undo.Undo();

        line.Outline.Thickness.ShouldBe(0);
    }

    [StaFact]
    public void Panel_Rechteck_OhneKontur()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());

        h.ViewModel.Properties.ShowCaps.ShouldBeFalse();
    }

    [StaFact]
    public void Vorlage_UebernimmtKontur()
    {
        var h = new EditorHarness();
        var line = Line(outline: 2);
        line.OutlineBlur = 3;
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, line));
        h.Context.Selection.Set(line);

        var preset = Editor.PresetStyles.Capture(h.Context, ArrowTool.ToolId, new(), "x");

        preset.Shape!.Outline.ShouldBe(line.Outline);
        preset.Shape.OutlineBlur.ShouldBe(3);
    }
}
