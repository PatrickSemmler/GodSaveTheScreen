using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Zeichenfläche wächst um Objekte, die über den Bildrand ragen (transparenter Bereich).</summary>
public sealed class CanvasExpansionTests
{
    private static ImageDocument WhiteDocument() => new(PixelTools.Solid(100, 80, 255, 255, 255));

    private static RectangleAnnotation BlueBox(double x, double y, double w, double h) => new()
    {
        Rect = new RectD(x, y, w, h),
        Stroke = new StrokeStyle(ArgbColor.Red, 4),
        Fill = ArgbColor.FromRgb(0, 0, 255),
    };

    [StaFact]
    public void Render_UeberstehendesObjekt_ErweitertBildUmTransparentenBereich()
    {
        var document = WhiteDocument();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, BlueBox(-40, 10, 60, 20)));

        var result = new DocumentExporter().Render(document);

        // Links um 40 px plus Strichrand erweitert.
        var origin = document.EffectiveBounds;
        origin.X.ShouldBeLessThan(-40);
        result.Width.ShouldBe(100 - origin.X);
        result.Height.ShouldBe(80);

        // Füllung außerhalb des Bilds bleibt sichtbar.
        PixelTools.Get(result, -20 - origin.X, 20).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));

        // Erweiterter Bereich ohne Objekt ist transparent.
        PixelTools.Get(result, -20 - origin.X, 70).A.ShouldBe((byte)0);

        // Bild selbst ist um den Überstand versetzt.
        PixelTools.Get(result, 50 - origin.X, 70).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_TransparenterBereich_WirdBeiBmpWeiss()
    {
        var document = WhiteDocument();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, BlueBox(90, 70, 40, 40)));
        var codec = new ImageCodecService();

        var data = codec.Encode(new DocumentExporter().Render(document), 96, 96, new ImageSaveOptions(Core.Settings.ImageFormatSetting.Bmp));
        var decoded = codec.Decode(data).Image;

        PixelTools.Get(decoded, decoded.Width - 1, 0).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void ViewRect_FolgtDerErweiterung_ZuschnittmodusZeigtNurBasisbild()
    {
        var harness = new EditorHarness();
        var changed = new List<string?>();
        harness.ViewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        harness.Document.Undo.Execute(new AddAnnotationAction(harness.Document.Annotations, BlueBox(180, 140, 50, 50)));

        changed.ShouldContain(nameof(harness.ViewModel.ViewRect));
        harness.ViewModel.ViewRect.Right.ShouldBeGreaterThan(230);
        harness.ViewModel.ViewRect.Bottom.ShouldBeGreaterThan(190);

        harness.ViewModel.Activate(harness.Tools.Get<CropTool>());
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(0, 0, 200, 150));
        harness.Context.Crop.Rect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }
}
