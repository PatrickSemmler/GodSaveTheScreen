using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Sprechblasen-Werkzeug, Spitze und Körper im Auswahlwerkzeug, Eigenschaften-Panel und Vorlagen.</summary>
public sealed class CalloutToolTests
{
    private static CalloutAnnotation AddCallout(EditorHarness h)
    {
        var callout = new CalloutAnnotation { Rect = new RectD(40, 30, 100, 50), Tail = new PointD(60, 120), Text = "Hallo" };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, callout));
        return callout;
    }

    private static void Drag(ToolBase tool, PointD from, PointD to)
    {
        tool.OnPointerDown(EditorHarness.Pointer(from.X, from.Y));
        tool.OnPointerMove(EditorHarness.Pointer((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        tool.OnPointerMove(EditorHarness.Pointer(to.X, to.Y));
        tool.OnPointerUp(EditorHarness.Pointer(to.X, to.Y));
    }

    [StaFact]
    public void Werkzeug_IstRegistriertMitTasteK()
    {
        var h = new EditorHarness();

        var tool = h.Tools.Get<CalloutTool>();

        h.Tools.FindByKey(System.Windows.Input.Key.K).ShouldBeSameAs(tool);
        tool.DisplayName.ShouldNotBe(tool.NameKey);
    }

    [StaFact]
    public void Aufziehen_ErzeugtBlaseMitStandardwertenUndStartetTexteingabe()
    {
        var h = new EditorHarness();
        h.Context.Callout.Style = h.Context.Callout.Style with { Shape = CalloutShape.Ellipse, Background = ArgbColor.FromRgb(0, 128, 0), FontSize = 16 };
        var tool = h.Tools.Get<CalloutTool>();
        h.ViewModel.Activate(tool);

        Drag(tool, new PointD(50, 40), new PointD(150, 90));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeTrue();
        var callout = session.Annotation.ShouldBeOfType<CalloutAnnotation>();
        callout.Rect.ShouldBe(new RectD(50, 40, 100, 50));
        callout.Tail.ShouldBe(CalloutToolState.DefaultTail(callout.Rect));
        callout.Shape.ShouldBe(CalloutShape.Ellipse);
        callout.Background.ShouldBe(ArgbColor.FromRgb(0, 128, 0));
        callout.FontSize.ShouldBe(16);
        h.Document.Annotations.ShouldBeEmpty();
        h.Context.Interaction.Hidden.ShouldBeSameAs(callout);

        h.Context.CommitTextEdit("Hallo");

        h.Document.Annotations.ShouldHaveSingleItem().ShouldBeSameAs(callout);
        callout.Text.ShouldBe("Hallo");
        callout.Name.ShouldNotBeNullOrEmpty();
        h.Context.Selection.Items.ShouldHaveSingleItem().ShouldBeSameAs(callout);

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBeEmpty();
    }

    [StaFact]
    public void Klick_SetztBlaseInStandardgroesseUmDenPunkt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<CalloutTool>();

        tool.OnPointerDown(EditorHarness.Pointer(100, 70));
        tool.OnPointerUp(EditorHarness.Pointer(100, 70));

        var callout = h.Context.ActiveTextEdit.ShouldNotBeNull().Annotation.ShouldBeOfType<CalloutAnnotation>();
        var size = CalloutToolState.DefaultBodySize(callout.FontSize);
        callout.Rect.ShouldBe(new RectD(100 - (size.Width / 2), 70 - (size.Height / 2), size.Width, size.Height));
    }

    [StaFact]
    public void LeererText_VerwirftNeueBlase()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<CalloutTool>();

        Drag(tool, new PointD(50, 40), new PointD(150, 90));
        h.Context.CommitTextEdit("  ");

        h.Document.Annotations.ShouldBeEmpty();
        h.Document.Undo.CanUndo.ShouldBeFalse();
    }

    [StaFact]
    public void LangerText_BlaseWaechstNachUnten()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<CalloutTool>();
        Drag(tool, new PointD(50, 40), new PointD(150, 80));
        var callout = (CalloutAnnotation)h.Context.ActiveTextEdit!.Annotation;

        h.Context.CommitTextEdit("Eins\nZwei\nDrei\nVier\nFünf");

        callout.Rect.Y.ShouldBe(40);
        callout.Rect.Height.ShouldBeGreaterThan(40);
        callout.TextArea.Height.ShouldBeGreaterThanOrEqualTo(callout.CreateTextBlock().LayoutSize.Height - 0.5);
    }

    [StaFact]
    public void TextBearbeiten_PasstHoeheAnUndIstEinUndoSchritt()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var before = callout.Rect;

        h.Context.BeginTextEdit(callout, isNew: false);
        h.Context.CommitTextEdit("1\n2\n3\n4\n5\n6");

        callout.Rect.Height.ShouldBeGreaterThan(before.Height);
        h.Document.Undo.Undo();
        callout.Text.ShouldBe("Hallo");
        callout.Rect.ShouldBe(before);
    }

    [StaFact]
    public void KlickAufVorhandeneBlase_BearbeitetText()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var tool = h.Tools.Get<CalloutTool>();

        tool.OnPointerDown(EditorHarness.Pointer(90, 55));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeFalse();
        session.Annotation.ShouldBeSameAs(callout);
    }

    [StaFact]
    public void Doppelklick_MitAuswahlwerkzeug_BearbeitetText()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);

        h.Tools.Get<SelectTool>().OnPointerDown(EditorHarness.Pointer(90, 55, clicks: 2));

        h.Context.ActiveTextEdit.ShouldNotBeNull().Annotation.ShouldBeSameAs(callout);
    }

    [StaFact]
    public void Griffe_KoerperUndSpitze()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);

        var handles = SelectionHandles.Get([callout]);

        handles.Count.ShouldBe(9);
        handles.ShouldContain(x => x.Kind == HandleKind.CalloutTail && x.Position == callout.Tail);
        handles.ShouldContain(x => x.Kind == HandleKind.BottomRight && x.Position == new PointD(140, 80));
    }

    [StaFact]
    public void SpitzeZiehen_VerschiebtNurDieSpitze_RueckgaengigMoeglich()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(callout);

        Drag(select, callout.Tail, new PointD(190, 60));

        callout.Tail.ShouldBe(new PointD(190, 60));
        callout.Rect.ShouldBe(new RectD(40, 30, 100, 50));
        callout.TailGeometry.ShouldNotBeNull().BaseStart.X.ShouldBe(139, 1e-6);

        h.Document.Undo.Undo();
        callout.Tail.ShouldBe(new PointD(60, 120));
    }

    [StaFact]
    public void Eckgriff_SkaliertNurDenKoerper()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(callout);

        Drag(select, new PointD(140, 80), new PointD(180, 100));

        callout.Rect.ShouldBe(new RectD(40, 30, 140, 70));
        callout.Tail.ShouldBe(new PointD(60, 120));
        callout.FontSize.ShouldBe(20);
    }

    [StaFact]
    public void Verschieben_BewegtKoerperUndSpitze()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var select = h.Tools.Get<SelectTool>();

        Drag(select, new PointD(90, 55), new PointD(100, 65));

        callout.Rect.ShouldBe(new RectD(50, 40, 100, 50));
        callout.Tail.ShouldBe(new PointD(70, 130));
    }

    [StaFact]
    public void Panel_OhneAuswahl_BearbeitetStandardwerte()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<CalloutTool>());
        var panel = h.ViewModel.CalloutProperties;

        panel.IsApplicable.ShouldBeTrue();
        panel.ShowDefaults.ShouldBeTrue();
        panel.Shape = CalloutShape.Cloud;
        panel.BorderColor = ArgbColor.FromRgb(255, 255, 0);
        panel.BorderThickness = 5;
        panel.BorderBlur = 8;
        panel.TailCurved = true;
        panel.DefaultOpacityPercent = 80;

        var style = h.Settings.Current.CalloutDefaults;
        style.Shape.ShouldBe(CalloutShape.Cloud);
        style.Border.ShouldBe(new StrokeStyle(ArgbColor.FromRgb(255, 255, 0), 5));
        style.BorderBlur.ShouldBe(8);
        style.TailCurved.ShouldBeTrue();
        style.Opacity.ShouldBe(0.8, 1e-9);
        panel.ShowCornerRadius.ShouldBeFalse();
        panel.ShowTailCurved.ShouldBeFalse();
        h.Document.Undo.CanUndo.ShouldBeFalse();
    }

    [StaFact]
    public void Panel_MitAuswahl_AendertBlaseMitUndoUndMerktStandardwerte()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<CalloutTool>());
        var callout = AddCallout(h);
        h.Context.Selection.Set(callout);
        var panel = h.ViewModel.CalloutProperties;

        panel.ShowDefaults.ShouldBeFalse();
        panel.BackgroundColor = ArgbColor.FromRgb(0, 0, 255);
        panel.TailWidth = 40;

        callout.Background.ShouldBe(ArgbColor.FromRgb(0, 0, 255));
        callout.TailWidth.ShouldBe(40);
        h.Settings.Current.CalloutDefaults.Background.ShouldBe(ArgbColor.FromRgb(0, 0, 255));

        h.Document.Undo.Undo();
        callout.TailWidth.ShouldBe(24);
    }

    [StaFact]
    public void Schatten_OhneAuswahl_IstStandardDerSprechblase()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<CalloutTool>());
        var shadow = h.ViewModel.ShadowProperties;

        shadow.IsApplicable.ShouldBeTrue();
        shadow.ShadowSize = 9;

        h.Settings.Current.CalloutDefaults.Shadow.Size.ShouldBe(9);
    }

    [StaFact]
    public void Vorlage_ErfassenUndAnwenden()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<CalloutTool>();
        h.ViewModel.Activate(tool);
        h.Context.Callout.Style = h.Context.Callout.Style with { Shape = CalloutShape.Ellipse, TextColor = ArgbColor.Black };

        var preset = PresetStyles.Capture(h.Context, tool.Id, tool.BuiltInStyle, "Gelb");
        preset.Callout.ShouldNotBeNull().Shape.ShouldBe(CalloutShape.Ellipse);

        var callout = AddCallout(h);
        h.Context.Selection.Set(callout);
        PresetStyles.Apply(h.Context, tool.Id, preset).ShouldBe(1);

        callout.Shape.ShouldBe(CalloutShape.Ellipse);
        callout.TextColor.ShouldBe(ArgbColor.Black);
        callout.Text.ShouldBe("Hallo");
        h.Document.Undo.Undo();
        callout.Shape.ShouldBe(CalloutShape.Rounded);
    }

    [StaFact]
    public void StandardWiederherstellen_SetztStilZurueck()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<CalloutTool>();
        h.Context.Callout.Style = h.Context.Callout.Style with { Shape = CalloutShape.Cloud, FontSize = 40 };

        ToolDefaultsReset.Supports(tool).ShouldBeTrue();
        ToolDefaultsReset.Reset(h.Context, tool);

        h.Context.Callout.Style.ShouldBe(new CalloutStyle());
    }

    [StaFact]
    public void Drehgriff_DrehtKoerper_ZielpunktBleibt_RueckgaengigMoeglich()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(callout);
        var rotate = SelectionHandles.Get([callout], 26).Single(x => x.Kind == HandleKind.Rotate);

        // Mitte (90, 55): Zeiger rechts daneben = 90°.
        Drag(select, rotate.Position, new PointD(190, 55));

        callout.Rotation.ShouldBe(90, 1e-6);
        callout.Tail.ShouldBe(new PointD(60, 120));
        callout.Rect.ShouldBe(new RectD(40, 30, 100, 50));

        h.Document.Undo.Undo();
        callout.Rotation.ShouldBe(0);
    }

    [StaFact]
    public void GedrehteBlase_EckgriffSkaliertImEigenenSystem_GegenueberliegendeEckeBleibt()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        callout.Rotation = 90;
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(callout);
        var center = callout.Center;
        var fixedCorner = RotationMath.Rotate(new PointD(40, 30), center, 90);

        // Lokal unten rechts (140, 80) → (160, 100).
        Drag(select, RotationMath.Rotate(new PointD(140, 80), center, 90), RotationMath.Rotate(new PointD(160, 100), center, 90));

        callout.Rect.Width.ShouldBe(120, 1e-6);
        callout.Rect.Height.ShouldBe(70, 1e-6);
        var corner = RotationMath.Rotate(callout.Rect.TopLeft, callout.Center, 90);
        corner.X.ShouldBe(fixedCorner.X, 1e-6);
        corner.Y.ShouldBe(fixedCorner.Y, 1e-6);
        callout.Tail.ShouldBe(new PointD(60, 120));
    }

    [StaFact]
    public void GedrehteBlase_GriffeSindMitgedreht()
    {
        var callout = new CalloutAnnotation { Rect = new RectD(40, 30, 100, 50), Tail = new PointD(60, 120), Rotation = 90 };

        var handles = SelectionHandles.Get([callout], 26);

        handles.Count.ShouldBe(10);
        var bottomRight = handles.Single(x => x.Kind == HandleKind.BottomRight).Position;
        var expected = RotationMath.Rotate(new PointD(140, 80), callout.Center, 90);
        bottomRight.X.ShouldBe(expected.X, 1e-9);
        bottomRight.Y.ShouldBe(expected.Y, 1e-9);
        handles.ShouldContain(x => x.Kind == HandleKind.CalloutTail && x.Position == callout.Tail);
    }

    [StaFact]
    public void Panel_Drehung_NurMitAuswahl_MitUndo_KeineVorgabe()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<CalloutTool>());
        var panel = h.ViewModel.CalloutProperties;
        panel.ShowRotation.ShouldBeFalse();

        var callout = AddCallout(h);
        h.Context.Selection.Set(callout);
        panel.ShowRotation.ShouldBeTrue();
        panel.Rotation = -30;

        callout.Rotation.ShouldBe(330);
        panel.Rotation.ShouldBe(-30);
        panel.RotationValue.ShouldBe(-30);

        h.Document.Undo.Undo();
        callout.Rotation.ShouldBe(0);

        callout.Rotation = 45;
        panel.ResetRotationCommand.Execute(null);
        callout.Rotation.ShouldBe(0);
    }

    [StaFact]
    public void Panel_BiegungUmkehren_WirdVorgabeUndVorlage()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<CalloutTool>());
        var panel = h.ViewModel.CalloutProperties;

        panel.TailCurved = true;
        panel.TailCurveReversed = true;

        h.Settings.Current.CalloutDefaults.TailCurveReversed.ShouldBeTrue();
        var callout = new CalloutAnnotation();
        StyleApplier.Apply(callout, h.Context.Callout.Style);
        callout.TailCurveReversed.ShouldBeTrue();
    }

    [StaFact]
    public void Tasten_EckigeKlammernDrehenBlase()
    {
        var h = new EditorHarness();
        var callout = AddCallout(h);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(callout);

        select.OnKeyDown(System.Windows.Input.Key.OemCloseBrackets, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();

        callout.Rotation.ShouldBe(ShapeGeometry.AngleStepDegrees);
        callout.Tail.ShouldBe(new PointD(60, 120));
    }
}
