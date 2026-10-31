#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using System.Windows.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Rahmen und Kanteneffekte (M14): Export, Werkzeug, Panel-ViewModel und Projekt.</summary>
public sealed class FrameRenderTests
{
    private static readonly (byte R, byte G, byte B, byte A) White = (255, 255, 255, 255);

    private static ImageDocument WhiteDocument(FrameStyle? frame = null) => new(PixelTools.Solid(100, 80, 255, 255, 255)) { Frame = frame };

    private static PixelBuffer Render(ImageDocument document) => new DocumentExporter().Render(document);

    [StaFact]
    public void Render_OhneRahmen_IstByteGleichZumInhalt()
    {
        var document = WhiteDocument();
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(10, 10, 30, 20), Fill = ArgbColor.FromRgb(0, 0, 255), Stroke = new StrokeStyle(ArgbColor.Red, 3) });
        var exporter = new DocumentExporter();

        var result = exporter.Render(document);
        var content = exporter.RenderContent(document);

        result.Width.ShouldBe(content.Width);
        result.Pixels.AsSpan().SequenceEqual(content.Pixels).ShouldBeTrue();
    }

    [StaFact]
    public void Render_OhneRahmenUndObjekte_LiefertDasBasisbildSelbst()
    {
        var document = WhiteDocument();

        Render(document).ShouldBeSameAs(document.MaskedImage);
    }

    [StaFact]
    public void Render_Rand_HatJeSeiteEigeneBreite()
    {
        var document = WhiteDocument(new FrameStyle { BorderColor = ArgbColor.Red, BorderLeft = 10, BorderTop = 5, BorderRight = 20, BorderBottom = 30 });

        var result = Render(document);

        result.Width.ShouldBe(130);
        result.Height.ShouldBe(115);
        PixelTools.Get(result, 4, 50).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // links
        PixelTools.Get(result, 50, 2).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // oben
        PixelTools.Get(result, 120, 50).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // rechts
        PixelTools.Get(result, 50, 100).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // unten
        PixelTools.Get(result, 10, 5).ShouldBe(White); // erstes Bildpixel
        PixelTools.Get(result, 109, 84).ShouldBe(White); // letztes Bildpixel
    }

    [StaFact]
    public void Render_RundeEcken_MachenDieEckeTransparent()
    {
        var result = Render(WhiteDocument(new FrameStyle { CornerRadius = 20 }));

        result.Width.ShouldBe(100);
        PixelTools.Get(result, 0, 0).A.ShouldBe((byte)0);
        PixelTools.Get(result, 99, 79).A.ShouldBe((byte)0);
        PixelTools.Get(result, 50, 40).ShouldBe(White);
        PixelTools.Get(result, 50, 0).ShouldBe(White);
    }

    [StaFact]
    public void Render_RundeEckenMitRand_RundetAuchDenRandAussen()
    {
        var result = Render(WhiteDocument(new FrameStyle { CornerRadius = 10, BorderColor = ArgbColor.Red, BorderLeft = 8, BorderTop = 8, BorderRight = 8, BorderBottom = 8 }));

        result.Width.ShouldBe(116);
        PixelTools.Get(result, 0, 0).A.ShouldBe((byte)0);
        PixelTools.Get(result, 58, 3).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void Render_Riss_MachtDieKanteAusgefranstTransparent()
    {
        var result = Render(WhiteDocument(new FrameStyle { TornSides = FrameSides.Bottom, TornHeight = 20, TornWidth = 8, TornSeed = 3 }));

        result.Width.ShouldBe(100);
        result.Height.ShouldBe(80);
        var transparentBottom = Enumerable.Range(0, 100).Count(x => PixelTools.Get(result, x, 79).A < 128);
        var transparentTop = Enumerable.Range(0, 100).Count(x => PixelTools.Get(result, x, 0).A < 128);
        transparentBottom.ShouldBeGreaterThan(10);
        transparentTop.ShouldBe(0);
        PixelTools.Get(result, 50, 40).ShouldBe(White);
    }

    [StaFact]
    public void Render_RissMitRand_FolgtDenZacken()
    {
        var result = Render(WhiteDocument(new FrameStyle { TornSides = FrameSides.Bottom, TornHeight = 16, TornWidth = 8, BorderColor = ArgbColor.Red, BorderLeft = 6, BorderTop = 2, BorderRight = 4, BorderBottom = 3 }));

        // Bei Riss gilt die größte Breite für alle Seiten.
        result.Width.ShouldBe(112);
        result.Height.ShouldBe(92);
        PixelTools.Get(result, 3, 40).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 56, 3).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void Render_Schatten_FaelltNachRechtsUnten()
    {
        var result = Render(WhiteDocument(new FrameStyle { Shadow = new ShadowStyle(ArgbColor.Black, 10, 0) }));

        result.Width.ShouldBe(110);
        result.Height.ShouldBe(90);
        PixelTools.Get(result, 105, 40).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255)); // rechts neben dem Bild
        PixelTools.Get(result, 50, 85).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255)); // unter dem Bild
        PixelTools.Get(result, 5, 85).A.ShouldBe((byte)0); // links unterhalb: vom Versatz nicht erfasst
        PixelTools.Get(result, 105, 5).A.ShouldBe((byte)0);
        PixelTools.Get(result, 50, 40).ShouldBe(White);
    }

    [StaFact]
    public void Render_Schatten_FolgtDerDeckungDesInhalts()
    {
        // Runde Ecken: Die abgeschnittene Ecke wirft keinen Schatten an derselben Stelle.
        var document = WhiteDocument(new FrameStyle { CornerRadius = 30, Shadow = new ShadowStyle(ArgbColor.Black, 6, 0) });
        var result = Render(document);

        result.Width.ShouldBe(106);
        PixelTools.Get(result, 0, 0).A.ShouldBe((byte)0);
        PixelTools.Get(result, 90, 75).A.ShouldBeGreaterThan((byte)0); // in der abgeschnittenen Ecke bleibt nur der versetzte Schatten
        PixelTools.Get(result, 50, 85).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void Render_SchattenAufJpeg_FuelltTransparenzWeiss()
    {
        var codec = new ImageCodecService();
        var rendered = Render(WhiteDocument(new FrameStyle { CornerRadius = 30, Shadow = new ShadowStyle(ArgbColor.Black, 6, 4) }));

        var data = codec.Encode(rendered, 96, 96, new ImageSaveOptions(ImageFormatSetting.Jpeg));
        var decoded = codec.Decode(data).Image;

        var corner = PixelTools.Get(decoded, 0, 0);
        corner.R.ShouldBeGreaterThan((byte)240);
        corner.G.ShouldBeGreaterThan((byte)240);
        corner.A.ShouldBe((byte)255);
    }

    [StaFact]
    public void Render_Vignette_DunkeltDieEckenAb()
    {
        var result = Render(WhiteDocument(new FrameStyle { VignetteStrength = 1, VignetteSize = 1 }));

        PixelTools.Get(result, 0, 0).R.ShouldBeLessThan((byte)90);
        PixelTools.Get(result, 99, 79).R.ShouldBeLessThan((byte)90);
        PixelTools.Get(result, 50, 40).R.ShouldBeGreaterThan((byte)250);
        PixelTools.Get(result, 50, 40).A.ShouldBe((byte)255);
    }

    [StaFact]
    public void Render_Vignette_LiegtUeberDenObjekten()
    {
        var document = WhiteDocument(new FrameStyle { VignetteStrength = 1, VignetteSize = 1 });
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(0, 0, 30, 30), Fill = ArgbColor.FromRgb(255, 255, 0), Stroke = new StrokeStyle(ArgbColor.Transparent, 0) });

        var corner = PixelTools.Get(Render(document), 1, 1);

        corner.R.ShouldBeLessThan((byte)100);
        corner.G.ShouldBeLessThan((byte)100);
    }

    [StaFact]
    public void Render_Graustufen_NurDerScreenshot_ObjekteBleibenFarbig()
    {
        var document = new ImageDocument(PixelTools.Solid(100, 80, 255, 0, 0)) { Frame = new FrameStyle { Grayscale = true } };
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(60, 10, 30, 30), Fill = ArgbColor.FromRgb(0, 0, 255), Stroke = new StrokeStyle(ArgbColor.Transparent, 0) });

        var result = Render(document);

        var basePixel = PixelTools.Get(result, 10, 60);
        basePixel.R.ShouldBe(basePixel.G);
        basePixel.G.ShouldBe(basePixel.B);
        basePixel.R.ShouldBe((byte)54);
        PixelTools.Get(result, 75, 25).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_Graustufen_UnschaerfeUndLupeZeigenGrau()
    {
        var document = new ImageDocument(PixelTools.Solid(120, 80, 255, 0, 0)) { Frame = new FrameStyle { Grayscale = true } };
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(5, 5, 40, 30), Mode = BlurMode.Pixelate });
        document.Annotations.Add(new MagnifierAnnotation
        {
            SourceRect = new RectD(50, 40, 20, 20),
            TargetCenter = new PointD(95, 55),
            Zoom = 2,
            Shape = MagnifierShape.Rectangle,
            ShowConnector = false,
            ShowSourceFrame = false,
            Border = new StrokeStyle(ArgbColor.Transparent, 0),
        });

        var result = Render(document);

        var blurred = PixelTools.Get(result, 20, 20);
        blurred.R.ShouldBe(blurred.B);
        blurred.R.ShouldBeLessThan((byte)100);
        var magnified = PixelTools.Get(result, 95, 55);
        magnified.R.ShouldBe(magnified.B);
        magnified.R.ShouldBe((byte)54);
    }

    [StaFact]
    public void Render_ZuschnittMitRahmen_HatDieErwarteteGroesse()
    {
        var document = WhiteDocument(new FrameStyle { BorderColor = ArgbColor.Red, BorderLeft = 3, BorderTop = 3, BorderRight = 3, BorderBottom = 3, Shadow = new ShadowStyle(ArgbColor.Black, 4, 0) });
        document.CropRect = new PixelRect(20, 10, 40, 30);

        var result = Render(document);

        result.Width.ShouldBe(40 + 6 + 4);
        result.Height.ShouldBe(30 + 6 + 4);
        PixelTools.Get(result, 1, 1).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 10, 10).ShouldBe(White);
    }

    [StaFact]
    public void Render_ErweiterteFlaeche_RahmenLiegtAussenUmDieObjekte()
    {
        var document = WhiteDocument(new FrameStyle { BorderColor = ArgbColor.Red, BorderLeft = 5, BorderTop = 5, BorderRight = 5, BorderBottom = 5 });
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, new RectangleAnnotation { Rect = new RectD(80, 60, 50, 50), Fill = ArgbColor.FromRgb(0, 0, 255), Stroke = new StrokeStyle(ArgbColor.Transparent, 0) }));

        var result = Render(document);
        var content = document.EffectiveBounds;

        content.Right.ShouldBeGreaterThanOrEqualTo(130);
        result.Width.ShouldBe(content.Width + 10);
        document.OutputBounds.ShouldBe(new PixelRect(content.X - 5, content.Y - 5, content.Width + 10, content.Height + 10));
        PixelTools.Get(result, result.Width - 3, result.Height - 3).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 2, 2).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void Render_AllesAus_VerhaeltSichWieOhneRahmen()
    {
        var document = WhiteDocument(new FrameStyle { BorderColor = ArgbColor.Red });

        document.Frame.ShouldBeNull();
        Render(document).ShouldBeSameAs(document.MaskedImage);
    }
}

