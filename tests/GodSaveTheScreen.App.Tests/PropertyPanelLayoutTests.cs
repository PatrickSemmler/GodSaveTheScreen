using GodSaveTheScreen.App.Controls;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Überarbeitetes Eigenschaftenpanel: Ein/Aus-Schalter der Effekte, gemerkter Aufklapp-Zustand, Zahlenfeld.</summary>
[Collection(WpfApplicationTests.Name)]
public sealed class PropertyPanelLayoutTests
{
    private static (EditorHarness Harness, TextAnnotation Text) WithText()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "abc", Position = new PointD(10, 10), FontFamilyName = "Segoe UI", FontSize = 20 };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        return (h, text);
    }

    [Fact]
    public void EffectSwitch_MerktSichDenLetztenWert()
    {
        var sw = new EffectSwitch<double>(v => v > 0, 0, 2);

        sw.IsOn(0).ShouldBe(false);
        sw.IsOn(5).ShouldBe(true);
        sw.IsOn(null).ShouldBeNull();
        sw.Switch(0, on: true).ShouldBe(2);
        sw.Switch(5, on: false).ShouldBe(0);
        sw.Switch(0, on: true).ShouldBe(5);
        sw.Switch(7, on: true).ShouldBe(7);
    }

    [StaFact]
    public void Schatten_EinUndAus_EinUndoSchrittJeUmschalten()
    {
        var (h, text) = WithText();
        var shadow = h.ViewModel.ShadowProperties;
        shadow.IsShadowOn.ShouldBe(false);

        shadow.IsShadowOn = true;
        text.Shadow!.Color.IsTransparent.ShouldBeFalse();
        shadow.IsShadowOn.ShouldBe(true);

        shadow.ShadowColor = ArgbColor.Red;
        shadow.IsShadowOn = false;
        text.Shadow.Color.IsTransparent.ShouldBeTrue();

        shadow.IsShadowOn = true;
        text.Shadow.Color.ShouldBe(ArgbColor.Red);

        h.Document.Undo.Undo();
        text.Shadow.Color.IsTransparent.ShouldBeTrue();
    }

    [StaFact]
    public void TextKonturUndHintergrund_Schalter()
    {
        var (h, text) = WithText();
        var props = h.ViewModel.TextProperties;

        props.IsOutlineOn.ShouldBe(false);
        props.IsOutlineOn = true;
        text.Stroke.Thickness.ShouldBe(2);
        props.OutlineThickness = 6;
        props.IsOutlineOn = false;
        text.Stroke.Thickness.ShouldBe(0);
        props.IsOutlineOn = true;
        text.Stroke.Thickness.ShouldBe(6);

        props.IsBackgroundOn.ShouldBe(false);
        props.IsBackgroundOn = true;
        text.Background.ShouldBe(ArgbColor.White);
    }

    [StaFact]
    public void Leuchten_UndLinienKontur_Schalter()
    {
        var h = new EditorHarness();
        var callout = new CalloutAnnotation { Rect = new RectD(40, 30, 100, 50), Tail = new PointD(60, 120), Text = "Hallo" };
        var line = new LineAnnotation { Start = new PointD(10, 40), End = new PointD(90, 40), Stroke = new StrokeStyle(ArgbColor.Black, 4) };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, callout));
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, line));

        h.Context.Selection.Set(callout);
        h.ViewModel.CalloutProperties.IsGlowOn = true;
        callout.Border.Thickness.ShouldBe(4);

        h.Context.Selection.Set(line);
        h.ViewModel.Properties.IsOutlineOn = true;
        line.Outline.Thickness.ShouldBe(2);
    }

    [StaFact]
    public void Schalter_OhneAuswahl_AendertWerkzeugVorgabe()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<RectangleTool>());

        h.ViewModel.ShadowProperties.IsShadowOn = true;

        h.Defaults.GetShape(h.Tools.Get<RectangleTool>().Id, new ShapeStyle()).Shadow!.Color.IsTransparent.ShouldBeFalse();
    }

    [Fact]
    public void PanelSectionStore_MerktZustandInDenEinstellungen()
    {
        var settings = new FakeSettings();
        var store = new PanelSectionStore(settings);

        store.IsExpanded("Shadow", fallback: false).ShouldBeFalse();
        store.SetExpanded("Shadow", true);

        store.IsExpanded("Shadow", fallback: false).ShouldBeTrue();
        settings.Current.PanelSections["Shadow"].ShouldBeTrue();
        settings.SaveCount.ShouldBe(1);

        store.SetExpanded("Shadow", true);
        settings.SaveCount.ShouldBe(1);
    }

    [Fact]
    public void AppSettings_PanelSections_WertgleichUndNormalisiert()
    {
        var a = new AppSettings { PanelSections = new PanelSectionMap { ["Font"] = true } };
        var b = new AppSettings { PanelSections = new PanelSectionMap { ["Font"] = true } };

        a.ShouldBe(b);
        (new AppSettings { PanelSections = null! }.Normalized().PanelSections).ShouldNotBeNull();
    }

    [StaFact]
    public void PanelSection_LaedtUndSpeichertZustand()
    {
        var settings = new FakeSettings();
        var store = new PanelSectionStore(settings);
        store.SetExpanded("Outline", true);
        var section = new PanelSection { DefaultExpanded = false };
        PanelSection.SetStore(section, store);

        section.SectionKey = "Outline";
        section.IsExpanded.ShouldBeTrue();

        section.IsExpanded = false;
        store.IsExpanded("Outline", fallback: true).ShouldBeFalse();

        var other = new PanelSection { DefaultExpanded = false, SectionKey = "Glow" };
        PanelSection.SetStore(other, store);
        other.IsExpanded.ShouldBeFalse();
    }

    [StaFact]
    public void SpinBox_BegrenztRundetUndSchrittweite()
    {
        var box = new SpinBox { Minimum = 0, Maximum = 10, SmallChange = 0.5, Decimals = 1, Value = 9.8 };

        box.Step(1);
        box.Value.ShouldBe(10);

        box.Value = null;
        box.Step(0.5);
        box.Value.ShouldBe(0.5);

        box.Value = 3.14159;
        box.Step(0);
        box.Value.ShouldBe(3.1);
    }
}
