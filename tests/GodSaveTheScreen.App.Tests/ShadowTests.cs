using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Schatten und weiche Textkontur: Darstellung, Cache und Eigenschaften-Panel.</summary>
public sealed class ShadowTests
{
    private static ImageDocument WhiteDocument() => new(PixelTools.Solid(100, 80, 255, 255, 255));

    private static RectangleAnnotation BlueBox(ShadowStyle? shadow = null) => new()
    {
        Rect = new RectD(10, 10, 30, 20),
        Stroke = new StrokeStyle(ArgbColor.Red, 0),
        Fill = ArgbColor.FromRgb(0, 0, 255),
        Shadow = shadow ?? ShadowStyle.None,
    };

    private static PixelBuffer Render(params Annotation[] annotations)
    {
        var document = WhiteDocument();
        foreach (var annotation in annotations)
        {
            document.Undo.Execute(new AddAnnotationAction(document.Annotations, annotation));
        }

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
    public void Render_HarterSchatten_LiegtVersetztHinterDemObjekt()
    {
        var result = Render(BlueBox(new ShadowStyle(ArgbColor.Black, Size: 10, Blur: 0)));

        // Objekt 10..40 × 10..30, Schatten 20..50 × 20..40.
        PixelTools.Get(result, 15, 15).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 35, 25).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 45, 35).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 55, 35).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        PixelTools.Get(result, 15, 35).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_OhneSchattenfarbe_KeinSchatten()
    {
        var result = Render(BlueBox(ShadowStyle.None with { Size = 10, Blur = 0 }));

        PixelTools.Get(result, 45, 35).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_WeicherSchatten_LaeuftAus()
    {
        var hard = Render(BlueBox(new ShadowStyle(ArgbColor.Black, Size: 10, Blur: 0)));
        var soft = Render(BlueBox(new ShadowStyle(ArgbColor.Black, Size: 10, Blur: 8)));

        // Jenseits der harten Kante (x = 50) ist der weiche Schatten noch sichtbar, aber heller als in der Mitte.
        PixelTools.Get(hard, 53, 30).R.ShouldBe((byte)255);
        var edge = PixelTools.Get(soft, 53, 30).R;
        edge.ShouldBeLessThan((byte)250);
        edge.ShouldBeGreaterThan(PixelTools.Get(soft, 45, 35).R);
    }

    [StaFact]
    public void Render_HalbtransparenteSchattenfarbe_IstHalbDeckend()
    {
        var result = Render(BlueBox(new ShadowStyle(new ArgbColor(128, 0, 0, 0), Size: 10, Blur: 0)));

        PixelTools.Get(result, 45, 35).R.ShouldBeInRange((byte)120, (byte)135);
    }

    [StaFact]
    public void Render_TextMitWeicherKontur_BleibtScharfUndStrahltAus()
    {
        TextAnnotation Text(double blur) => new()
        {
            Text = "H",
            Position = new PointD(20, 10),
            FontSize = 40,
            Fill = ArgbColor.FromRgb(0, 0, 255),
            Stroke = new StrokeStyle(ArgbColor.Black, 2),
            OutlineBlur = blur,
        };

        var sharp = Render(Text(0));
        var soft = Render(Text(6));

        CountNonWhite(soft).ShouldBeGreaterThan(CountNonWhite(sharp));

        // Die Füllung bleibt deckend blau (Mitte des linken H-Balkens ist in beiden gleich).
        var blueSharp = Enumerable.Range(0, sharp.Height).SelectMany(y => Enumerable.Range(0, sharp.Width).Select(x => (x, y)))
            .Count(p => PixelTools.Get(sharp, p.x, p.y) == ((byte)0, (byte)0, (byte)255, (byte)255));
        var blueSoft = Enumerable.Range(0, soft.Height).SelectMany(y => Enumerable.Range(0, soft.Width).Select(x => (x, y)))
            .Count(p => PixelTools.Get(soft, p.x, p.y) == ((byte)0, (byte)0, (byte)255, (byte)255));
        blueSharp.ShouldBeGreaterThan(0);
        blueSoft.ShouldBe(blueSharp);
    }

    [StaFact]
    public void Cache_WirdBeiEigenschaftsaenderungVerworfen()
    {
        var box = BlueBox(new ShadowStyle(ArgbColor.Black, 5, 4));
        Render(box);
        SoftLayerCache.CountFor(box).ShouldBe(1);

        box.Shadow = box.Shadow with { Blur = 2 };

        SoftLayerCache.CountFor(box).ShouldBe(0);
    }

    [StaFact]
    public void Panel_RechteckWerkzeug_BearbeitetStandardschatten()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        var shadow = h.ViewModel.ShadowProperties;

        shadow.IsApplicable.ShouldBeTrue();
        shadow.ShadowColor = ArgbColor.Black;
        shadow.ShadowSize = 9;
        shadow.ShadowBlur = 500;

        var style = h.Defaults.GetShape(RectangleTool.ToolId, new()).Shadow;
        style.ShouldBe(new ShadowStyle(ArgbColor.Black, 9, ShadowStyle.MaxBlur));
        h.Defaults.GetShape(EllipseTool.ToolId, new()).Shadow.IsVisible.ShouldBeFalse();
    }

    [StaFact]
    public void Panel_FreihandWerkzeuge_OhneSchatten()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<PenTool>());

        h.ViewModel.ShadowProperties.IsApplicable.ShouldBeFalse();
    }

    [StaFact]
    public void Panel_TextMarkerEmoji_SpeichernEigeneStandardwerte()
    {
        var h = new EditorHarness();
        var black = new ShadowStyle(ArgbColor.Black);

        Activate(h, h.Tools.Get<TextTool>());
        h.ViewModel.ShadowProperties.ShadowColor = ArgbColor.Black;
        Activate(h, h.Tools.Get<MarkerTool>());
        h.ViewModel.ShadowProperties.ShadowSize = 2;
        Activate(h, h.Tools.Get<EmojiTool>());
        h.ViewModel.ShadowProperties.ShadowColor = ArgbColor.Red;

        h.Defaults.GetText().Shadow.ShouldBe(black);
        h.Defaults.GetMarker().Shadow.ShouldBe(ShadowStyle.None with { Size = 2 });
        h.Context.Emoji.Create(new RectD(0, 0, 32, 32)).Shadow.Color.ShouldBe(ArgbColor.Red);
    }

    [StaFact]
    public void Panel_Auswahl_SetztSchattenAllerObjekteInEinemSchritt()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 30, 20);
        var text = new TextAnnotation { Text = "A", Position = new PointD(60, 60) };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set([rect, text]);
        var shadow = h.ViewModel.ShadowProperties;

        shadow.ShadowColor = ArgbColor.Black;

        rect.Shadow.Color.ShouldBe(ArgbColor.Black);
        text.Shadow.Color.ShouldBe(ArgbColor.Black);
        shadow.ShadowColor.ShouldBe(ArgbColor.Black);

        h.Document.Undo.Undo();

        rect.Shadow.ShouldBe(ShadowStyle.None);
        text.Shadow.ShouldBe(ShadowStyle.None);
    }

    [StaFact]
    public void Panel_GemischteWerte_SindNull()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(10, 10, 30, 20);
        var b = h.AddRectangle(50, 10, 30, 20);
        a.Shadow = new ShadowStyle(ArgbColor.Black, 3);
        h.Context.Selection.Set([a, b]);

        h.ViewModel.ShadowProperties.ShadowColor.ShouldBeNull();
        h.ViewModel.ShadowProperties.ShadowSize.ShouldBeNull();
        h.ViewModel.ShadowProperties.ShadowBlur.ShouldBe(6);
    }

    [StaFact]
    public void TextPanel_KonturUnschaerfe_AnAuswahlUndStandard()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<TextTool>());
        h.ViewModel.TextProperties.OutlineBlur = 3;
        h.Defaults.GetText().OutlineBlur.ShouldBe(3);

        var text = new TextAnnotation { Text = "A" };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        h.ViewModel.TextProperties.OutlineBlur = 99;

        text.OutlineBlur.ShouldBe(TextAnnotation.MaxOutlineBlur);
    }

    [StaFact]
    public void Vorlage_UebernimmtSchatten()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 30, 20);
        rect.Shadow = new ShadowStyle(ArgbColor.Black, 8, 2);
        h.Context.Selection.Set(rect);

        var preset = Editor.PresetStyles.Capture(h.Context, RectangleTool.ToolId, new(), "x");

        preset.Shape!.Shadow.ShouldBe(rect.Shadow);
    }
}