public sealed class FrameToolTests
{
    private static EditorHarness Harness(PixelBuffer? image = null)
    {
        var harness = new EditorHarness(new ImageDocument(image ?? PixelTools.Solid(200, 150, 10, 200, 30)));
        harness.ViewModel.Activate(harness.Tools.Get<FrameTool>());
        return harness;
    }

    [StaFact]
    public void Werkzeug_IstRegistriertMitTasteF()
    {
        var harness = Harness();

        harness.Tools.Get<FrameTool>().Id.ShouldBe("frame");
        harness.Tools.FindByKey(Key.F).ShouldBeOfType<FrameTool>();
        harness.Tools.PaletteTools.Select(t => t.ShortcutKey).Where(k => k != Key.None).ShouldBeUnique();
        harness.ViewModel.FrameProperties.IsApplicable.ShouldBeTrue();
        harness.ViewModel.HasPanelContent.ShouldBeTrue();
    }

    [StaFact]
    public void Schalter_StellenGemerkteWerteWiederHer()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.IsBorderOn = true;
        harness.Document.Frame!.HasBorder.ShouldBeTrue();
        harness.Document.Frame.BorderLeft.ShouldBe(16);

        panel.BorderWidth = 30;
        panel.BorderColor = ArgbColor.Red;
        harness.Document.Frame!.BorderBottom.ShouldBe(30);

