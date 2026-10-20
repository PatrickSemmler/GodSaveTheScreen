using System.IO;
using System.Windows.Input;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Bereichsauswahl (W), Hintergrundradierer (D) und Maskenbefehle im Editor.</summary>
public sealed class MaskToolTests
{
    private static EditorHarness WhiteEditor() => new(new ImageDocument(PixelTools.Solid(100, 80, 255, 255, 255)));

    private static byte MaskAt(ImageDocument document, int x, int y) => document.Mask.Values[(y * document.Mask.Width) + x];

    private static byte Alpha(PixelBuffer b, int x, int y) => b.Pixels[(y * b.Stride) + (x * 4) + 3];

    private static void Drag(ToolBase tool, double fromX, double fromY, double toX, double toY, ModifierKeys modifiers = ModifierKeys.None)
    {
        tool.OnPointerDown(EditorHarness.Pointer(fromX, fromY, modifiers));
        tool.OnPointerMove(EditorHarness.Pointer((fromX + toX) / 2, (fromY + toY) / 2, modifiers));
        tool.OnPointerMove(EditorHarness.Pointer(toX, toY, modifiers));
        tool.OnPointerUp(EditorHarness.Pointer(toX, toY, modifiers));
    }

    private static AreaSelectTool ActivateSelect(EditorHarness h)
    {
        var tool = h.Tools.Get<AreaSelectTool>();
        h.ViewModel.Activate(tool);
        return tool;
    }

    [StaFact]
    public void Werkzeuge_SindMitWUndDRegistriert()
    {
        var h = WhiteEditor();

        h.Tools.FindByKey(Key.W).ShouldBeOfType<AreaSelectTool>();
        h.Tools.FindByKey(Key.D).ShouldBeOfType<BackgroundEraserTool>();
        h.Tools.FindByKey(Key.X).ShouldBeOfType<EraserTool>("der Objekt-Radierer bleibt unverändert");
    }

