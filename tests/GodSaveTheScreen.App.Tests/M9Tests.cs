#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.Globalization;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

public sealed class CropExportTests
{
    private static ImageDocument WhiteDocument(int width = 100, int height = 80) => new(PixelTools.Solid(width, height, 255, 255, 255));

    [StaFact]
    public void Render_MitZuschnitt_HatExaktDieZuschnittgroesse()
    {
        var document = WhiteDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(20, 20, 40, 30)));

        var result = new DocumentExporter().Render(document);

        result.Width.ShouldBe(40);
        result.Height.ShouldBe(30);
        PixelTools.Get(result, 0, 0).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_ObjektUeberDenZuschnittrand_WirdAbgeschnittenUndVersetztDargestellt()
    {
        var document = WhiteDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(20, 20, 40, 30)));
        var rect = new RectangleAnnotation
        {
            Rect = new RectD(10, 10, 30, 30), // Originalkoordinaten x 10..40, y 10..40
            Stroke = new StrokeStyle(ArgbColor.Red, 4),
            Fill = ArgbColor.FromRgb(0, 0, 255),
        };
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, rect));

        var result = new DocumentExporter().Render(document);

        result.Width.ShouldBe(40);
        result.Height.ShouldBe(30);

        // Original (30,30) liegt im Inneren des Rechtecks -> im Zuschnitt (10,10) blau.
        PixelTools.Get(result, 10, 10).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));

        // Original (55,30) liegt rechts vom Rechteck -> im Zuschnitt (35,10) weiß.
        PixelTools.Get(result, 35, 10).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));

        // Der Rand des Rechtecks bei Originalx 40 (Strichmitte) -> Zuschnitt x 20 ist rot; das Objekt ragt links über den Zuschnitt hinaus.
        PixelTools.Get(result, 20, 10).R.ShouldBe((byte)255);
        PixelTools.Get(result, 20, 10).B.ShouldBeLessThan((byte)255);
        PixelTools.Get(result, 0, 0).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_ObjektKomplettAusserhalb_BleibtImModellAberNichtImErgebnis()
    {
        var document = WhiteDocument();
        var rect = new RectangleAnnotation
        {
            Rect = new RectD(5, 5, 10, 10),
            Stroke = new StrokeStyle(ArgbColor.Red, 2),
            Fill = ArgbColor.FromRgb(255, 0, 0),
        };
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, rect));
        document.Undo.Execute(new CropAction(document, new PixelRect(50, 40, 30, 30)));

        var result = new DocumentExporter().Render(document);

        document.Annotations.ShouldContain(rect);
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                PixelTools.Get(result, x, y).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
            }
        }
    }

    [StaFact]
    public void Render_ZuschnittUndSpaeterErweitert_ErgibtWiederDasGanzeBild()
    {
        var document = WhiteDocument();
        document.Undo.Execute(new CropAction(document, new PixelRect(10, 10, 20, 20)));
        document.Undo.Execute(new CropAction(document, null));

        var result = new DocumentExporter().Render(document);

        result.Width.ShouldBe(100);
        result.Height.ShouldBe(80);
    }
}

public sealed class CropToolTests
{
    private static EditorHarness NewHarness(out CropTool tool)
    {
        var harness = new EditorHarness();
        tool = harness.Tools.Get<CropTool>();
        return harness;
    }