        panel.IsBorderOn = false;
        harness.Document.Frame.ShouldBeNull();

        panel.IsBorderOn = true;
        harness.Document.Frame!.BorderLeft.ShouldBe(30);
        harness.Document.Frame.BorderColor.ShouldBe(ArgbColor.Red);
        harness.Context.Frame.Defaults.BorderLeft.ShouldBe(30);
    }

    [StaFact]
    public void Einschalten_ErsterNutzung_VerwendetEingebauteStandards()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.IsShadowOn = true;
        panel.IsCornersOn = true;
        panel.IsVignetteOn = true;
        panel.IsTornOn = true;

        var frame = harness.Document.Frame!;
        frame.HasShadow.ShouldBeTrue();
        frame.CornerRadius.ShouldBe(16);
        frame.VignetteStrength.ShouldBe(0.5);
        frame.TornSides.ShouldBe(FrameSides.Bottom);
    }

    [StaFact]
    public void GemerkteWerte_GeltenAuchInEinemAnderenEditor()
    {
        var first = Harness();
        first.ViewModel.FrameProperties.IsCornersOn = true;
        first.ViewModel.FrameProperties.CornerRadius = 42;

        var second = new EditorHarness();
        second.Settings.Current = first.Settings.Current;
        second.ViewModel.Activate(second.Tools.Get<FrameTool>());
        second.ViewModel.FrameProperties.IsCornersOn = true;

        second.Document.Frame!.CornerRadius.ShouldBe(42);
    }

    [StaFact]
    public void Aenderung_IstEinUndoSchrittUndLaesstSichWiederholen()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.IsShadowOn = true;
        for (var size = 1; size <= 8; size++)
        {
            panel.ShadowSize = size;
        }

        harness.Document.Frame!.Shadow.Size.ShouldBe(8);
        harness.Context.Undo.Undo();
        harness.Document.Frame.ShouldBeNull();
        harness.Context.Undo.Redo();
        harness.Document.Frame!.Shadow.Size.ShouldBe(8);
    }

    [StaFact]
    public void RahmenEntfernen_IstEinUndoSchritt()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;
        panel.IsBorderOn = true;
        panel.IsShadowOn = true;
        panel.IsGrayscale = true;
        var before = harness.Document.Frame;

        panel.RemoveFrameCommand.Execute(null);

        harness.Document.Frame.ShouldBeNull();
        panel.HasFrame.ShouldBeFalse();
        harness.Context.Undo.Undo();
        harness.Document.Frame.ShouldBe(before);
    }

    [StaFact]
    public void Rissseiten_UndNeuWuerfeln()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.TornLeft = true;
        harness.Document.Frame!.TornSides.ShouldBe(FrameSides.Left);
        panel.TornTop = true;
        harness.Document.Frame!.TornSides.ShouldBe(FrameSides.Left | FrameSides.Top);

        var seed = harness.Document.Frame.TornSeed;
        panel.RerollTornCommand.Execute(null);
        harness.Document.Frame!.TornSeed.ShouldNotBe(seed);

        panel.TornLeft = false;
        panel.TornTop = false;
        harness.Document.Frame.ShouldBeNull();
        panel.IsTornOn.ShouldBe(false);
    }

    [StaFact]
    public void RissMitRand_SperrtDieSeitenEinzeln()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;
        panel.IsBorderOn = true;
        panel.BorderPerSide = true;
        panel.CanBorderPerSide.ShouldBeTrue();
        panel.IsBorderPerSideVisible.ShouldBeTrue();

        panel.BorderBottom = 60;
        harness.Document.Frame!.BorderBottom.ShouldBe(60);
        harness.Document.Frame.BorderLeft.ShouldBe(16);

        panel.IsTornOn = true;

        panel.CanBorderPerSide.ShouldBeFalse();
        panel.IsBorderPerSideVisible.ShouldBeFalse();
        panel.IsBorderTornNoteVisible.ShouldBeTrue();
    }

    [StaFact]
    public void Graustufen_SchaltenDenEffektUndDasBasisbildUm()
    {
        var harness = Harness();

        harness.ViewModel.FrameProperties.IsGrayscale = true;

        harness.Document.Frame!.Grayscale.ShouldBeTrue();
        harness.Document.EffectiveImage.ShouldNotBeSameAs(harness.Document.MaskedImage);
        harness.Context.Undo.Undo();
        harness.Document.EffectiveImage.ShouldBeSameAs(harness.Document.MaskedImage);
    }

    [StaFact]
    public void ViewRectUndBildgroesse_FolgenDemRahmen_ZuschnittmodusZeigtDasBasisbild()
    {
        var harness = Harness();
        harness.ViewModel.FrameProperties.IsBorderOn = true;

        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(-16, -16, 232, 182));
        harness.ViewModel.ImageSizeText.ShouldContain("232");

        harness.ViewModel.Activate(harness.Tools.Get<CropTool>());
        harness.ViewModel.ViewRect.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void Mausposition_IstRelativZumInhaltNichtZumRahmen()
    {
        var harness = Harness();
        harness.ViewModel.FrameProperties.IsBorderOn = true;

        harness.ViewModel.SetPointer(new PointD(10, 20));

        harness.ViewModel.PointerText.ShouldContain("10");
        harness.ViewModel.PointerText.ShouldContain("20");
        harness.Document.EffectiveBounds.ShouldBe(new PixelRect(0, 0, 200, 150));
    }

    [StaFact]
    public void Pipette_LiefertDieFarbeDesInhaltsAuchMitRand()
    {
        var harness = Harness();
        harness.ViewModel.FrameProperties.IsBorderOn = true;
        harness.ViewModel.FrameProperties.BorderColor = ArgbColor.Red;
        var pipette = harness.Tools.Get<PipetteTool>();
        ArgbColor? picked = null;
        pipette.Picked = c => picked = c;

        pipette.Activate();
        pipette.OnPointerDown(EditorHarness.Pointer(5, 5));

        picked.ShouldBe(ArgbColor.FromRgb(10, 200, 30));
    }

    [StaFact]
    public void ZuruecksetzenButton_NurSichtbarOhneRahmen()
    {
        var harness = Harness();

        harness.ViewModel.CanResetToolDefaults.ShouldBeTrue();
        harness.ViewModel.FrameProperties.IsVignetteOn = true;
        harness.ViewModel.CanResetToolDefaults.ShouldBeFalse();
        harness.ViewModel.FrameProperties.RemoveFrameCommand.Execute(null);
        harness.ViewModel.CanResetToolDefaults.ShouldBeTrue();
    }

    [StaFact]
    public void Zuruecksetzen_SetztDieGemerktenWerteZurueck()
    {
        var harness = Harness();
        harness.ViewModel.FrameProperties.IsCornersOn = true;
        harness.ViewModel.FrameProperties.CornerRadius = 55;
        harness.ViewModel.FrameProperties.RemoveFrameCommand.Execute(null);
        harness.Context.Frame.Defaults.CornerRadius.ShouldBe(55);

        ToolDefaultsReset.Reset(harness.Context, harness.Tools.Get<FrameTool>());

        harness.Context.Frame.Defaults.ShouldBe(new FrameStyle());
        harness.ViewModel.FrameProperties.IsCornersOn = true;
        harness.Document.Frame!.CornerRadius.ShouldBe(16);
    }

    [StaFact]
    public void Objekte_LassenSichMitRahmenWeiterVerschieben()
    {
        var harness = Harness();
        var box = harness.AddRectangle(20, 20, 30, 30);
        harness.ViewModel.FrameProperties.IsBorderOn = true;
        harness.ViewModel.Activate(harness.Tools.Get<SelectTool>());

        var select = harness.Tools.Get<SelectTool>();
        select.OnPointerDown(EditorHarness.Pointer(35, 35));
        select.OnPointerMove(EditorHarness.Pointer(55, 45));
        select.OnPointerUp(EditorHarness.Pointer(55, 45));

        box.Rect.X.ShouldBe(40, 0.5);
        box.Rect.Y.ShouldBe(30, 0.5);
    }
}

