#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using GodSaveTheScreen.App.Editor;
using NSubstitute;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Tests;

public sealed class ToolPresetServiceTests
{
    private static ToolPreset Rect(string name, double thickness = 4) =>
        new() { Name = name, Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, thickness) } };

    [Fact]
    public void ErsterZugriff_LegtMitgelieferteTextvorlagenAn()
    {
        var service = new ToolPresetService(new FakeSettings());

        service.GetPresets(TextTool.ToolId).Count.ShouldBe(4);
        service.GetPresets(RectangleTool.ToolId).ShouldBeEmpty();
    }

    [Fact]
    public void Speichern_GleicherNameAnderesCasing_ErsetztUndUebernimmtNeueSchreibweise()
    {
        var service = new ToolPresetService(new FakeSettings());
        service.Save(RectangleTool.ToolId, Rect("Rot", 2));

        service.Save(RectangleTool.ToolId, Rect("rOT", 9));

        var presets = service.GetPresets(RectangleTool.ToolId);
        presets.Count.ShouldBe(1);
        presets[0].Name.ShouldBe("rOT");
        presets[0].Shape!.Stroke.Thickness.ShouldBe(9);
    }

    [Fact]
    public void Vorlagen_SindJeWerkzeugGetrennt()
    {
        var service = new ToolPresetService(new FakeSettings());
        service.Save(RectangleTool.ToolId, Rect("Rot"));

        service.Exists(RectangleTool.ToolId, "rot").ShouldBeTrue();
        service.Exists(EllipseTool.ToolId, "Rot").ShouldBeFalse();
    }

    [Fact]
    public void Speichern_UngueltigerName_WirdAbgelehnt()
    {
        var service = new ToolPresetService(new FakeSettings());

        service.Save(RectangleTool.ToolId, Rect("   ")).ShouldBeFalse();
        service.GetPresets(RectangleTool.ToolId).ShouldBeEmpty();
    }

    [Fact]
    public void Umbenennen_AendertNurDenNamen_Loeschen_EntferntDieVorlage()
    {
        var service = new ToolPresetService(new FakeSettings());
        service.Save(RectangleTool.ToolId, Rect("Rot", 7));

        service.Rename(RectangleTool.ToolId, "rot", "Blau").ShouldBeTrue();
        service.Find(RectangleTool.ToolId, "Blau")!.Shape!.Stroke.Thickness.ShouldBe(7);
        service.Exists(RectangleTool.ToolId, "Rot").ShouldBeFalse();

        service.Delete(RectangleTool.ToolId, "BLAU").ShouldBeTrue();
        service.GetPresets(RectangleTool.ToolId).ShouldBeEmpty();
        service.Delete(RectangleTool.ToolId, "Blau").ShouldBeFalse();
    }

    [Fact]
    public void Umbenennen_NurGrossKleinschreibung_IstErlaubt()
    {
        var service = new ToolPresetService(new FakeSettings());
        service.Save(RectangleTool.ToolId, Rect("rot"));

        service.Rename(RectangleTool.ToolId, "rot", "ROT").ShouldBeTrue();

        service.GetPresets(RectangleTool.ToolId).Select(p => p.Name).ShouldBe(["ROT"]);
    }

    [Fact]
    public void Umbenennen_AufNamenEinerAnderenVorlage_ErsetztDiese()
    {
        var service = new ToolPresetService(new FakeSettings());
        service.Save(RectangleTool.ToolId, Rect("A", 1));
        service.Save(RectangleTool.ToolId, Rect("B", 2));

        service.Rename(RectangleTool.ToolId, "A", "b").ShouldBeTrue();

        var presets = service.GetPresets(RectangleTool.ToolId);
        presets.Count.ShouldBe(1);
        presets[0].Name.ShouldBe("b");
        presets[0].Shape!.Stroke.Thickness.ShouldBe(1);
    }

    [Fact]
    public void StandardvorlagenWiederherstellen_LaesstEigeneUndGeaenderteUnberuehrt()
    {
        var service = new ToolPresetService(new FakeSettings());
        var original = service.GetPresets(TextTool.ToolId).Select(p => p.Name).ToList();
        service.Delete(TextTool.ToolId, original[0]);
        service.Rename(TextTool.ToolId, original[1], "Eigener Name");
        service.Save(TextTool.ToolId, new ToolPreset { Name = "Eigene", Text = new TextStyle { FontSize = 99 } });

        service.RestoreDefaults(TextTool.ToolId);

        var names = service.GetPresets(TextTool.ToolId).Select(p => p.Name).ToList();
        names.ShouldContain(original[0]);
        names.ShouldContain(original[1]);
        names.ShouldContain("Eigener Name");
        names.ShouldContain("Eigene");
        service.Find(TextTool.ToolId, "Eigene")!.Text!.FontSize.ShouldBe(99);
    }

    [Fact]
    public void Vorlagen_WerdenInDenEinstellungenGespeichert()
    {
        var settings = new FakeSettings();
        new ToolPresetService(settings).Save(RectangleTool.ToolId, Rect("Rot"));

        settings.SaveCount.ShouldBeGreaterThan(0);
        new ToolPresetService(settings).Exists(RectangleTool.ToolId, "Rot").ShouldBeTrue();
    }
}

