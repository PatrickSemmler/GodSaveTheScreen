using System.Collections.Immutable;
using System.Windows.Input;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Drehen von Formen, Polygonen, Freihandstrichen und Text: Griffe, Skalieren, Eckpunkte, Panel, Texteingabe, Darstellung.</summary>
public sealed class RotationToolTests
{
    private static T Add<T>(EditorHarness h, T annotation)
        where T : Annotation
    {
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, annotation));
        return annotation;
    }

    private static void Drag(ToolBase tool, PointD from, PointD to)
    {
        tool.OnPointerDown(EditorHarness.Pointer(from.X, from.Y));
        tool.OnPointerMove(EditorHarness.Pointer((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        tool.OnPointerMove(EditorHarness.Pointer(to.X, to.Y));
        tool.OnPointerUp(EditorHarness.Pointer(to.X, to.Y));
    }

    private static void ShouldBeNear(PointD actual, PointD expected, double tolerance = 1e-6)
    {
        actual.X.ShouldBe(expected.X, tolerance);
        actual.Y.ShouldBe(expected.Y, tolerance);
    }

    [StaFact]
    public void Rechteck_HatGedrehteGriffeUndDrehgriff()
    {
        var box = new RectangleAnnotation { Rect = new RectD(40, 30, 100, 50), Rotation = 90 };

        var handles = SelectionHandles.Get([box], 26);

        handles.Count.ShouldBe(9);
        ShouldBeNear(handles.Single(x => x.Kind == HandleKind.BottomRight).Position, RotationMath.Rotate(new PointD(140, 80), box.Center, 90));
        handles.ShouldContain(x => x.Kind == HandleKind.Rotate);
    }

    [StaFact]
    public void Drehgriff_DrehtEllipse_MitUndo()
    {
        var h = new EditorHarness();
        var ellipse = Add(h, new EllipseAnnotation { Rect = new RectD(40, 30, 100, 50) });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(ellipse);
        var rotate = SelectionHandles.Get([ellipse], 26).Single(x => x.Kind == HandleKind.Rotate);

        // Mitte (90, 55): Zeiger rechts daneben = 90°.
        Drag(select, rotate.Position, new PointD(190, 55));

        ellipse.Rotation.ShouldBe(90, 1e-6);
        ellipse.Rect.ShouldBe(new RectD(40, 30, 100, 50));
        h.Document.Undo.Undo();
        ellipse.Rotation.ShouldBe(0);
    }

    [StaFact]
    public void GedrehtesRechteck_EckgriffSkaliertImEigenenSystem_GegenueberliegendeEckeBleibt()
    {
        var h = new EditorHarness();
        var box = Add(h, new RectangleAnnotation { Rect = new RectD(40, 30, 100, 50), Rotation = 90 });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(box);
        var center = box.Center;
        var fixedCorner = RotationMath.Rotate(new PointD(40, 30), center, 90);

        // Lokal unten rechts (140, 80) → (160, 100), frei (ohne Umschalt).
        Drag(select, RotationMath.Rotate(new PointD(140, 80), center, 90), RotationMath.Rotate(new PointD(160, 100), center, 90));

        box.Rect.Width.ShouldBe(120, 1e-6);
        box.Rect.Height.ShouldBe(70, 1e-6);
        box.Rotation.ShouldBe(90);
        ShouldBeNear(box.ToWorld(box.Rect.TopLeft), fixedCorner);
    }

    [StaFact]
    public void GedrehterStrich_KantengriffSkaliertImEigenenSystem()
    {
        var h = new EditorHarness();
        var stroke = Add(h, new StrokeAnnotation
        {
            Points = ImmutableArray.Create(new StrokePoint(0, 0), new StrokePoint(100, 0), new StrokePoint(100, 40)),
            Stroke = new StrokeStyle(ArgbColor.Black, 2),
            Rotation = 90,
        });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(stroke);
        var start = stroke.ToWorld(stroke.Points[0].Point);
        var right = SelectionHandles.Get([stroke], 26).Single(x => x.Kind == HandleKind.Right);

        // Rechte Kante lokal von x = 100 auf x = 150 (im Bild nach unten, da um 90° gedreht).
        Drag(select, right.Position, stroke.ToWorld(new PointD(150, 20)));

        stroke.Rect.Width.ShouldBe(150, 1e-6);
        stroke.Rect.Height.ShouldBe(40, 1e-6);
        ShouldBeNear(stroke.ToWorld(stroke.Points[0].Point), start);
    }

    [StaFact]
    public void Polygon_GriffeSindGedrehtePunktePlusDrehgriff()
    {
        var polygon = new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(40, 0), new PointD(40, 20)], Rotation = 90 };

        var handles = SelectionHandles.Get([polygon], 26);

        handles.Count(x => x.Kind == HandleKind.PolygonPoint).ShouldBe(3);
        ShouldBeNear(handles.Single(x => x.Kind == HandleKind.PolygonPoint && x.Index == 0).Position, new PointD(30, -10));
        handles.ShouldContain(x => x.Kind == HandleKind.Rotate);
        SelectionHandles.Get([polygon]).ShouldNotContain(x => x.Kind == HandleKind.Rotate);
    }

    [StaFact]
    public void GedrehtesPolygon_EckpunktZiehen_UebrigePunkteBleibenStehen()
    {
        var h = new EditorHarness();
        var polygon = Add(h, new PolygonAnnotation { Points = [new PointD(50, 50), new PointD(150, 50), new PointD(150, 100)], Rotation = 30 });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(polygon);
        var before = polygon.WorldPoints.ToList();
        var target = new PointD(220, 160);

        Drag(select, before[2], target);

        var after = polygon.WorldPoints.ToList();
        ShouldBeNear(after[0], before[0]);
        ShouldBeNear(after[1], before[1]);
        ShouldBeNear(after[2], target);
        polygon.Rotation.ShouldBe(30);

        h.Document.Undo.Undo();
        ShouldBeNear(polygon.WorldPoints.ToList()[2], before[2]);
    }

    [StaFact]
    public void GedrehtesPolygon_DoppelklickFuegtPunktAufDerKanteEin()
    {
        var h = new EditorHarness();
        var polygon = Add(h, new PolygonAnnotation { Points = [new PointD(50, 50), new PointD(150, 50), new PointD(150, 100)], Rotation = 90 });
        var select = h.Tools.Get<SelectTool>();
        var before = polygon.WorldPoints.ToList();
        var onEdge = polygon.ToWorld(new PointD(100, 50));

        select.OnPointerDown(EditorHarness.Pointer(onEdge.X, onEdge.Y, clicks: 2));

        var after = polygon.WorldPoints.ToList();
        after.Count.ShouldBe(4);
        ShouldBeNear(after[0], before[0]);
        ShouldBeNear(after[1], onEdge);
        ShouldBeNear(after[3], before[2]);
    }

    [StaFact]
    public void Tasten_EckigeKlammernDrehenStrichUndText()
    {
        var h = new EditorHarness();
        var stroke = Add(h, new StrokeAnnotation { Points = ImmutableArray.Create(new StrokePoint(10, 10), new StrokePoint(60, 30)) });
        var text = Add(h, new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100) });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set([stroke, text]);

        select.OnKeyDown(Key.OemCloseBrackets, ModifierKeys.None).ShouldBeTrue();

        stroke.Rotation.ShouldBe(ShapeGeometry.AngleStepDegrees);
        text.Rotation.ShouldBe(ShapeGeometry.AngleStepDegrees);
    }

    [StaFact]
    public void Linie_IstNichtDrehbar()
    {
        var line = new LineAnnotation { Start = new PointD(0, 0), End = new PointD(50, 0) };

        line.ShouldNotBeAssignableTo<IRotatableAnnotation>();
        SelectionHandles.Get([line], 26).ShouldNotContain(x => x.Kind == HandleKind.Rotate);
    }

    [StaFact]
    public void GedrehterText_GriffeOhneObenUnten()
    {
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 45 };

        var handles = SelectionHandles.Get([text], 26);

        handles.ShouldNotContain(x => x.Kind == HandleKind.Top || x.Kind == HandleKind.Bottom);
        handles.ShouldContain(x => x.Kind == HandleKind.Rotate);
        ShouldBeNear(handles.Single(x => x.Kind == HandleKind.TopLeft).Position, text.ToWorld(text.Rect.TopLeft));
    }

    [StaFact]
    public void GedrehterText_RechterGriffAendertUmbruchbreite_LinkeObereEckeBleibt()
    {
        var h = new EditorHarness();
        var text = Add(h, new TextAnnotation { Text = "Ein etwas längerer Text", Position = new PointD(100, 100), Rotation = 90 });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(text);
        var corner = text.ToWorld(text.Rect.TopLeft);
        var right = SelectionHandles.Get([text], 26).Single(x => x.Kind == HandleKind.Right);

        Drag(select, right.Position, text.ToWorld(new PointD(text.TextRect.X + 60, text.Center.Y)));

        text.WrapWidth.ShouldNotBeNull().ShouldBe(60, 1e-6);
        ShouldBeNear(text.ToWorld(text.Rect.TopLeft), corner);
    }

    [StaFact]
    public void GedrehterText_EckgriffSkaliertSchrift_GegenueberliegendeEckeBleibt()
    {
        var h = new EditorHarness();
        var text = Add(h, new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 90 });
        var select = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(text);
        var corner = text.ToWorld(text.Rect.TopLeft);
        var rect = text.Rect;
        var bottomRight = SelectionHandles.Get([text], 26).Single(x => x.Kind == HandleKind.BottomRight);

        Drag(select, bottomRight.Position, text.ToWorld(new PointD(rect.X + (rect.Width * 2), rect.Y + (rect.Height * 2))));

        text.FontSize.ShouldBeGreaterThan(40);
        ShouldBeNear(text.ToWorld(text.Rect.TopLeft), corner, 1);
    }

    [StaFact]
    public void GedrehterText_BearbeitenWaechstVonDerLinkenOberenEcke_MitUndo()
    {
        var h = new EditorHarness();
        var text = Add(h, new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 30 });
        var corner = text.ToWorld(text.Rect.TopLeft);

        h.Context.BeginTextEdit(text, isNew: false);
        h.Context.CommitTextEdit("Hallo Welt\nmit zweiter Zeile");

        ShouldBeNear(text.ToWorld(text.Rect.TopLeft), corner);
        h.Document.Undo.Undo();
        text.Text.ShouldBe("Hallo");
        text.Position.ShouldBe(new PointD(100, 100));
    }

    [StaFact]
    public void GedrehterText_SchriftgroesseImPanel_LinkeObereEckeBleibt_UndoStelltLageWiederHer()
    {
        var h = new EditorHarness();
        var text = Add(h, new TextAnnotation { Text = "Hallo", Position = new PointD(100, 100), Rotation = 30 });
        h.Context.Selection.Set(text);
        var corner = text.ToWorld(text.Rect.TopLeft);

        h.ViewModel.TextProperties.FontSize = 60;

        text.FontSize.ShouldBe(60);
        ShouldBeNear(text.ToWorld(text.Rect.TopLeft), corner);
        h.Document.Undo.Undo();
        ShouldBeNear(text.Position, new PointD(100, 100));
    }

    [StaFact]
    public void Panel_Drehung_NurMitAuswahl_MitUndo_UndZuruecksetzen()
    {
        var h = new EditorHarness();
        var panel = h.ViewModel.RotationProperties;
        h.ViewModel.Activate(h.Tools.Get<SelectTool>());
        panel.IsApplicable.ShouldBeFalse();

        var box = Add(h, new TriangleAnnotation { Rect = new RectD(10, 10, 50, 40) });
        var polygon = Add(h, new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(40, 0), new PointD(0, 40)] });
        h.Context.Selection.Set([box, polygon]);
        panel.IsApplicable.ShouldBeTrue();
        panel.Rotation = -30;

        box.Rotation.ShouldBe(330);
        polygon.Rotation.ShouldBe(330);
        panel.Rotation.ShouldBe(-30);
        panel.RotationValue.ShouldBe(-30);

        h.Document.Undo.Undo();
        box.Rotation.ShouldBe(0);
        polygon.Rotation.ShouldBe(0);

        box.Rotation = 45;
        panel.Rotation.ShouldBeNull();
        panel.ResetRotationCommand.Execute(null);
        box.Rotation.ShouldBe(0);
    }

    [StaFact]
    public void Panel_Drehung_NichtFuerLinieEmojiSprechblase()
    {
        var h = new EditorHarness();
        var line = Add(h, new LineAnnotation { Start = new PointD(0, 0), End = new PointD(50, 0) });
        var emoji = Add(h, new EmojiAnnotation { Rect = new RectD(0, 0, 32, 32) });

        h.Context.Selection.Set([line, emoji]);

        h.ViewModel.RotationProperties.IsApplicable.ShouldBeFalse();
    }

    [StaFact]
    public void Render_GedrehtesRechteck_UndSchattenFaelltNachRechtsUnten()
    {
        // Schmaler Balken 100×10 um 90° gedreht: steht senkrecht durch die Mitte (100, 100).
        var bar = new RectangleAnnotation
        {
            Rect = new RectD(50, 95, 100, 10),
            Fill = ArgbColor.Black,
            Stroke = new StrokeStyle(ArgbColor.Black, 0),
            Rotation = 90,
            Shadow = new ShadowStyle(ArgbColor.FromRgb(255, 0, 0), Size: 20, Blur: 0),
        };
        var document = new ImageDocument(PixelTools.Solid(200, 200, 255, 255, 255));
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, bar));

        var result = new DocumentExporter().Render(document);

        PixelTools.Get(result, 100, 60).R.ShouldBeLessThan((byte)60);
        PixelTools.Get(result, 60, 100).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));

        // Schatten: um 20 nach rechts und unten versetzt, nicht mitgedreht.
        var shadow = PixelTools.Get(result, 120, 150);
        shadow.R.ShouldBeGreaterThan((byte)200);
        shadow.G.ShouldBeLessThan((byte)60);
    }
}