public sealed class FramePresetTests
{
    private static EditorHarness Harness()
    {
        var harness = new EditorHarness();
        harness.ViewModel.Activate(harness.Tools.Get<FrameTool>());
        return harness;
    }

    [StaFact]
    public void EingebauteVorlagen_SindVorhandenUndPolaroidHatBreitereUnterkante()
    {
        var presets = FramePresets.BuiltIn;

        presets.Count.ShouldBe(11);
        presets.Select(p => p.Name).ShouldBeUnique();
        presets.ShouldAllBe(p => p.Frame != null && !p.Frame.IsEmpty);
        var polaroid = presets[2].Frame!;
        polaroid.BorderBottom.ShouldBeGreaterThan(polaroid.BorderLeft);
        polaroid.HasShadow.ShouldBeTrue();
        polaroid.TornSides.ShouldBe(FrameSides.None);
        presets.All(p => FramePresets.IsBuiltInName(p.Name)).ShouldBeTrue();
    }

    [StaFact]
    public void Vorlagenliste_ZeigtEingebauteUndEigeneVorlagen_EingebauteSindGeschuetzt()
    {
        var harness = Harness();
        var presets = harness.ViewModel.Presets;

        presets.ToolId.ShouldBe("frame");
        presets.Items.Count(i => i.IsBuiltIn).ShouldBe(11);

        presets.SelectedItem = presets.Items[0];
        presets.RenameCommand.CanExecute(null).ShouldBeFalse();
        presets.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }

