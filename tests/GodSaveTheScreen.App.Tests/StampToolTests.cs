#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

public sealed class StampToolTests
{
    private static StampAnnotation AddStamp(EditorHarness h, string text = "GENEHMIGT", double rotation = 0)
    {
        var stamp = new StampAnnotation { Text = text, Center = new PointD(100, 75), FontSize = 20, FrameThickness = 3, Rotation = rotation, Seed = 5 };
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, stamp));
        return stamp;
    }

    [StaFact]
    public void Klick_SetztStempelMitStandardwertenUndStartetBearbeitung()
    {
        var h = new EditorHarness();
        h.Context.Stamp.Style = h.Context.Stamp.Style with { Text = "ERLEDIGT", FontSize = 30, Wear = 70, Rotation = -10 };
        var tool = h.Tools.Get<StampTool>();
        h.ViewModel.Activate(tool);

        tool.OnPointerDown(EditorHarness.Pointer(60, 40));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeTrue();
        var stamp = session.Annotation.ShouldBeOfType<StampAnnotation>();
        stamp.Text.ShouldBe("ERLEDIGT");
        stamp.Center.ShouldBe(new PointD(60, 40));
        stamp.FontSize.ShouldBe(30);
        stamp.Wear.ShouldBe(70);
        stamp.SignedRotation.ShouldBe(-10, 1e-9);
        stamp.Seed.ShouldNotBe(0);
        h.Document.Annotations.ShouldBeEmpty();

        h.Context.CommitTextEdit("GEPRÜFT\nOK");

        h.Document.Annotations.ShouldHaveSingleItem().ShouldBeSameAs(stamp);
        stamp.Text.ShouldBe("GEPRÜFT\nOK");
        stamp.Name.ShouldNotBeNullOrEmpty();
        h.Context.Selection.Items.ShouldHaveSingleItem().ShouldBeSameAs(stamp);
        h.Context.Stamp.Text.ShouldBe("GEPRÜFT\nOK");

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBeEmpty();
    }

    [StaFact]
    public void Klick_LeererTextVerwirftNeuenStempel()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<StampTool>();

        tool.OnPointerDown(EditorHarness.Pointer(60, 40));
        h.Context.CommitTextEdit("   ");

        h.Document.Annotations.ShouldBeEmpty();
        h.Document.Undo.CanUndo.ShouldBeFalse();
    }

    [StaFact]
    public void KlickAufVorhandenenStempel_BearbeitetDenText()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h);
        var tool = h.Tools.Get<StampTool>();

        tool.OnPointerDown(EditorHarness.Pointer(100, 75));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeFalse();
        session.Annotation.ShouldBeSameAs(stamp);
        h.Context.CommitTextEdit("ABGELEHNT");
        stamp.Text.ShouldBe("ABGELEHNT");
        h.Document.Annotations.Count.ShouldBe(1);

        h.Document.Undo.Undo();
        stamp.Text.ShouldBe("GENEHMIGT");
    }

    [StaFact]
    public void Doppelklick_MitAuswahlwerkzeug_BearbeitetDenText()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h);
        var select = h.Tools.Get<SelectTool>();

        select.OnPointerDown(EditorHarness.Pointer(100, 75, clicks: 2));

        h.Context.ActiveTextEdit.ShouldNotBeNull().Annotation.ShouldBeSameAs(stamp);
    }

    [StaFact]
    public void Griffe_NurEckenUndDrehgriff()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h);

        var handles = SelectionHandles.Get([stamp], 10);

        handles.Select(x => x.Kind).ShouldBe([HandleKind.TopLeft, HandleKind.TopRight, HandleKind.BottomRight, HandleKind.BottomLeft, HandleKind.Rotate], ignoreOrder: true);
    }

    [StaFact]
    public void Eckgriff_SkaliertSchriftUndRahmenProportional()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h, rotation: 0);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(stamp);
        var rect = stamp.Rect;
        var corner = new PointD(rect.Right, rect.Bottom);
        var target = new PointD(rect.X + (rect.Width * 2), rect.Y + (rect.Height * 2));

        select.OnPointerDown(EditorHarness.Pointer(corner.X, corner.Y));
        select.OnPointerMove(EditorHarness.Pointer(target.X, target.Y));
        select.OnPointerUp(EditorHarness.Pointer(target.X, target.Y));

        stamp.FontSize.ShouldBe(40, 0.01);
        stamp.FrameThickness.ShouldBe(6, 0.01);
        stamp.Rect.X.ShouldBe(rect.X, 0.5);
        stamp.Rect.Y.ShouldBe(rect.Y, 0.5);

        h.Document.Undo.Undo();
        stamp.FontSize.ShouldBe(20);
        stamp.Rect.ShouldBe(rect);
    }

    [StaFact]
    public void Eckgriff_GedrehterStempel_GegenueberliegendeEckeBleibtStehen()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h, rotation: 30);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(stamp);
        var corners = RotationMath.Corners(stamp.Rect, stamp.Rotation);
        var anchor = corners[0];
        var dragged = corners[2];
        var target = new PointD(anchor.X + ((dragged.X - anchor.X) * 1.5), anchor.Y + ((dragged.Y - anchor.Y) * 1.5));

        select.OnPointerDown(EditorHarness.Pointer(dragged.X, dragged.Y));
        select.OnPointerMove(EditorHarness.Pointer(target.X, target.Y));
        select.OnPointerUp(EditorHarness.Pointer(target.X, target.Y));

        stamp.FontSize.ShouldBe(30, 0.05);
        var after = RotationMath.Corners(stamp.Rect, stamp.Rotation)[0];
        after.X.ShouldBe(anchor.X, 0.5);
        after.Y.ShouldBe(anchor.Y, 0.5);
    }

    [StaFact]
    public void Tastatur_DrehtStempel()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h);
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(stamp);

        select.OnKeyDown(System.Windows.Input.Key.OemCloseBrackets, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();

        stamp.Rotation.ShouldBe(ShapeGeometry.AngleStepDegrees);
        h.Document.Undo.Undo();
        stamp.Rotation.ShouldBe(0);
    }

    [StaFact]
    public void Panel_OhneAuswahl_BearbeitetDieVorgaben()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<StampTool>());
        var panel = h.ViewModel.StampProperties;

        panel.IsApplicable.ShouldBeTrue();
        panel.ShowDefaults.ShouldBeTrue();
        panel.Wear = 90;
        panel.Rotation = 15;
        panel.FrameShape = StampFrameShape.Oval;
        panel.InkBlend = true;
        panel.DefaultOpacityPercent = 60;
        panel.ApplyTextCommand.Execute("WICHTIG");

        var style = h.Settings.Current.StampDefaults;
        style.Wear.ShouldBe(90);
        style.Rotation.ShouldBe(15);
        style.FrameShape.ShouldBe(StampFrameShape.Oval);
        style.InkBlend.ShouldBeTrue();
        style.Opacity.ShouldBe(0.6, 1e-9);
        style.Text.ShouldBe("WICHTIG");
        h.Document.Undo.CanUndo.ShouldBeFalse();
    }

    [StaFact]
    public void Panel_MitAuswahl_AendertStempelMitUndo()
    {
        var h = new EditorHarness();
        var stamp = AddStamp(h);
        h.Context.Selection.Set(stamp);
        var panel = h.ViewModel.StampProperties;
        panel.IsApplicable.ShouldBeTrue();
        panel.HasStamps.ShouldBeTrue();

        panel.Color = ArgbColor.FromRgb(0, 0, 255);
        panel.Rotation = -20;
        panel.ApplyTextCommand.Execute("ENTWURF");
        var seed = stamp.Seed;
        panel.RerollCommand.Execute(null);

        stamp.Color.ShouldBe(ArgbColor.FromRgb(0, 0, 255));
        stamp.SignedRotation.ShouldBe(-20, 1e-9);
        stamp.Text.ShouldBe("ENTWURF");
        stamp.Seed.ShouldNotBe(seed);
        panel.Rotation.ShouldBe(-20);
        h.Settings.Current.StampDefaults.Color.ShouldBe(ArgbColor.FromRgb(0, 0, 255));

        h.Document.Undo.Undo();
        stamp.Seed.ShouldBe(seed);
    }

    [StaFact]
    public void Renderer_AbnutzungIstDeterministischUndHaengtVomMusterAb()
    {
        StampCoverage(50, 7).ShouldBe(StampCoverage(50, 7));
        StampCoverage(50, 7).ShouldNotBe(StampCoverage(50, 8));
        StampCoverage(0, 7).ShouldBe(StampCoverage(0, 8)); // ohne Abnutzung spielt das Muster keine Rolle
    }

    [StaFact]
    public void Renderer_MehrAbnutzungEntferntMehrFarbe()
    {
        double clean = StampCoverage(0, 3).Sum();
        double worn = StampCoverage(100, 3).Sum();

        worn.ShouldBeLessThan(clean * 0.95);
        worn.ShouldBeGreaterThan(clean * 0.3);
    }

    [StaFact]
    public void Renderer_MusterIstUnabhaengigVomMassstab()
    {
        var stamp = new StampAnnotation { Text = "GENEHMIGT", FontSize = 30, Wear = 80, Seed = 42 };
        var (small, ws, hs) = StampRenderer.RasterizeCoverage(stamp, 2);
        var (large, wl, hl) = StampRenderer.RasterizeCoverage(stamp, 4);

        double ratioSmall = small.Sum() / (ws * hs);
        double ratioLarge = large.Sum() / (wl * hl);
        ratioLarge.ShouldBe(ratioSmall, ratioSmall * 0.1);
    }

    [StaFact]
    public void Export_EnthaeltStempelInStempelfarbe()
    {
        var document = new ImageDocument(WhiteImage(200, 150));
        var stamp = new StampAnnotation { Text = "OK", Center = new PointD(100, 75), FontSize = 30, Wear = 0, Color = ArgbColor.FromRgb(200, 0, 0) };
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, stamp));

        var result = new DocumentExporter().Render(document);

        var rect = stamp.Rect;
        var frame = Pixel(result, (int)(rect.X + 1.5), (int)rect.Center.Y);
        frame.R.ShouldBeGreaterThan((byte)150);
        frame.G.ShouldBeLessThan((byte)60);
        Pixel(result, 5, 5).ShouldBe(((byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void TintenLook_MultipliziertMitDemBild()
    {
        var gray = new PixelBuffer(200, 150);
        for (var i = 0; i < gray.Pixels.Length; i += 4)
        {
            gray.Pixels[i] = gray.Pixels[i + 1] = gray.Pixels[i + 2] = 128;
            gray.Pixels[i + 3] = 255;
        }

        var normal = RenderOn(gray, inkBlend: false);
        var ink = RenderOn(gray, inkBlend: true);

        normal.R.ShouldBeGreaterThan((byte)180);
        ink.R.ShouldBeLessThan((byte)120);
        ink.R.ShouldBeGreaterThan((byte)80);
    }

    private static (byte R, byte G, byte B) RenderOn(PixelBuffer background, bool inkBlend)
    {
        var document = new ImageDocument(background);
        var stamp = new StampAnnotation { Text = "OK", Center = new PointD(100, 75), FontSize = 30, FrameThickness = 6, Wear = 0, Color = ArgbColor.FromRgb(200, 0, 0), InkBlend = inkBlend };
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, stamp));
        var result = new DocumentExporter().Render(document);
        return Pixel(result, (int)(stamp.Rect.X + 3), (int)stamp.Rect.Center.Y);
    }

    private static float[] StampCoverage(double wear, int seed)
    {
        var stamp = new StampAnnotation { Text = "VERTRAULICH", FontSize = 24, Wear = wear, Seed = seed };
        return StampRenderer.RasterizeCoverage(stamp, 2).Coverage;
    }

    private static PixelBuffer WhiteImage(int width, int height)
    {
        var buffer = new PixelBuffer(width, height);
        Array.Fill(buffer.Pixels, (byte)255);
        return buffer;
    }

    private static (byte R, byte G, byte B) Pixel(PixelBuffer buffer, int x, int y)
    {
        var o = ((y * buffer.Width) + x) * 4;
        return (buffer.Pixels[o + 2], buffer.Pixels[o + 1], buffer.Pixels[o]);
    }
}