public sealed class PresetListTests
{
    private static void Name(EditorHarness h, params string?[] answers) =>
        h.Dialogs.AskPresetName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>())
            .Returns(answers[0], answers.Skip(1).ToArray());

    private static void Activate(EditorHarness h, ToolBase tool)
    {
        h.Context.Selection.Clear();
        h.ViewModel.Activate(tool);
    }

    [StaFact]
    public void Liste_ZeigtVorlagenNurFuerWerkzeugeMitVorlagen()
    {
        var h = new EditorHarness();

        Activate(h, h.Tools.Get<SelectTool>());
        h.ViewModel.Presets.IsApplicable.ShouldBeFalse();
        Activate(h, h.Tools.Get<EraserTool>());
        h.ViewModel.Presets.IsApplicable.ShouldBeFalse();

        foreach (var tool in new ToolBase[]
                 {
                     h.Tools.Get<RectangleTool>(), h.Tools.Get<EllipseTool>(), h.Tools.Get<LineTool>(), h.Tools.Get<ArrowTool>(), h.Tools.Get<TriangleTool>(),
                     h.Tools.Get<PolygonTool>(), h.Tools.Get<PenTool>(), h.Tools.Get<HighlighterTool>(), h.Tools.Get<TextTool>(), h.Tools.Get<MarkerTool>(),
                     h.Tools.Get<BlurTool>(), h.Tools.Get<MagnifierTool>(),
                 })
        {
            Activate(h, tool);
            h.ViewModel.Presets.ToolId.ShouldBe(tool.Id);
            h.ViewModel.Presets.IsApplicable.ShouldBeTrue();
        }
    }

    [StaFact]
    public void TextWerkzeug_ZeigtDieMitgeliefertenVorlagenAlsListe()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<TextTool>());

        h.ViewModel.Presets.Items.Count.ShouldBe(4);
        h.ViewModel.Presets.HasDefaults.ShouldBeTrue();
    }

    [StaFact]
    public void Speichern_ErfasstAktuelleWerkzeugEinstellungen()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Defaults.SetShape(RectangleTool.ToolId, new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 9), CornerRadius = 12 });
        Name(h, "  Dick  ");

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        var saved = h.Context.Presets.Find(RectangleTool.ToolId, "Dick")!;
        saved.Name.ShouldBe("Dick");
        saved.Shape!.Stroke.Thickness.ShouldBe(9);
        saved.Shape!.CornerRadius.ShouldBe(12);
        h.ViewModel.Presets.SelectedItem!.Name.ShouldBe("Dick");
    }

    [StaFact]
    public void Speichern_MitAuswahl_ErfasstGemeinsameWerteDerAuswahl()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 40);
        rect.Stroke = new StrokeStyle(ArgbColor.FromRgb(1, 2, 3), 11);
        h.Context.Selection.Set(rect);
        Name(h, "Aus Auswahl");

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        var saved = h.Context.Presets.Find(RectangleTool.ToolId, "Aus Auswahl")!;
        saved.Shape!.Stroke.Thickness.ShouldBe(11);
        saved.Shape!.Stroke.Color.ShouldBe(ArgbColor.FromRgb(1, 2, 3));
    }

    [StaFact]
    public void Speichern_AutovervollstaendigungEnthaeltNurNamenDesAktuellenWerkzeugs()
    {
        var h = new EditorHarness();
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "Rot", Shape = new ShapeStyle() });
        h.Context.Presets.Save(EllipseTool.ToolId, new ToolPreset { Name = "Kreis", Shape = new ShapeStyle() });
        Activate(h, h.Tools.Get<RectangleTool>());
        Name(h, (string?)null);

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        h.Dialogs.Received(1).AskPresetName(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(names => names.Count == 1 && names[0] == "Rot"),
            Arg.Any<string>());
        h.Context.Presets.GetPresets(RectangleTool.ToolId).Count.ShouldBe(1);
    }

    [StaFact]
    public void Speichern_Abbruch_AendertNichts()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        Name(h, (string?)null);

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        h.Context.Presets.GetPresets(RectangleTool.ToolId).ShouldBeEmpty();
        h.Dialogs.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
    }

    [StaFact]
    public void Speichern_VorhandenerNameMitAbweichendemCasing_FragtNachUndUeberschreibt()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "Rot", Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 1) } });
        h.Defaults.SetShape(RectangleTool.ToolId, new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 8) });
        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        Name(h, "rOT");

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        h.Dialogs.Received(1).Confirm(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("Rot", StringComparison.Ordinal)));
        var presets = h.Context.Presets.GetPresets(RectangleTool.ToolId);
        presets.Count.ShouldBe(1);
        presets[0].Name.ShouldBe("rOT");
        presets[0].Shape!.Stroke.Thickness.ShouldBe(8);
    }

    [StaFact]
    public void Speichern_UeberschreibenAbgelehnt_FragtErneutNachDemNamen()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "Rot", Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 1) } });
        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        Name(h, "ROT", "Grün");

        h.ViewModel.Presets.SaveAsPresetCommand.Execute(null);

        var presets = h.Context.Presets.GetPresets(RectangleTool.ToolId);
        presets.Select(p => p.Name).ShouldBe(["Grün", "Rot"]);
        presets.Single(p => p.Name == "Rot").Shape!.Stroke.Thickness.ShouldBe(1);
    }

    [StaFact]
    public void Umbenennen_VorhandenerNameFragtNachNurBeiAndererVorlage()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "A", Shape = new ShapeStyle() });
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "B", Shape = new ShapeStyle() });
        h.ViewModel.Presets.SelectedItem = h.ViewModel.Presets.Items.First(i => i.Name == "A");

        // Nur Groß-/Kleinschreibung der eigenen Vorlage ändern: keine Rückfrage.
        Name(h, "a");
        h.ViewModel.Presets.RenameCommand.Execute(null);
        h.Dialogs.DidNotReceiveWithAnyArgs().Confirm(default!, default!);
        h.Context.Presets.GetPresets(RectangleTool.ToolId).Select(p => p.Name).ShouldBe(["a", "B"]);

        // Auf den Namen einer anderen Vorlage: Rückfrage.
        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        h.ViewModel.Presets.SelectedItem = h.ViewModel.Presets.Items.First(i => i.Name == "a");
        Name(h, "b");
        h.ViewModel.Presets.RenameCommand.Execute(null);
        h.Dialogs.Received(1).Confirm(Arg.Any<string>(), Arg.Any<string>());
        h.Context.Presets.GetPresets(RectangleTool.ToolId).Select(p => p.Name).ShouldBe(["b"]);
    }

    [StaFact]
    public void Loeschen_FragtNachUndEntferntDieVorlage()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "A", Shape = new ShapeStyle() });
        h.ViewModel.Presets.SelectedItem = h.ViewModel.Presets.Items.Single();

        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        h.ViewModel.Presets.DeleteCommand.Execute(null);
        h.Context.Presets.Exists(RectangleTool.ToolId, "A").ShouldBeTrue();

        h.Dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        h.ViewModel.Presets.DeleteCommand.Execute(null);
        h.Context.Presets.Exists(RectangleTool.ToolId, "A").ShouldBeFalse();
        h.ViewModel.Presets.Items.ShouldBeEmpty();
    }

    [StaFact]
    public void Umbenennen_UndLoeschen_BrauchenEineMarkierteVorlage()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "A", Shape = new ShapeStyle() });

        h.ViewModel.Presets.RenameCommand.CanExecute(null).ShouldBeFalse();
        h.ViewModel.Presets.DeleteCommand.CanExecute(null).ShouldBeFalse();
        h.ViewModel.Presets.SelectedItem = h.ViewModel.Presets.Items.Single();
        h.ViewModel.Presets.RenameCommand.CanExecute(null).ShouldBeTrue();
        h.ViewModel.Presets.DeleteCommand.CanExecute(null).ShouldBeTrue();
    }

    [StaFact]
    public void Anwenden_OhneAuswahl_SetztDieWerkzeugVorgabe()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<RectangleTool>());
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset { Name = "Dick", Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 13), Opacity = 0.5 } });

        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.Single());

        var style = h.Defaults.GetShape(RectangleTool.ToolId, new ShapeStyle());
        style.Stroke.Thickness.ShouldBe(13);
        style.Opacity.ShouldBe(0.5);
        h.ViewModel.Properties.Thickness.ShouldBe(13);
    }

    [StaFact]
    public void Anwenden_MitAuswahl_WirktAlsEineUndoAktionUndLaesstGesperrteUnberuehrt()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(10, 10, 40, 40);
        var b = h.AddRectangle(60, 10, 40, 40);
        var locked = h.AddRectangle(110, 10, 40, 40);
        locked.IsLocked = true;
        h.Context.Selection.Set([a, b, locked]);
        h.Context.Presets.Save(RectangleTool.ToolId, new ToolPreset
        {
            Name = "Grün",
            Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.FromRgb(0, 200, 0), 9), Fill = ArgbColor.FromRgb(1, 1, 1), CornerRadius = 6 },
        });

        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.Single());

        a.Stroke.Thickness.ShouldBe(9);
        b.Stroke.Color.ShouldBe(ArgbColor.FromRgb(0, 200, 0));
        a.CornerRadius.ShouldBe(6);
        locked.Stroke.Thickness.ShouldBe(4);
        h.Document.Undo.Undo();
        a.Stroke.Thickness.ShouldBe(4);
        b.Stroke.Thickness.ShouldBe(4);
        a.CornerRadius.ShouldBe(0);
    }

    [StaFact]
    public void Anwenden_StiftVorlage_SetztAuchGlaettungUndDruck()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<PenTool>());
        h.Context.Presets.Save(PenTool.ToolId, new ToolPreset { Name = "Fein", Shape = new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.Red, 2) }, Smoothing = 0.9, UsePressure = false });

        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.Single());

        h.Context.Freehand.Smoothing.ShouldBe(0.9);
        h.Context.Freehand.UsePressure.ShouldBeFalse();
        h.Defaults.GetShape(PenTool.ToolId, new ShapeStyle()).Stroke.Thickness.ShouldBe(2);
    }

    [StaFact]
    public void Anwenden_UnschaerfeUndLupe_SetzenVorlagenUndSpeichern()
    {
        var h = new EditorHarness();
        Activate(h, h.Tools.Get<BlurTool>());
        h.Context.Presets.Save(BlurTool.ToolId, new ToolPreset { Name = "Schwarz", Blur = new BlurStyle { Mode = BlurMode.Redact, Strength = 40 } });
        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.Single());
        h.Context.BlurTemplate.Mode.ShouldBe(BlurMode.Redact);
        h.Defaults.GetBlur().Strength.ShouldBe(40);

        Activate(h, h.Tools.Get<MagnifierTool>());
        h.Context.Presets.Save(MagnifierTool.ToolId, new ToolPreset { Name = "Stark", Magnifier = new MagnifierStyle { Zoom = 5, Shape = MagnifierShape.Rectangle } });
        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.Single());
        h.Context.MagnifierTemplate.Zoom.ShouldBe(5);
        h.Defaults.GetMagnifier().Shape.ShouldBe(MagnifierShape.Rectangle);
    }

    [StaFact]
    public void Anwenden_TextVorlage_UebernimmtAuchSchriftart()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "abc", Position = new PointD(10, 10), FontFamilyName = "Segoe UI", FontSize = 20 };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        h.Context.Presets.Save(TextTool.ToolId, new ToolPreset { Name = "Mono", Text = new TextStyle { FontFamilyName = "Consolas", FontSize = 40, TextAlignment = TextAlignmentKind.Center } });

        h.ViewModel.Presets.ApplyCommand.Execute(h.ViewModel.Presets.Items.First(i => i.Name == "Mono"));

        text.FontFamilyName.ShouldBe("Consolas");
        text.FontSize.ShouldBe(40);
        text.TextAlignment.ShouldBe(TextAlignmentKind.Center);
    }

    [StaFact]
    public void Auswahl_GemischterObjektarten_ZeigtKeineVorlagen()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 40, 40);
        var text = new TextAnnotation { Text = "abc", Position = new PointD(10, 70), FontFamilyName = "Segoe UI", FontSize = 20 };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));

        h.Context.Selection.Set([rect, text]);

        h.ViewModel.Presets.IsApplicable.ShouldBeFalse();
    }

    [StaFact]
    public void Auswahl_Textobjekt_ZeigtTextvorlagenAuchMitAuswahlwerkzeug()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "abc", Position = new PointD(10, 70), FontFamilyName = "Segoe UI", FontSize = 20 };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));

        h.Context.Selection.Set(text);

        h.ViewModel.Presets.ToolId.ShouldBe(TextTool.ToolId);
        h.ViewModel.Presets.Items.Count.ShouldBe(4);
    }
}