    [StaFact]
    public void Vorlage_Anwenden_ErsetztDenRahmenMitEinemUndoSchritt()
    {
        var harness = Harness();
        harness.ViewModel.FrameProperties.IsVignetteOn = true;
        var before = harness.Document.Frame;
        var presets = harness.ViewModel.Presets;
        var polaroid = presets.Items.First(i => i.Preset.Frame!.BorderBottom == 80);

        presets.ApplyCommand.Execute(polaroid);

        harness.Document.Frame.ShouldBe(polaroid.Preset.Frame!.Normalized());
        harness.Document.Frame!.HasVignette.ShouldBeFalse();
        harness.Context.Undo.Undo();
        harness.Document.Frame.ShouldBe(before);
    }

    [StaFact]
    public void EigeneVorlage_SpeichernUndAnwenden()
    {
        var harness = Harness();
        harness.Dialogs.AskPresetName(default!, default!, default!, default!).ReturnsForAnyArgs("Mein Look");
        harness.ViewModel.FrameProperties.IsCornersOn = true;
        harness.ViewModel.FrameProperties.CornerRadius = 33;
        harness.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        harness.Context.Presets.GetPresets("frame").Select(p => p.Name).ShouldBe(["Mein Look"]);
        harness.ViewModel.FrameProperties.RemoveFrameCommand.Execute(null);

        var item = harness.ViewModel.Presets.Items.First(i => i.Name == "Mein Look");
        item.IsBuiltIn.ShouldBeFalse();
        harness.ViewModel.Presets.ApplyCommand.Execute(item);

        harness.Document.Frame!.CornerRadius.ShouldBe(33);
    }

