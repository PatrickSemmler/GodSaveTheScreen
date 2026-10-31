#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using System.Windows;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Bild verschönern (M15): Hintergrund, Innenabstand und Seitenverhältnis im Export.</summary>
public sealed class FrameBackgroundRenderTests
{
    private static readonly ArgbColor Blue = ArgbColor.FromRgb(0, 0, 255);

    private static ImageDocument WhiteDocument(FrameStyle? frame = null) => new(PixelTools.Solid(100, 80, 255, 255, 255)) { Frame = frame };

    private static PixelBuffer Render(ImageDocument document) => new DocumentExporter().Render(document);

    private static FrameBackground Solid(ArgbColor color) => new() { Kind = FrameBackgroundKind.Solid, ColorA = color };

    private static bool Near((byte R, byte G, byte B, byte A) actual, byte r, byte g, byte b, int tolerance = 6) =>
        Math.Abs(actual.R - r) <= tolerance && Math.Abs(actual.G - g) <= tolerance && Math.Abs(actual.B - b) <= tolerance && actual.A == 255;

    [StaFact]
    public void Einfarbig_FuelltAlleVierEckenDerAusgabe()
    {
        var result = Render(WhiteDocument(new FrameStyle { Background = Solid(Blue), Padding = 20 }));

        result.Width.ShouldBe(140);
        result.Height.ShouldBe(120);
        PixelTools.Get(result, 0, 0).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 139, 0).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 0, 119).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 139, 119).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 70, 60).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255)); // Screenshot bleibt unberührt
    }

    [StaFact]
    public void Verlauf_Waagerecht_EndetAnDenSeiten()
    {
        var gradient = new FrameBackground { Kind = FrameBackgroundKind.Gradient, ColorA = ArgbColor.FromRgb(255, 0, 0), ColorB = ArgbColor.FromRgb(0, 0, 255), AngleDegrees = 0 };
        var result = Render(WhiteDocument(new FrameStyle { Background = gradient, Padding = 40 }));

        Near(PixelTools.Get(result, 0, 5), 255, 0, 0).ShouldBeTrue();
        Near(PixelTools.Get(result, 0, 115), 255, 0, 0).ShouldBeTrue();
        Near(PixelTools.Get(result, result.Width - 1, 5), 0, 0, 255).ShouldBeTrue();
        Near(PixelTools.Get(result, result.Width - 1, 115), 0, 0, 255).ShouldBeTrue();

        // In der Mitte der Fläche (im Randbereich oben) ist es etwa halb und halb.
        var middle = PixelTools.Get(result, result.Width / 2, 5);
        Math.Abs(middle.R - 127).ShouldBeLessThan(8);
        Math.Abs(middle.B - 127).ShouldBeLessThan(8);
    }

    [StaFact]
    public void Verlauf_Senkrecht_EndetObenUndUnten()
    {
        var gradient = new FrameBackground { Kind = FrameBackgroundKind.Gradient, ColorA = ArgbColor.FromRgb(255, 0, 0), ColorB = ArgbColor.FromRgb(0, 0, 255), AngleDegrees = 90 };
        var result = Render(WhiteDocument(new FrameStyle { Background = gradient, Padding = 40 }));

        Near(PixelTools.Get(result, 5, 0), 255, 0, 0).ShouldBeTrue();
        Near(PixelTools.Get(result, result.Width - 6, 0), 255, 0, 0).ShouldBeTrue();
        Near(PixelTools.Get(result, 5, result.Height - 1), 0, 0, 255).ShouldBeTrue();
        Near(PixelTools.Get(result, result.Width - 6, result.Height - 1), 0, 0, 255).ShouldBeTrue();
    }

    [StaFact]
    public void Verlauf_45Grad_EndetInDenGegenueberliegendenEcken()
    {
        var gradient = new FrameBackground { Kind = FrameBackgroundKind.Gradient, ColorA = ArgbColor.FromRgb(255, 0, 0), ColorB = ArgbColor.FromRgb(0, 0, 255), AngleDegrees = 45 };
        var result = Render(WhiteDocument(new FrameStyle { Background = gradient, Padding = 40 }));

        Near(PixelTools.Get(result, 0, 0), 255, 0, 0, 8).ShouldBeTrue();
        Near(PixelTools.Get(result, result.Width - 1, result.Height - 1), 0, 0, 255, 8).ShouldBeTrue();

        // Die anderen beiden Ecken liegen auf der Mittellinie des Verlaufs: weder Anfangs- noch Endfarbe.
        var topRight = PixelTools.Get(result, result.Width - 1, 0);
        topRight.R.ShouldBeInRange((byte)30, (byte)225);
        topRight.B.ShouldBeInRange((byte)30, (byte)225);
    }

    [Fact]
    public void Verlaufspinsel_EndetGenauInDenEcken()
    {
        var brush = FrameRenderer.CreateGradientBrush(new Rect(0, 0, 200, 100), new FrameBackground { Kind = FrameBackgroundKind.Gradient, AngleDegrees = 45 });

        // Länge der Verlaufslinie = |w·cos| + |h·sin|; die Linie läuft durch die Mitte.
        var length = (200 + 100) * Math.Cos(Math.PI / 4);
        (brush.EndPoint - brush.StartPoint).Length.ShouldBe(length, 1e-9);
        ((brush.StartPoint.X + brush.EndPoint.X) / 2).ShouldBe(100, 1e-9);
        ((brush.StartPoint.Y + brush.EndPoint.Y) / 2).ShouldBe(50, 1e-9);
    }

    [Fact]
    public void FillRect_FuelltDieFlaeche_OhneVerzerrung()
    {
        var wide = FrameRenderer.FillRect(new Rect(0, 0, 160, 140), 40, 20);
        wide.Width.ShouldBe(280, 1e-9);
        wide.Height.ShouldBe(140, 1e-9);
        wide.X.ShouldBe(-60, 1e-9);
        (wide.Width / wide.Height).ShouldBe(2, 1e-9);

        var tall = FrameRenderer.FillRect(new Rect(10, 20, 100, 100), 10, 40);
        tall.Width.ShouldBe(100, 1e-9);
        tall.Height.ShouldBe(400, 1e-9);
        tall.Y.ShouldBe(-130, 1e-9);
    }

    [StaFact]
    public void Bild_FuelltDieAusgabe_UndWirdZentriertBeschnitten()
    {
        // Bild 40 × 20: linke Hälfte rot, rechte Hälfte blau.
        var asset = new ImageAsset(ImageAssetFormat.Png, TwoColorImage(40, 20).EncodePng());
        var background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset };
        var result = Render(WhiteDocument(new FrameStyle { Background = background, Padding = 30 }));

        result.Width.ShouldBe(160);
        result.Height.ShouldBe(140);

        // Maßstab 7 (Höhe 20 -> 140), Breite 280, Versatz -60: Die Farbgrenze liegt bei x = 20 · 7 − 60 = 80.
        Near(PixelTools.Get(result, 5, 5), 255, 0, 0, 10).ShouldBeTrue();
        Near(PixelTools.Get(result, 70, 5), 255, 0, 0, 10).ShouldBeTrue();
        Near(PixelTools.Get(result, 90, 5), 0, 0, 255, 10).ShouldBeTrue();
        Near(PixelTools.Get(result, 154, 135), 0, 0, 255, 10).ShouldBeTrue();
        PixelTools.Get(result, 80, 70).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255)); // Screenshot
    }

    [StaFact]
    public void OhneHintergrund_IstDerInnenabstandTransparent()
    {
        var result = Render(WhiteDocument(new FrameStyle { Padding = 15 }));

        result.Width.ShouldBe(130);
        PixelTools.Get(result, 0, 0).A.ShouldBe((byte)0);
        PixelTools.Get(result, 129, 109).A.ShouldBe((byte)0);
        PixelTools.Get(result, 7, 50).A.ShouldBe((byte)0);
        PixelTools.Get(result, 15, 15).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Jpeg_FuelltDenTransparentenInnenabstandWeiss()
    {
        var result = Render(WhiteDocument(new FrameStyle { Padding = 15 }));

        var data = new ImageCodecService().Encode(result, 96, 96, new ImageSaveOptions(ImageFormatSetting.Jpeg));
        var decoded = new ImageCodecService().Decode(data);

        Near(PixelTools.Get(decoded.Image, 2, 2), 255, 255, 255, 4).ShouldBeTrue();
    }

    [StaFact]
    public void RundeEcken_ZeigenDenHintergrundStattTransparenz()
    {
        var result = Render(WhiteDocument(new FrameStyle { CornerRadius = 20, Background = Solid(Blue) }));

        // Ohne Innenabstand reicht der Hintergrund bis an die Bildecken; die abgerundete Ecke des Screenshots zeigt ihn.
        result.Width.ShouldBe(100);
        PixelTools.Get(result, 0, 0).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 99, 79).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
        PixelTools.Get(result, 50, 40).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Seitenverhaeltnis_GiltFuerDieExportierteBildgroesse()
    {
        foreach (var (kind, w, h) in new[] { (FrameAspectKind.Wide16x9, 16, 9), (FrameAspectKind.Standard4x3, 4, 3), (FrameAspectKind.Square, 1, 1), (FrameAspectKind.Tall9x16, 9, 16) })
        {
            var result = Render(WhiteDocument(new FrameStyle { Background = Solid(Blue), Padding = 10, AspectRatio = new FrameAspect { Kind = kind } }));

            Math.Abs(result.Width * h - result.Height * w).ShouldBeLessThanOrEqualTo(Math.Max(w, h));
            result.Width.ShouldBeGreaterThanOrEqualTo(120);
            result.Height.ShouldBeGreaterThanOrEqualTo(100);
        }
    }

    [StaFact]
    public void Inhalt_BehaeltInJederKombinationSeinePixelgroesse()
    {
        var document = WhiteDocument(new FrameStyle
        {
            Background = Solid(Blue),
            Padding = 25,
            CornerRadius = 0,
            BorderColor = ArgbColor.Red,
            BorderLeft = 4,
            BorderTop = 4,
            BorderRight = 4,
            BorderBottom = 4,
            AspectRatio = new FrameAspect { Kind = FrameAspectKind.Square },
        });
        var layout = FrameLayout.Compute(document.EffectiveBounds, document.Frame);
        var result = Render(document);

        result.Width.ShouldBe(layout.Output.Width);
        result.Height.ShouldBe(layout.Output.Height);

        // Der weiße Inhalt (100 × 80) steht unverändert im Ergebnis.
        var left = layout.Content.X - layout.Output.X;
        var top = layout.Content.Y - layout.Output.Y;
        PixelTools.Get(result, left, top).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        PixelTools.Get(result, left + 99, top + 79).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        PixelTools.Get(result, left - 2, top + 40).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // Rand
        PixelTools.Get(result, left + 100, top + 40).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255)); // Rand rechts
    }

    [StaFact]
    public void ObjekteUeberDenBildrand_LassenDieAusgabeWachsen_UndDasVerhaeltnisStimmt()
    {
        var document = WhiteDocument(new FrameStyle { Background = Solid(Blue), Padding = 10, AspectRatio = new FrameAspect { Kind = FrameAspectKind.Wide16x9 } });
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(80, 60, 80, 60), Fill = ArgbColor.Red, Stroke = new StrokeStyle(ArgbColor.Red, 0) });

        var result = Render(document);

        document.EffectiveBounds.Width.ShouldBeGreaterThan(100);
        result.Width.ShouldBeGreaterThanOrEqualTo(document.EffectiveBounds.Width + 20);
        Math.Abs((result.Width * 9) - (result.Height * 16)).ShouldBeLessThanOrEqualTo(16);
    }

    [StaFact]
    public void Schatten_BleibtImInnenabstandSichtbar()
    {
        var result = Render(WhiteDocument(new FrameStyle { Background = Solid(ArgbColor.White), Padding = 40, Shadow = new ShadowStyle(new ArgbColor(255, 0, 0, 0), 10, 0) }));

        result.Width.ShouldBe(180);
        PixelTools.Get(result, 40 + 100 + 5, 80).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255)); // Schatten rechts neben dem Bild
        PixelTools.Get(result, 2, 2).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255)); // Hintergrund
    }

    private static PixelBuffer TwoColorImage(int width, int height)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x < width / 2)
                {
                    PixelTools.Set(buffer, x, y, 255, 0, 0);
                }
                else
                {
                    PixelTools.Set(buffer, x, y, 0, 0, 255);
                }
            }
        }

        return buffer;
    }
}