public sealed class LastConfigurationTests
{
    private static EditorContext NewContext(EditorHarness h) =>
        new(new GodSaveTheScreen.Core.Imaging.ImageDocument(new GodSaveTheScreen.Core.Imaging.PixelBuffer(50, 50)), new ToolDefaultsService(h.Settings), h.Settings, h.Exporter);

    [StaFact]
    public void Unschaerfe_LetzteKonfigurationUeberdauertNeuesFenster()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<BlurTool>());
        h.ViewModel.ToolOptions.Strength = 33;
        h.ViewModel.ToolOptions.Mode = BlurMode.Redact;
        h.ViewModel.ToolOptions.RedactColor = ArgbColor.FromRgb(5, 6, 7);

        var next = NewContext(h);

        next.BlurTemplate.Strength.ShouldBe(33);
        next.BlurTemplate.Mode.ShouldBe(BlurMode.Redact);
        next.BlurTemplate.RedactColor.ShouldBe(ArgbColor.FromRgb(5, 6, 7));
    }

    [StaFact]
    public void Lupe_LetzteKonfigurationUeberdauertNeuesFenster()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<MagnifierTool>());
        h.ViewModel.ToolOptions.Zoom = 4;
        h.ViewModel.ToolOptions.ShowConnector = false;
        h.ViewModel.ToolOptions.BorderThickness = 8;

        var next = NewContext(h);

        next.MagnifierTemplate.Zoom.ShouldBe(4);
        next.MagnifierTemplate.ShowConnector.ShouldBeFalse();
        next.MagnifierTemplate.Border.Thickness.ShouldBe(8);
    }

    [StaFact]
    public void Freihand_GlaettungUndDruckUeberdauernNeuesFenster()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<PenTool>());
        h.ViewModel.ToolOptions.SmoothingPercent = 80;
        h.ViewModel.ToolOptions.UsePressure = false;

        var next = NewContext(h);

        next.Freehand.Smoothing.ShouldBe(0.8, 1e-9);
        next.Freehand.UsePressure.ShouldBeFalse();
    }

    [StaFact]
    public void FormTextMarker_LetzteKonfigurationBleibtGespeichert()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<EllipseTool>());
        h.ViewModel.Properties.Thickness = 12;
        h.ViewModel.Activate(h.Tools.Get<TextTool>());
        h.ViewModel.TextProperties.FontSize = 51;
        h.ViewModel.Activate(h.Tools.Get<MarkerTool>());
        h.ViewModel.MarkerProperties.Size = 61;

        var defaults = new ToolDefaultsService(h.Settings);

        defaults.GetShape(EllipseTool.ToolId, new ShapeStyle()).Stroke.Thickness.ShouldBe(12);
        defaults.GetText().FontSize.ShouldBe(51);
        defaults.GetMarker().Size.ShouldBe(61);
    }
}