    [StaFact]
    public void EigeneVorlage_MitNamenEinerEingebautenWirdAbgelehnt()
    {
        var harness = Harness();
        var builtIn = FramePresets.BuiltIn[0].Name;
        harness.Dialogs.AskPresetName(default!, default!, default!, default!).ReturnsForAnyArgs(builtIn);
        harness.ViewModel.FrameProperties.IsCornersOn = true;

        harness.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        harness.Context.Presets.GetPresets("frame").ShouldBeEmpty();
        harness.Dialogs.ReceivedWithAnyArgs().ShowInfo(default!, default!);
    }
}

public sealed class FrameProjectRoundtripTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-frame-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectService _service = new(new DocumentExporter(), new ImageCodecService());

    public FrameProjectRoundtripTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [StaFact]
    public async Task Projekt_SpeichertUndLaedtDenRahmen()
    {
        var frame = new FrameStyle
        {
            BorderColor = ArgbColor.FromRgb(20, 40, 60),
            BorderLeft = 4,
            BorderTop = 4,
            BorderRight = 4,
            BorderBottom = 40,
            Shadow = new ShadowStyle(new ArgbColor(120, 0, 0, 0), 6, 10),
            CornerRadius = 12,
            VignetteStrength = 0.3,
            Grayscale = true,
        };
        var document = new ImageDocument(PixelTools.Solid(60, 40, 200, 100, 50)) { Frame = frame };
        var path = Path.Combine(_directory, "frame.gsts");

        await _service.SaveAsync(document, path);
        var loaded = await _service.LoadAsync(path);

        loaded.Warnings.ShouldBeEmpty();
        loaded.Document.Frame.ShouldBe(frame.Normalized());
        loaded.Document.OutputBounds.ShouldBe(document.OutputBounds);
    }

    [StaFact]
    public void Snapshot_UebernimmtDenRahmenFuerDieAutomatischeSicherung()
    {
        var document = new ImageDocument(PixelTools.Solid(30, 20, 1, 2, 3)) { Frame = new FrameStyle { CornerRadius = 9 } };

        var content = _service.Snapshot(document, _service.EncodeBase(document));

        content.Frame.ShouldBe(document.Frame);
    }
}