    [StaFact]
    public void Aktivieren_ZeigtGesamtesBildAlsRahmen()
    {
        var harness = NewHarness(out var tool);

        harness.ViewModel.Activate(tool);

        harness.ViewModel.IsCropping.ShouldBeTrue();
        harness.ViewModel.AreToolsEnabled.ShouldBeFalse();
        harness.Context.Crop.IsActive.ShouldBeTrue();
        harness.Context.Crop.Rect.ShouldBe(new PixelRect(0, 0, 200, 150));
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void Aktivieren_BeiBestehendemZuschnitt_ZeigtGanzesBildUndRahmenAlsAuswahl()
    {
        var harness = NewHarness(out var tool);
        harness.Document.Undo.Execute(new CropAction(harness.Document, new PixelRect(20, 30, 100, 60)));
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(20, 30, 100, 60));

        harness.ViewModel.Activate(tool);

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(20, 30, 100, 60));
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void Uebernehmen_SetztZuschnittUndVerlaesstDenModus()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 20, 80, 60));

        tool.Commit();

        harness.Document.CropRect.ShouldBe(new PixelRect(10, 20, 80, 60));
        harness.ViewModel.IsCropping.ShouldBeFalse();
        harness.ViewModel.ActiveTool.ShouldBeOfType<SelectTool>();
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(10, 20, 80, 60));
        harness.Document.IsDirty.ShouldBeTrue();
        harness.Context.Undo.CanUndo.ShouldBeTrue();
    }

    [StaFact]
    public void Abbrechen_VerwirftDenRahmen()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 20, 80, 60));

        tool.CancelMode();

        harness.Document.CropRect.ShouldBeNull();
        harness.ViewModel.IsCropping.ShouldBeFalse();
        harness.Context.Undo.CanUndo.ShouldBeFalse();
        harness.Context.Crop.IsActive.ShouldBeFalse();
    }

    [StaFact]
    public void Enter_UebernimmtEscBrichtAb()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(5, 5, 50, 50));
        tool.OnKeyDown(System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();
        harness.Document.CropRect.ShouldBeNull();

        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(5, 5, 50, 50));
        tool.OnKeyDown(System.Windows.Input.Key.Enter, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();
        harness.Document.CropRect.ShouldBe(new PixelRect(5, 5, 50, 50));
    }

    [StaFact]
    public void Uebernehmen_OhneAenderung_ErzeugtKeinenUndoSchritt()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);

        tool.Commit();

        harness.Context.Undo.CanUndo.ShouldBeFalse();
        harness.Document.IsDirty.ShouldBeFalse();
    }

    [StaFact]
    public void Undo_ImZuschnittmodus_BrichtDenModusAb()
    {
        var harness = NewHarness(out var tool);
        harness.AddRectangle(10, 10, 30, 30);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(5, 5, 50, 50));

        harness.ViewModel.UndoCommand.Execute(null);

        harness.ViewModel.IsCropping.ShouldBeFalse();
        harness.Document.CropRect.ShouldBeNull();
    }

    [StaFact]
    public void NeuenBereichAufziehen_AussenhalbDesRahmens()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(100, 100, 50, 40));

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerMove(EditorHarness.Pointer(40, 50));
        tool.OnPointerUp(EditorHarness.Pointer(40, 50));

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(10, 10, 30, 40));
    }

    [StaFact]
    public void KlickAusserhalbOhneZiehen_LaesstDenRahmenUnveraendert()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(100, 100, 50, 40));

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(10, 10));

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(100, 100, 50, 40));
    }

    [StaFact]
    public void EckgriffZiehen_VeraendertDieGroesse()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(50, 50, 100, 60));

        tool.HitHandle(new PointD(150, 110)).ShouldBe(CropHandle.BottomRight);
        tool.OnPointerDown(EditorHarness.Pointer(150, 110));
        tool.OnPointerMove(EditorHarness.Pointer(170, 130, System.Windows.Input.ModifierKeys.Alt));
        tool.OnPointerUp(EditorHarness.Pointer(170, 130));

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(50, 50, 120, 80));
    }

    [StaFact]
    public void Inneres_VerschiebtDenRahmen()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(50, 50, 40, 30));

        tool.HitHandle(new PointD(70, 65)).ShouldBe(CropHandle.Move);
        tool.OnPointerDown(EditorHarness.Pointer(70, 65));
        tool.OnPointerMove(EditorHarness.Pointer(90, 85, System.Windows.Input.ModifierKeys.Alt));
        tool.OnPointerUp(EditorHarness.Pointer(90, 85));

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(70, 70, 40, 30));
    }

    [StaFact]
    public void Griffe_WerdenErkannt()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(50, 50, 100, 80));

        tool.HitHandle(new PointD(50, 50)).ShouldBe(CropHandle.TopLeft);
        tool.HitHandle(new PointD(150, 50)).ShouldBe(CropHandle.TopRight);
        tool.HitHandle(new PointD(50, 130)).ShouldBe(CropHandle.BottomLeft);
        tool.HitHandle(new PointD(100, 50)).ShouldBe(CropHandle.Top);
        tool.HitHandle(new PointD(100, 130)).ShouldBe(CropHandle.Bottom);
        tool.HitHandle(new PointD(50, 90)).ShouldBe(CropHandle.Left);
        tool.HitHandle(new PointD(150, 90)).ShouldBe(CropHandle.Right);
        tool.HitHandle(new PointD(5, 5)).ShouldBe(CropHandle.None);
    }

    [StaFact]
    public void Einrasten_AnObjektkanteInnerhalbDerToleranz()
    {
        var harness = NewHarness(out var tool);
        harness.AddRectangle(100, 40, 50, 50); // Kante bei x = 100 - Strichrand
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 10, 60, 60));
        var (xs, _) = CropGeometry.SnapLines(harness.Context.Crop.FullBounds, harness.Document.Annotations);
        var target = xs.First(x => x > 90 && x < 100);

        tool.OnPointerDown(EditorHarness.Pointer(70, 40));
        tool.OnPointerMove(EditorHarness.Pointer(target + 3, 40));

        harness.Context.Crop.Rect.Right.ShouldBe((int)target);
    }

    [StaFact]
    public void Seitenverhaeltnis_PasstDenRahmenAnUndHaeltEsBeimZiehen()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);

        harness.ViewModel.Crop.SelectedAspect = harness.ViewModel.Crop.AspectOptions.First(o => o.Kind == CropAspectKind.Square);
        var rect = harness.Context.Crop.Rect;
        rect.Width.ShouldBe(rect.Height);

        harness.Context.Crop.SetRect(new PixelRect(20, 20, 50, 50));
        tool.OnPointerDown(EditorHarness.Pointer(70, 70));
        tool.OnPointerMove(EditorHarness.Pointer(120, 90, System.Windows.Input.ModifierKeys.Alt));
        tool.OnPointerUp(EditorHarness.Pointer(120, 90));

        harness.Context.Crop.Rect.Width.ShouldBe(harness.Context.Crop.Rect.Height);
        harness.Context.Crop.Rect.Width.ShouldBeGreaterThan(50);
    }

    [StaFact]
    public void Pixeleingabe_AendertDenRahmen()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 10, 50, 40));

        harness.ViewModel.Crop.Width = 80;
        harness.ViewModel.Crop.Height = 70;
        harness.ViewModel.Crop.X = 30;
        harness.ViewModel.Crop.Y = 20;

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(30, 20, 80, 70));
    }

    [StaFact]
    public void Pixeleingabe_WirdAnBildgrenzenGeklemmt()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 10, 50, 40));

        harness.ViewModel.Crop.Width = 5000;
        harness.ViewModel.Crop.X = 5000;

        harness.Context.Crop.Rect.Right.ShouldBeLessThanOrEqualTo(200);
        harness.Context.Crop.Rect.X.ShouldBeGreaterThanOrEqualTo(0);
    }

    [StaFact]
    public void ZurueckSetzen_StelltDieVolleFlaecheHer()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);
        harness.Context.Crop.SetRect(new PixelRect(10, 10, 50, 40));

        harness.ViewModel.Crop.ResetCommand.Execute(null);

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void AufObjekteZuschneiden_UmschliesstAlleObjekte()
    {
        var harness = NewHarness(out var tool);
        harness.AddRectangle(40, 30, 20, 20);
        harness.AddRectangle(100, 80, 30, 20);
        harness.ViewModel.Activate(tool);

        harness.ViewModel.Crop.CropToObjectsCommand.Execute(null);

        var rect = harness.Context.Crop.Rect;
        rect.X.ShouldBeLessThanOrEqualTo(40);
        rect.Y.ShouldBeLessThanOrEqualTo(30);
        rect.Right.ShouldBeGreaterThanOrEqualTo(130);
        rect.Bottom.ShouldBeGreaterThanOrEqualTo(100);
        rect.Width.ShouldBeLessThan(200);
    }

    [StaFact]
    public void AufObjekteZuschneiden_OhneObjekte_ZeigtHinweis()
    {
        var harness = NewHarness(out var tool);
        harness.ViewModel.Activate(tool);

        harness.ViewModel.Crop.CropToObjectsCommand.Execute(null);

        harness.ViewModel.Crop.Hint.ShouldNotBeNull();
        harness.Context.Crop.Rect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void LeerenRandEntfernen_ErkenntInhaltsbereich()
    {
        var image = PixelTools.Solid(120, 80, 255, 255, 255);
        for (var y = 20; y < 50; y++)
        {
            for (var x = 30; x < 90; x++)
            {
                PixelTools.Set(image, x, y, 0, 0, 0);
            }
        }

        var harness = new EditorHarness(new ImageDocument(image));
        var tool = harness.Tools.Get<CropTool>();
        harness.ViewModel.Activate(tool);

        harness.ViewModel.Crop.RemoveBorderCommand.Execute(null);

        harness.Context.Crop.Rect.ShouldBe(new PixelRect(30, 20, 60, 30));
    }

    [StaFact]
    public void MauskoordinatenInDerStatusleiste_SindRelativZumZuschnitt()
    {
        var harness = NewHarness(out var tool);
        harness.Document.Undo.Execute(new CropAction(harness.Document, new PixelRect(20, 30, 100, 60)));

        harness.ViewModel.SetPointer(new PointD(25, 40));

        harness.ViewModel.PointerText.ShouldBe(string.Format(CultureInfo.CurrentUICulture, Strings.Editor_PointerPosition, 5, 10));
        harness.ViewModel.ImageSizeText.ShouldBe(string.Format(CultureInfo.CurrentUICulture, Strings.Editor_ImageSize, 100, 60));
        _ = tool;
    }

    [StaFact]
    public void Ebenenliste_MarkiertObjekteAusserhalbDesZuschnitts()
    {
        var harness = NewHarness(out _);
        var outside = harness.AddRectangle(5, 5, 10, 10);
        var inside = harness.AddRectangle(60, 60, 30, 30);

        harness.Document.Undo.Execute(new CropAction(harness.Document, new PixelRect(50, 50, 100, 80)));

        var items = harness.ViewModel.Layers.Items;
        items.First(i => i.Annotation == outside).IsOutsideCrop.ShouldBeTrue();
        items.First(i => i.Annotation == inside).IsOutsideCrop.ShouldBeFalse();

        harness.Document.Undo.Undo();
        items.First(i => i.Annotation == outside).IsOutsideCrop.ShouldBeFalse();
    }

    [StaFact]
    public void Pipette_LiestFarbeRelativZumZuschnitt()
    {
        var image = PixelTools.Solid(100, 80, 255, 255, 255);
        PixelTools.Set(image, 50, 40, 10, 20, 30);
        var harness = new EditorHarness(new ImageDocument(image));
        harness.Document.Undo.Execute(new CropAction(harness.Document, new PixelRect(40, 30, 30, 30)));
        var pipette = harness.Tools.Get<PipetteTool>();
        ArgbColor? picked = null;
        pipette.Picked = c => picked = c;

        pipette.Activate();
        pipette.OnPointerDown(EditorHarness.Pointer(50.5, 40.5));

        picked.ShouldBe(ArgbColor.FromRgb(10, 20, 30));
    }
}
