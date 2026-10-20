using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Tests;

public sealed class CanvasBoundsTests
{
    private static ImageDocument NewDocument() => new(new PixelBuffer(200, 100));

    private static RectangleAnnotation Box(double x, double y, double w, double h) => new() { Rect = new RectD(x, y, w, h) };

    private static int Margin(Annotation annotation) => (int)Math.Ceiling(annotation.VisualMargin);

    [Fact]
    public void OhneObjekte_IstBildflaeche()
    {
        var document = NewDocument();
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));
        document.EffectiveBounds.ShouldBe(document.CanvasBounds);
    }

    [Fact]
    public void ObjektImBild_ErweitertNicht()
    {
        var document = NewDocument();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, Box(20, 20, 50, 30)));
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));
    }

    [Fact]
    public void UeberstehendesObjekt_ErweitertUndUndoVerkleinert()
    {
        var document = NewDocument();
        var box = Box(-30, 80, 50, 40);
        var m = Margin(box);
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, box));

        document.CanvasBounds.ShouldBe(PixelRect.FromLtrb(-30 - m, 0, 200, 120 + m));
        document.EffectiveBounds.ShouldBe(document.CanvasBounds);

        document.Undo.Undo();
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));
    }

    [Fact]
    public void Verschieben_PasstErstNachUndoSchrittAn()
    {
        var document = NewDocument();
        var box = Box(10, 10, 20, 20);
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, box));
        var before = box.Clone();

        // Während des Ziehens ändert das Werkzeug das Objekt direkt – die Fläche bleibt stehen.
        box.Translate(new VectorD(250, 0));
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));

        document.Undo.Execute(new ReplaceGeometryAction([(box, before, box.Clone())]));
        document.CanvasBounds.Right.ShouldBe(280 + Margin(box));

        // Zurückgeschoben: die Fläche schrumpft wieder.
        var outside = box.Clone();
        box.Translate(new VectorD(-250, 0));
        document.Undo.Execute(new ReplaceGeometryAction([(box, outside, box.Clone())]));
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));
    }

    [Fact]
    public void UnsichtbareObjekte_ZaehlenNicht()
    {
        var document = NewDocument();
        var box = Box(-50, -50, 20, 20);
        box.IsVisible = false;
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, box));
        document.CanvasBounds.ShouldBe(new PixelRect(0, 0, 200, 100));
    }

    [Fact]
    public void MitZuschnitt_GiltDerZuschnitt()
    {
        var document = NewDocument();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, Box(-30, 10, 50, 40)));
        document.Undo.Execute(new CropAction(document, new PixelRect(10, 10, 50, 50)));

        document.EffectiveBounds.ShouldBe(new PixelRect(10, 10, 50, 50));
        document.CanvasBounds.X.ShouldBeLessThan(0);
    }

    [Fact]
    public void WeitEntferntesObjekt_WirdBegrenzt()
    {
        var document = NewDocument();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, Box(1_000_000, 0, 10, 10)));
        document.CanvasBounds.Right.ShouldBe(200 + ImageDocument.MaxCanvasOverhang);
    }

    [Fact]
    public void Aenderung_MeldetEffectiveBounds()
    {
        var document = NewDocument();
        var changed = new List<string?>();
        document.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        document.Undo.Execute(new AddAnnotationAction(document.Annotations, Box(150, 50, 100, 10)));

        changed.ShouldContain(nameof(ImageDocument.CanvasBounds));
        changed.ShouldContain(nameof(ImageDocument.EffectiveBounds));
    }
}