public sealed class FrameBackgroundToolTests
{
    private static EditorHarness Harness()
    {
        var harness = new EditorHarness();
        harness.ViewModel.Activate(harness.Tools.Get<FrameTool>());
        return harness;
    }

    private static string WritePng(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gsts-m15-{Guid.NewGuid():N}-{name}.png");
        File.WriteAllBytes(path, PixelTools.Solid(30, 20, 200, 100, 50).EncodePng());
        return path;
    }

    [StaFact]
    public void HintergrundEinschalten_SetztGemerktenOderStandardwertUndInnenabstand()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.IsBackgroundOn = true;

        var frame = harness.Document.Frame!;
        frame.HasBackground.ShouldBeTrue();
        frame.Background!.Kind.ShouldBe(FrameBackgroundKind.Gradient);
        frame.Padding.ShouldBe(FrameToolState.DefaultPadding);

        panel.IsBackgroundOn = false;
        harness.Document.Frame.ShouldNotBeNull(); // der Innenabstand bleibt als Effekt
        harness.Document.Frame!.HasBackground.ShouldBeFalse();
    }

    [StaFact]
    public void Hintergrund_WirdGemerktUndBeimWiedereinschaltenHergestellt()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;
        panel.IsBackgroundOn = true;
        panel.BackgroundColorA = ArgbColor.Red;
        panel.BackgroundColorB = ArgbColor.Black;
        panel.BackgroundAngle = 60;
        panel.Padding = 77;

        panel.IsBackgroundOn = false;
        panel.Padding = 0;
        harness.Document.Frame.ShouldBeNull();
        panel.IsBackgroundOn = true;

        var frame = harness.Document.Frame!;
        frame.Background!.ColorA.ShouldBe(ArgbColor.Red);
        frame.Background.ColorB.ShouldBe(ArgbColor.Black);
        frame.Background.AngleDegrees.ShouldBe(60);
        frame.Padding.ShouldBe(77);
    }

    [StaFact]
    public void ArtWechsel_AufEinfarbigUndVerlauf()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.BackgroundKind = FrameBackgroundKind.Solid;
        harness.Document.Frame!.Background!.Kind.ShouldBe(FrameBackgroundKind.Solid);
        panel.IsBackgroundSolid.ShouldBeTrue();

        panel.BackgroundKind = FrameBackgroundKind.Gradient;
        harness.Document.Frame!.Background!.Kind.ShouldBe(FrameBackgroundKind.Gradient);
        panel.IsBackgroundGradient.ShouldBeTrue();
    }

    [StaFact]
    public void BildWaehlen_LaedtDieDateiAlsHintergrund_AbbruchAendertNichts()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;
        var path = WritePng("bg");
        try
        {
            harness.ImageDialogs.PickImageFiles().Returns([]);
            panel.BackgroundKind = FrameBackgroundKind.Image;
            harness.Document.Frame.ShouldBeNull();

            harness.ImageDialogs.PickImageFiles().Returns([path]);
            panel.BackgroundKind = FrameBackgroundKind.Image;

            var background = harness.Document.Frame!.Background!;
            background.Kind.ShouldBe(FrameBackgroundKind.Image);
            background.Image.ShouldNotBeNull();
            panel.HasBackgroundImage.ShouldBeTrue();
            panel.BackgroundImagePreview.ShouldNotBeNull();
            harness.Document.Frame.Padding.ShouldBeGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void BildHintergrund_WirdNichtInDenEinstellungenGespeichert()
    {
        var harness = Harness();
        var path = WritePng("bg");
        try
        {
            harness.ImageDialogs.PickImageFiles().Returns([path]);
            harness.ViewModel.FrameProperties.BackgroundKind = FrameBackgroundKind.Image;

            var defaults = harness.Context.Frame.Defaults;
            defaults.Background.ShouldNotBeNull();
            defaults.Background.Image.ShouldBeNull();
            harness.Settings.Current.FrameDefaults.Background!.Image.ShouldBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void Seitenverhaeltnis_AuswahlUndBenutzerdefiniert()
    {
        var harness = Harness();
        var panel = harness.ViewModel.FrameProperties;

        panel.AspectKind = FrameAspectKind.Wide16x9;
        harness.Document.Frame!.AspectRatio!.Ratio.ShouldBe((16, 9));
        panel.LayoutSummary.ShouldContain("16:9");
        panel.IsAspectCustom.ShouldBeFalse();

        panel.AspectKind = FrameAspectKind.Custom;
        panel.IsAspectCustom.ShouldBeTrue();
        panel.AspectWidth.ShouldBe(16);
        panel.AspectWidth = 21;
        panel.AspectHeight = 9;
        harness.Document.Frame!.AspectRatio!.Ratio.ShouldBe((21, 9));

        panel.AspectKind = FrameAspectKind.Free;
        harness.Document.Frame.ShouldBeNull();
    }

    [StaFact]
    public void ZuGrosseAusgabe_VerhindertDasVerhaeltnis_UndZeigtHinweis()
    {
        var harness = new EditorHarness(new ImageDocument(PixelTools.Solid(12000, 100, 1, 2, 3)));
        harness.ViewModel.Activate(harness.Tools.Get<FrameTool>());
        var panel = harness.ViewModel.FrameProperties;

        panel.AspectKind = FrameAspectKind.Tall9x16;

        panel.IsAspectLimited.ShouldBeTrue();
        harness.Document.OutputBounds.Width.ShouldBe(12000);
    }

    [StaFact]
    public void Innenabstand_LaesstDieAusgabeWachsen()
    {
        var harness = Harness();
        var before = harness.Document.OutputBounds;

        harness.ViewModel.FrameProperties.Padding = 32;

        harness.Document.OutputBounds.Width.ShouldBe(before.Width + 64);
        harness.ViewModel.FrameProperties.LayoutSummary.ShouldBe("32 px");
        harness.ViewModel.ViewRect.Width.ShouldBe(harness.Document.OutputBounds.Width);
    }

    [StaFact]
    public void EingebauteVorlagen_HabenHintergrundUndVorschaubilder()
    {
        var presets = FramePresets.BuiltIn;

        presets.Count.ShouldBe(11);
        var backgrounds = presets.Skip(5).ToList();
        backgrounds.Select(p => p.Name).ShouldBeUnique();
        backgrounds.ShouldAllBe(p => p.Frame!.HasBackground && p.Frame.HasPadding && p.Frame.HasCorners && p.Frame.HasShadow);
        backgrounds.Count(p => p.Frame!.AspectRatio is { Kind: FrameAspectKind.Square }).ShouldBe(1);
        backgrounds.Count(p => p.Frame!.AspectRatio is { Kind: FrameAspectKind.Wide16x9 }).ShouldBe(1);

        foreach (var preset in presets)
        {
            var thumbnail = FramePresetThumbnails.Get(preset.Frame!);
            thumbnail.ShouldNotBeNull();
            thumbnail.IsFrozen.ShouldBeTrue();
        }
    }

    [StaFact]
    public void Vorlage_Anwenden_IstEinUndoSchritt()
    {
        var harness = Harness();
        var presets = harness.ViewModel.Presets;
        var slide = presets.Items.First(i => i.Preset.Frame!.AspectRatio is { Kind: FrameAspectKind.Wide16x9 });

        presets.ApplyCommand.Execute(slide);

        harness.Document.Frame.ShouldBe(slide.Preset.Frame!.Normalized());
        harness.Document.OutputBounds.Width.ShouldBeGreaterThan(harness.Document.OutputBounds.Height);
        harness.Context.Undo.Undo();
        harness.Document.Frame.ShouldBeNull();
        harness.Context.Undo.CanUndo.ShouldBeFalse();
    }

    [StaFact]
    public void Vorlagenliste_ZeigtRahmenvorlagenAlsRaster()
    {
        var harness = Harness();

        harness.ViewModel.Presets.ShowGrid.ShouldBeTrue();
        harness.ViewModel.Presets.Items.ShouldAllBe(i => i.HasThumbnail && i.Thumbnail != null);
    }

    [StaFact]
    public void BildVorlage_OhneVorhandenesBild_FaelltAufEinfarbigZurueck()
    {
        var harness = Harness();
        var preset = new ToolPreset
        {
            Name = "Mit Bild",
            Frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, ColorA = ArgbColor.Red }, Padding = 20 },
        };

        PresetStyles.Apply(harness.Context, FrameTool.ToolId, preset);

        harness.Document.Frame!.Background!.Kind.ShouldBe(FrameBackgroundKind.Solid);
        harness.Document.Frame.Background.ColorA.ShouldBe(ArgbColor.Red);
    }

    [StaFact]
    public void BildVorlage_MitVorhandenemBild_BehaeltDasBild()
    {
        var harness = Harness();
        var asset = new ImageAsset(ImageAssetFormat.Png, PixelTools.Solid(4, 4, 1, 2, 3).EncodePng());
        harness.Document.Frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 12 };
        var preset = new ToolPreset
        {
            Name = "Mit Bild",
            Frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image }, Padding = 30, CornerRadius = 8 },
        };

        PresetStyles.Apply(harness.Context, FrameTool.ToolId, preset);

        harness.Document.Frame!.Background!.Image.ShouldBe(asset);
        harness.Document.Frame.Padding.ShouldBe(30);
    }

    [StaFact]
    public void EigeneVorlage_SpeichertKeinHintergrundbild()
    {
        var harness = Harness();
        var asset = new ImageAsset(ImageAssetFormat.Png, PixelTools.Solid(4, 4, 1, 2, 3).EncodePng());
        harness.Document.Frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 12 };

        var preset = PresetStyles.Capture(harness.Context, FrameTool.ToolId, harness.Tools.Get<FrameTool>().BuiltInStyle, "Eigene");

        preset.Frame!.Background!.Kind.ShouldBe(FrameBackgroundKind.Image);
        preset.Frame.Background.Image.ShouldBeNull();
    }
}