    [StaFact]
    public void Rechteck_Entf_MachtBereichTransparent_UndoStelltWiederHer()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);

        Drag(tool, 10, 10, 30, 20);
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(10, 10, 20, 10));
        h.Document.Mask.IsOpaque.ShouldBeTrue("Auswählen allein entfernt nichts");

        tool.OnKeyDown(Key.Delete, ModifierKeys.None).ShouldBeTrue();

        MaskAt(h.Document, 15, 15).ShouldBe((byte)0);
        MaskAt(h.Document, 35, 15).ShouldBe((byte)255);
        h.Context.AreaSelection.IsEmpty.ShouldBeTrue();
        h.Document.Undo.UndoCount.ShouldBe(1);
        h.Document.IsDirty.ShouldBeTrue();

        h.ViewModel.UndoCommand.Execute(null);
        h.Document.Mask.IsOpaque.ShouldBeTrue();
    }

    [StaFact]
    public void Export_EntfernterBereichIstTransparent_UnschaerfeSiehtIhnNicht()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);
        Drag(tool, 0, 0, 50, 80);
        tool.OnKeyDown(Key.Delete, ModifierKeys.None);

        var plain = h.Exporter.Render(h.Document);
        Alpha(plain, 10, 10).ShouldBe((byte)0);
        Alpha(plain, 80, 10).ShouldBe((byte)255);

        // Mit Objekt läuft der Export über den Renderer; die Unschärfe liest das maskierte Bild.
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(
            h.Document.Annotations,
            new GodSaveTheScreen.Core.Annotations.BlurAnnotation { Rect = new RectD(0, 0, 100, 80), Strength = 4 }));
        var blurred = h.Exporter.Render(h.Document);
        Alpha(blurred, 10, 40).ShouldBe((byte)0);
        Alpha(blurred, 90, 40).ShouldBe((byte)255);
    }

    [StaFact]
    public void Umschalt_FuegtHinzu_Alt_ZiehtAb()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);

        Drag(tool, 0, 0, 20, 20);
        Drag(tool, 50, 50, 70, 70, ModifierKeys.Shift);
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(0, 0, 70, 70));

        Drag(tool, 0, 0, 30, 30, ModifierKeys.Alt);
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(50, 50, 20, 20));

        Drag(tool, 5, 5, 10, 10);
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(5, 5, 5, 5), "ohne Taste wird ersetzt");

        tool.OnPointerDown(EditorHarness.Pointer(90, 70));
        tool.OnPointerUp(EditorHarness.Pointer(90, 70));
        h.Context.AreaSelection.IsEmpty.ShouldBeTrue("Klick ohne Fläche hebt die Auswahl auf");
    }

    [StaFact]
    public void Lasso_UndEllipse_WaehlenFormAus()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);

        h.Context.MaskTool = h.Context.MaskTool with { Shape = AreaSelectShape.Lasso };
        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerMove(EditorHarness.Pointer(60, 10));
        h.Context.Interaction.AreaPreview.ShouldNotBeNull();
        tool.OnPointerMove(EditorHarness.Pointer(10, 60));
        tool.OnPointerUp(EditorHarness.Pointer(10, 60));
        h.Context.Interaction.AreaPreview.ShouldBeNull();
        var lasso = h.Context.AreaSelection;
        lasso.Values[(20 * 100) + 20].ShouldBe((byte)255);
        lasso.Values[(55 * 100) + 55].ShouldBe((byte)0, "außerhalb des Dreiecks");

        h.Context.MaskTool = h.Context.MaskTool with { Shape = AreaSelectShape.Ellipse };
        Drag(tool, 0, 0, 40, 40);
        lasso.Values[(20 * 100) + 20].ShouldBe((byte)255);
        lasso.Values[(1 * 100) + 1].ShouldBe((byte)0, "Ecke außerhalb der Ellipse");
    }

    [StaFact]
    public void Zauberstab_WaehltZusammenhaengendeFlaecheMitWeicherKante()
    {
        var image = PixelTools.Solid(100, 80, 255, 255, 255);
        for (var y = 0; y < 80; y++)
        {
            for (var x = 40; x < 100; x++)
            {
                PixelTools.Set(image, x, y, 0, 0, 200);
            }
        }

        var h = new EditorHarness(new ImageDocument(image));
        var tool = ActivateSelect(h);
        h.Context.MaskTool = h.Context.MaskTool with { Shape = AreaSelectShape.MagicWand, Tolerance = 10, Feather = 6 };

        tool.OnPointerDown(EditorHarness.Pointer(5, 5));
        tool.OnPointerUp(EditorHarness.Pointer(5, 5));
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(0, 0, 40, 80));

        tool.OnKeyDown(Key.Delete, ModifierKeys.None);
        MaskAt(h.Document, 5, 40).ShouldBe((byte)0);
        MaskAt(h.Document, 39, 40).ShouldBeInRange((byte)1, (byte)254, "weiche Kante");
        MaskAt(h.Document, 90, 40).ShouldBe((byte)255);
    }

    [StaFact]
    public void Tasten_EscStrgAStrgUmschaltI()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);

        tool.OnKeyDown(Key.A, ModifierKeys.Control).ShouldBeTrue();
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(0, 0, 100, 80));

        tool.OnKeyDown(Key.Escape, ModifierKeys.None).ShouldBeTrue();
        h.Context.AreaSelection.IsEmpty.ShouldBeTrue();
        tool.OnKeyDown(Key.Escape, ModifierKeys.None).ShouldBeFalse();

        Drag(tool, 0, 0, 100, 40);
        tool.OnKeyDown(Key.I, ModifierKeys.Control | ModifierKeys.Shift).ShouldBeTrue();
        h.Context.AreaSelection.Bounds.ShouldBe(new PixelRect(0, 40, 100, 40));

        tool.OnKeyDown(Key.Delete, ModifierKeys.None);
        MaskAt(h.Document, 50, 20).ShouldBe((byte)255);
        MaskAt(h.Document, 50, 60).ShouldBe((byte)0);
    }

    [StaFact]
    public void Werkzeugwechsel_HebtAuswahlAuf()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);
        Drag(tool, 0, 0, 20, 20);

        h.ViewModel.Activate(h.Tools.Get<SelectTool>());

        h.Context.AreaSelection.IsEmpty.ShouldBeTrue();
    }

    [StaFact]
    public void Radierer_EinStrichEinUndoSchritt_AltStelltWiederHer()
    {
        var h = WhiteEditor();
        var eraser = h.Tools.Get<BackgroundEraserTool>();
        h.ViewModel.Activate(eraser);
        h.Context.MaskTool = h.Context.MaskTool with { BrushSize = 10, BrushHardness = 100 };

        Drag(eraser, 10, 40, 90, 40);

        MaskAt(h.Document, 50, 40).ShouldBe((byte)0);
        MaskAt(h.Document, 50, 10).ShouldBe((byte)255);
        h.Document.Undo.UndoCount.ShouldBe(1);

        Drag(eraser, 50, 40, 52, 40, ModifierKeys.Alt);
        MaskAt(h.Document, 50, 40).ShouldBe((byte)255);
        MaskAt(h.Document, 20, 40).ShouldBe((byte)0);
        h.Document.Undo.UndoCount.ShouldBe(2);

        h.ViewModel.UndoCommand.Execute(null);
        h.ViewModel.UndoCommand.Execute(null);
        h.Document.Mask.IsOpaque.ShouldBeTrue();
    }

    [StaFact]
    public void Radierer_EscBrichtStrichAb_KlammernAendernGroesse()
    {
        var h = WhiteEditor();
        var eraser = h.Tools.Get<BackgroundEraserTool>();
        h.ViewModel.Activate(eraser);
        h.Context.MaskTool = h.Context.MaskTool with { BrushSize = 50 };

        eraser.OnPointerDown(EditorHarness.Pointer(50, 40));
        MaskAt(h.Document, 50, 40).ShouldBeLessThan((byte)255, "Live-Anzeige während des Strichs");
        eraser.OnKeyDown(Key.Escape, ModifierKeys.None).ShouldBeTrue();
        eraser.OnPointerUp(EditorHarness.Pointer(50, 40));
        h.Document.Mask.IsOpaque.ShouldBeTrue();
        h.Document.Undo.UndoCount.ShouldBe(0);

        eraser.OnKeyDown(Key.OemCloseBrackets, ModifierKeys.None).ShouldBeTrue();
        h.Context.MaskTool.BrushSize.ShouldBe(60);
        h.ViewModel.MaskTools.BrushSize.ShouldBe(60);
        eraser.OnKeyDown(Key.OemOpenBrackets, ModifierKeys.None);
        h.Context.MaskTool.BrushSize.ShouldBe(50);
    }

    [StaFact]
    public void Panel_ZeigtWerkzeugOptionen_UndSpeichertSie()
    {
        var h = WhiteEditor();
        var panel = h.ViewModel.MaskTools;
        panel.IsApplicable.ShouldBeFalse();

        ActivateSelect(h);
        panel.ShowSelect.ShouldBeTrue();
        panel.ShowEraser.ShouldBeFalse();
        h.ViewModel.HasPanelContent.ShouldBeTrue();
        h.ViewModel.PanelHeader.ShouldBe(h.Tools.Get<AreaSelectTool>().DisplayName);

        panel.Shape = AreaSelectShape.MagicWand;
        panel.ShowTolerance.ShouldBeTrue();
        panel.Feather = 250;
        panel.Feather.ShouldBe(100);
        panel.Tolerance = null;
        h.Settings.Current.MaskToolDefaults.Shape.ShouldBe(AreaSelectShape.MagicWand);
        h.Settings.Current.MaskToolDefaults.Feather.ShouldBe(100);
        h.Settings.Current.MaskToolDefaults.Tolerance.ShouldBe(15);

        h.ViewModel.Activate(h.Tools.Get<BackgroundEraserTool>());
        panel.ShowEraser.ShouldBeTrue();
        panel.BrushHardness = 42.4;
        h.Settings.Current.MaskToolDefaults.BrushHardness.ShouldBe(42);
    }

    [StaFact]
    public void Panel_Befehle_EntfernenZuruecksetzenZuschneiden()
    {
        var h = WhiteEditor();
        var panel = h.ViewModel.MaskTools;
        ActivateSelect(h);
        panel.RemoveAreaCommand.CanExecute(null).ShouldBeFalse();
        panel.ResetMaskCommand.CanExecute(null).ShouldBeFalse();

        panel.SelectAllCommand.Execute(null);
        panel.RemoveAreaCommand.CanExecute(null).ShouldBeTrue();
        Drag(h.Tools.Get<AreaSelectTool>(), 20, 10, 60, 50, ModifierKeys.Alt);
        panel.RemoveAreaCommand.Execute(null);
        panel.HasRemovedAreas.ShouldBeTrue();
        panel.CropToVisibleCommand.CanExecute(null).ShouldBeTrue();

        panel.CropToVisibleCommand.Execute(null);
        h.Document.CropRect.ShouldBe(new PixelRect(20, 10, 40, 40));

        panel.ResetMaskCommand.Execute(null);
        h.Document.Mask.IsOpaque.ShouldBeTrue();
        h.Document.Undo.UndoCount.ShouldBe(3);
    }

    [StaFact]
    public void Zuschneiden_UeberstehendeObjekteBleibenSichtbar()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);
        Drag(tool, 0, 0, 50, 80);
        tool.OnKeyDown(Key.Delete, ModifierKeys.None);
        h.AddRectangle(10, 10, 10, 10);

        MaskCommands.CropToVisible(h.Context).ShouldBeTrue();

        h.Document.CropRect!.Value.X.ShouldBeLessThanOrEqualTo(8);
        h.Document.CropRect!.Value.Right.ShouldBe(100);
    }

    [StaFact]
    public async Task Projekt_SpeichertUndLaedtMaske()
    {
        var h = WhiteEditor();
        var tool = ActivateSelect(h);
        Drag(tool, 10, 10, 30, 30);
        tool.OnKeyDown(Key.Delete, ModifierKeys.None);
        var service = new ProjectService(new DocumentExporter(), new ImageCodecService());
        var path = Path.Combine(Path.GetTempPath(), $"gsts-mask-{Guid.NewGuid():N}.gsts");
        try
        {
            await service.SaveAsync(h.Document, path);
            var loaded = await service.LoadAsync(path);

            loaded.Document.Mask.Values.ShouldBe(h.Document.Mask.Values);
            loaded.Document.IsDirty.ShouldBeFalse();
            loaded.Document.BaseImage.Pixels.ShouldBe(h.Document.BaseImage.Pixels, "das Original bleibt vollständig erhalten");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
