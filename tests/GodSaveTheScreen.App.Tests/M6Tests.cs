#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

public sealed class TextLayoutTests
{
    private static TextAnnotation NewText(string text, double wrap = 0) => new()
    {
        Text = text,
        Position = new PointD(10, 10),
        FontFamilyName = "Segoe UI",
        FontSize = 24,
        WrapWidth = wrap > 0 ? wrap : null,
    };

    public TextLayoutTests() => TextLayout.Install();

    [StaFact]
    public void Bounds_MehrzeiligerText_IstHoeherAlsEinzeiliger()
    {
        var single = NewText("Hallo");
        var multi = NewText("Hallo\nWelt\nDrei");

        single.Bounds.Height.ShouldBeGreaterThan(24);
        multi.Bounds.Height.ShouldBe(single.Bounds.Height * 3, single.Bounds.Height * 0.15);
    }

    [StaFact]
    public void Bounds_Umbruch_BegrenztBreiteUndVergroessertHoehe()
    {
        var text = "Ein ziemlich langer Text der umbrochen werden muss";
        var free = NewText(text);
        var wrapped = NewText(text, wrap: 120);

        wrapped.Bounds.Width.ShouldBe(120, 1e-6);
        wrapped.Bounds.Height.ShouldBeGreaterThan(free.Bounds.Height * 1.8);
        free.Bounds.Width.ShouldBeGreaterThan(120);
    }

    [StaFact]
    public void Bounds_Kontur_VergroessertUmHalbeStaerke()
    {
        var plain = NewText("Kontur");
        var outlined = NewText("Kontur");
        outlined.Stroke = new StrokeStyle(ArgbColor.White, 8);

        outlined.Bounds.Width.ShouldBe(plain.Bounds.Width + 8, 1e-6);
        outlined.Bounds.Height.ShouldBe(plain.Bounds.Height + 8, 1e-6);
    }

    [StaFact]
    public void Bounds_SchriftgroesseAendernAktualisiertDieGroesse()
    {
        var text = NewText("Gross");
        var before = text.Bounds.Width;

        text.FontSize = 48;

        text.Bounds.Width.ShouldBeGreaterThan(before * 1.8);
    }

    [StaFact]
    public void Bounds_LeererText_HatMindestgroesse()
    {
        var text = NewText(string.Empty);
        text.Bounds.Width.ShouldBeGreaterThan(1);
        text.Bounds.Height.ShouldBeGreaterThan(1);
    }

    [StaFact]
    public void Typefaces_SegoeUi_LiefertMehrereSchnitte()
    {
        var faces = FontCatalog.GetTypefaces("Segoe UI");

        faces.Count.ShouldBeGreaterThanOrEqualTo(4);
        faces.ShouldContain(f => f.Weight == 700 && f.Style == FontStyleKind.Normal);
        faces.ShouldContain(f => f.Style == FontStyleKind.Italic);
        faces.Select(f => f.Name).ShouldAllBe(n => !string.IsNullOrWhiteSpace(n));
    }

    [StaFact]
    public void Typefaces_UnbekannteSchrift_LiefertSynthetischenSatz()
    {
        FontCatalog.GetTypefaces("Diese Schrift gibt es nicht 123").Count.ShouldBeGreaterThanOrEqualTo(4);
    }

    [StaFact]
    public void Katalog_EnthaeltSegoeUiAlphabetischUndFindetSchriften()
    {
        FontCatalog.All.Count.ShouldBeGreaterThan(10);
        FontCatalog.Exists("Segoe UI").ShouldBeTrue();
        FontCatalog.Exists("Diese Schrift gibt es nicht 123").ShouldBeFalse();
        FontCatalog.Find("segoe ui")!.Family.ShouldNotBeNull();
    }
}

public sealed class TextMarkerRenderingTests
{
    private static ImageDocument Document(int width, int height, byte value)
    {
        var buffer = new PixelBuffer(width, height);
        Array.Fill(buffer.Pixels, value);
        return new ImageDocument(buffer);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(PixelBuffer buffer, int x, int y)
    {
        var i = (y * buffer.Stride) + (x * 4);
        return (buffer.Pixels[i + 2], buffer.Pixels[i + 1], buffer.Pixels[i], buffer.Pixels[i + 3]);
    }

    [StaFact]
    public void Export_Text_HatKeinenFarbsaum_Graustufenglaettung()
    {
        var document = Document(160, 80, 0);
        document.Annotations.Add(new TextAnnotation
        {
            Text = "Hgx",
            Position = new PointD(10, 10),
            FontSize = 40,
            Fill = ArgbColor.White,
        });

        var result = new DocumentExporter().Render(document);

        var lit = 0;
        for (var i = 0; i < result.Width * result.Height; i++)
        {
            var b = result.Pixels[i * 4];
            var g = result.Pixels[(i * 4) + 1];
            var r = result.Pixels[(i * 4) + 2];
            if (r > 0)
            {
                lit++;
            }

            Math.Abs(r - g).ShouldBeLessThanOrEqualTo(2);
            Math.Abs(g - b).ShouldBeLessThanOrEqualTo(2);
        }

        lit.ShouldBeGreaterThan(100, "der Text wurde gezeichnet");
    }

    [StaFact]
    public void Export_Text_HatDiePixelhoeheDerSchriftgroesse()
    {
        // Ein großes „I“ in 100 px Schrift: die Versalhöhe von Segoe UI beträgt ca. 70 % der Schriftgröße.
        var document = Document(120, 200, 255);
        document.Annotations.Add(new TextAnnotation
        {
            Text = "I",
            Position = new PointD(20, 20),
            FontSize = 100,
            FontFamilyName = "Segoe UI",
            Fill = ArgbColor.Black,
        });

        var result = new DocumentExporter().Render(document);

        var rows = Enumerable.Range(0, result.Height).Count(y => Enumerable.Range(0, result.Width).Any(x => Pixel(result, x, y).R < 128));
        rows.ShouldBeInRange(60, 85);
    }

    [StaFact]
    public void Export_TextMitHintergrund_ZeichnetKastenMitInnenabstand()
    {
        var document = Document(200, 100, 255);
        document.Annotations.Add(new TextAnnotation
        {
            Text = "Hi",
            Position = new PointD(50, 30),
            FontSize = 24,
            Fill = ArgbColor.Black,
            Background = ArgbColor.FromRgb(255, 0, 0),
            BackgroundPadding = 10,
            BackgroundCornerRadius = 0,
        });

        var result = new DocumentExporter().Render(document);

        Pixel(result, 41, 31).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255), "im Innenabstand links oben");
        Pixel(result, 20, 20).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255), "außerhalb des Kastens");
    }

    [StaFact]
    public void Export_TextMitKontur_ZeichnetAussenkonturUmDieBuchstaben()
    {
        var document = Document(120, 100, 128);
        document.Annotations.Add(new TextAnnotation
        {
            Text = "O",
            Position = new PointD(30, 10),
            FontSize = 70,
            Fill = ArgbColor.Black,
            Stroke = new StrokeStyle(ArgbColor.White, 6),
        });

        var result = new DocumentExporter().Render(document);

        // Es gibt weiße (Kontur), schwarze (Füllung) und graue (Hintergrund) Pixel.
        var white = 0;
        var black = 0;
        for (var i = 0; i < result.Width * result.Height; i++)
        {
            var r = result.Pixels[(i * 4) + 2];
            white += r > 240 ? 1 : 0;
            black += r < 15 ? 1 : 0;
        }

        white.ShouldBeGreaterThan(200);
        black.ShouldBeGreaterThan(200);
    }

    [StaFact]
    public void Export_Marker_ZeichnetFlaecheMitZahl()
    {
        var document = Document(100, 100, 255);
        document.Annotations.Add(new MarkerAnnotation
        {
            Center = new PointD(50, 50),
            Size = 60,
            Number = 7,
            Fill = ArgbColor.FromRgb(200, 0, 0),
            TextColor = ArgbColor.White,
            Stroke = new StrokeStyle(ArgbColor.White, 0),
        });

        var result = new DocumentExporter().Render(document);

        Pixel(result, 28, 50).ShouldBe(((byte)200, (byte)0, (byte)0, (byte)255), "Fläche des Markers");
        Pixel(result, 5, 5).R.ShouldBe((byte)255, "außerhalb");
        var whiteInside = 0;
        for (var y = 35; y < 65; y++)
        {
            for (var x = 38; x < 62; x++)
            {
                whiteInside += Pixel(result, x, y).G > 200 ? 1 : 0;
            }
        }

        whiteInside.ShouldBeGreaterThan(20, "die Zahl ist sichtbar");
    }

    [StaFact]
    public void Export_MarkerMitSpitze_FuelltDenWegZumZiel()
    {
        var document = Document(200, 100, 255);
        document.Annotations.Add(new MarkerAnnotation
        {
            Center = new PointD(30, 50),
            Size = 40,
            Fill = ArgbColor.FromRgb(0, 0, 200),
            Stroke = new StrokeStyle(ArgbColor.White, 0),
            PointerTarget = new PointD(150, 50),
        });

        var result = new DocumentExporter().Render(document);

        Pixel(result, 70, 50).B.ShouldBe((byte)200, "Spitze liegt auf der Verbindungslinie");
        Pixel(result, 70, 5).R.ShouldBe((byte)255);
    }
}

public sealed class TextEditSessionTests
{
    private static TextAnnotation NewText(EditorHarness h, string text = "")
    {
        var t = new TextAnnotation { Text = text, Position = new PointD(20, 20), FontSize = 20 };
        return t;
    }

    [StaFact]
    public void NeuerText_LeerBestaetigt_WirdVerworfenOhneUndoEintrag()
    {
        var h = new EditorHarness();
        var text = NewText(h);
        h.Context.BeginTextEdit(text, isNew: true);
        h.Context.Interaction.Hidden.ShouldBe(text);

        h.Context.CommitTextEdit("   ");

        h.Document.Annotations.ShouldBeEmpty();
        h.Document.Undo.UndoCount.ShouldBe(0);
        h.Context.Interaction.Hidden.ShouldBeNull();
        h.Context.ActiveTextEdit.ShouldBeNull();
    }

    [StaFact]
    public void NeuerText_MitInhalt_IstEinUndoSchrittUndAusgewaehlt()
    {
        var h = new EditorHarness();
        var text = NewText(h);
        h.Context.BeginTextEdit(text, isNew: true);

        h.Context.CommitTextEdit("Hallo");

        h.Document.Annotations.ShouldHaveSingleItem().ShouldBe(text);
        text.Text.ShouldBe("Hallo");
        text.Name.ShouldNotBeNullOrEmpty();
        h.Document.Undo.UndoCount.ShouldBe(1);
        h.Context.Selection.Items.ShouldBe([text]);

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBeEmpty();
    }

    [StaFact]
    public void BestehenderText_Bearbeiten_IstGenauEineUndoAktion()
    {
        var h = new EditorHarness();
        var text = NewText(h, "alt");
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        var before = h.Document.Undo.UndoCount;

        h.Context.BeginTextEdit(text, isNew: false);
        h.Context.CommitTextEdit("neu");

        text.Text.ShouldBe("neu");
        h.Document.Undo.UndoCount.ShouldBe(before + 1);
        h.Document.Undo.Undo();
        text.Text.ShouldBe("alt");
        h.Document.Undo.Redo();
        text.Text.ShouldBe("neu");
    }

    [StaFact]
    public void BestehenderText_UnveraenderterInhalt_ErzeugtKeinenUndoEintrag()
    {
        var h = new EditorHarness();
        var text = NewText(h, "gleich");
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        var before = h.Document.Undo.UndoCount;

        h.Context.BeginTextEdit(text, isNew: false);
        h.Context.CommitTextEdit("gleich");

        h.Document.Undo.UndoCount.ShouldBe(before);
    }

    [StaFact]
    public void BestehenderText_LeerBestaetigt_LoeschtObjektUndIstRueckgaengig()
    {
        var h = new EditorHarness();
        var text = NewText(h, "weg");
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));

        h.Context.BeginTextEdit(text, isNew: false);
        h.Context.CommitTextEdit(string.Empty);

        h.Document.Annotations.ShouldBeEmpty();
        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldContain(text);
        text.Text.ShouldBe("weg");
    }

    [StaFact]
    public void Abbrechen_LaesstDasDokumentUnveraendert()
    {
        var h = new EditorHarness();
        var text = NewText(h, "x");
        h.Context.BeginTextEdit(text, isNew: true);

        h.Context.CancelTextEdit();

        h.Document.Annotations.ShouldBeEmpty();
        h.Context.Interaction.Hidden.ShouldBeNull();
    }

    [StaFact]
    public void Ereignisse_SindPaarweise()
    {
        var h = new EditorHarness();
        var started = 0;
        var ended = 0;
        h.Context.TextEditStarted += (_, _) => started++;
        h.Context.TextEditEnded += (_, _) => ended++;

        h.Context.BeginTextEdit(NewText(h, "a"), isNew: true);
        h.Context.CommitTextEdit("b");

        started.ShouldBe(1);
        ended.ShouldBe(1);
    }
}

public sealed class TextToolTests
{
    [StaFact]
    public void Klick_StartetSofortDieBearbeitungMitStandardwerten()
    {
        var h = new EditorHarness();
        h.Defaults.SetText(new GodSaveTheScreen.Core.Settings.TextStyle { FontFamilyName = "Arial", FontSize = 33, FontWeight = 700 });
        var tool = h.Tools.Get<TextTool>();

        tool.OnPointerDown(EditorHarness.Pointer(40, 50));
        tool.OnPointerUp(EditorHarness.Pointer(40, 50));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeTrue();
        var annotation = session.Annotation.ShouldBeOfType<TextAnnotation>();
        annotation.Position.ShouldBe(new PointD(40, 50));
        annotation.FontFamilyName.ShouldBe("Arial");
        annotation.FontSize.ShouldBe(33);
        annotation.WrapWidth.ShouldBeNull();

        h.Context.CommitTextEdit("Beschriftung");
        h.Document.Annotations.Count.ShouldBe(1);
    }

    [StaFact]
    public void Ziehen_ErzeugtTextboxMitUmbruchbreite()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<TextTool>();

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerMove(EditorHarness.Pointer(120, 60));
        h.Context.Interaction.RubberBand.ShouldNotBeNull();
        tool.OnPointerUp(EditorHarness.Pointer(120, 60));

        var text = h.Context.ActiveTextEdit!.Annotation.ShouldBeOfType<TextAnnotation>();
        text.WrapWidth.ShouldBe(100);
        text.Position.ShouldBe(new PointD(20, 20));
        h.Context.Interaction.RubberBand.ShouldBeNull();
    }

    [StaFact]
    public void KlickAufVorhandenenText_BearbeitetIhn()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "da", Position = new PointD(30, 30), FontSize = 20 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        var tool = h.Tools.Get<TextTool>();

        tool.OnPointerDown(EditorHarness.Pointer(35, 40));

        var session = h.Context.ActiveTextEdit.ShouldNotBeNull();
        session.IsNew.ShouldBeFalse();
        session.Annotation.ShouldBe(text);
    }

    [StaFact]
    public void Doppelklick_MitAuswahlwerkzeug_StartetBearbeitung()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "edit", Position = new PointD(30, 30), FontSize = 20 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        var tool = h.Tools.Get<SelectTool>();

        tool.OnPointerDown(EditorHarness.Pointer(35, 40, clicks: 2));

        h.Context.ActiveTextEdit.ShouldNotBeNull().Annotation.ShouldBe(text);
        h.Context.Selection.Items.ShouldBe([text]);
    }

    [StaFact]
    public void SeitengriffZiehen_AendertDieUmbruchbreite()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "Ein Text", Position = new PointD(30, 30), FontSize = 20, WrapWidth = 100 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        var tool = h.Tools.Get<SelectTool>();
        var right = text.Bounds.Right;
        var middle = text.Bounds.Y + (text.Bounds.Height / 2);

        tool.OnPointerDown(EditorHarness.Pointer(right, middle));
        tool.OnPointerMove(EditorHarness.Pointer(right + 50, middle));
        tool.OnPointerUp(EditorHarness.Pointer(right + 50, middle));

        text.WrapWidth.ShouldNotBeNull().ShouldBe(150, 1e-6);
        text.FontSize.ShouldBe(20);
        h.Document.Undo.Undo();
        text.WrapWidth.ShouldNotBeNull().ShouldBe(100, 1e-6);
    }

    [StaFact]
    public void EckgriffZiehen_SkaliertSchriftgroesseProportional()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "Gross", Position = new PointD(30, 30), FontSize = 20 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        var tool = h.Tools.Get<SelectTool>();
        var corner = new PointD(text.Bounds.Right, text.Bounds.Bottom);
        var width = text.Bounds.Width;

        tool.OnPointerDown(EditorHarness.Pointer(corner.X, corner.Y));
        tool.OnPointerMove(EditorHarness.Pointer(corner.X + width, corner.Y + width));
        tool.OnPointerUp(EditorHarness.Pointer(corner.X + width, corner.Y + width));

        text.FontSize.ShouldBeGreaterThan(30);
    }
}

public sealed class MarkerToolTests
{
    [StaFact]
    public void Klick_SetztMarkerMitFortlaufenderNummer()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MarkerTool>();

        foreach (var x in new double[] { 20, 80, 140 })
        {
            tool.OnPointerDown(EditorHarness.Pointer(x, 50));
            tool.OnPointerUp(EditorHarness.Pointer(x, 50));
        }

        h.Document.Annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ShouldBe([1, 2, 3]);
        h.Document.Undo.UndoCount.ShouldBe(3);
    }

    [StaFact]
    public void NaechsteNummer_IstMaxPlusEins_AuchNachLoeschen()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MarkerTool>();
        foreach (var x in new double[] { 20, 80, 140 })
        {
            tool.OnPointerDown(EditorHarness.Pointer(x, 50));
            tool.OnPointerUp(EditorHarness.Pointer(x, 50));
        }

        h.Document.Annotations.Remove(h.Document.Annotations[1]); // Marker 2 entfernen: Lücke
        tool.OnPointerDown(EditorHarness.Pointer(180, 50));
        tool.OnPointerUp(EditorHarness.Pointer(180, 50));

        h.Document.Annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ShouldBe([1, 3, 4]);
    }

    [StaFact]
    public void Startnummer_GiltFuerDenErstenMarker()
    {
        var h = new EditorHarness();
        h.Defaults.MarkerStartNumber = 5;
        var tool = h.Tools.Get<MarkerTool>();

        tool.OnPointerDown(EditorHarness.Pointer(20, 50));
        tool.OnPointerUp(EditorHarness.Pointer(20, 50));

        h.Document.Annotations.OfType<MarkerAnnotation>().Single().Number.ShouldBe(5);
    }

    [StaFact]
    public void Ziehen_ErzeugtSpitzeZumZielpunkt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MarkerTool>();

        tool.OnPointerDown(EditorHarness.Pointer(30, 30));
        tool.OnPointerMove(EditorHarness.Pointer(100, 80));
        h.Context.Interaction.Preview.ShouldBeOfType<MarkerAnnotation>();
        tool.OnPointerUp(EditorHarness.Pointer(100, 80));

        var marker = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<MarkerAnnotation>();
        marker.PointerTarget.ShouldBe(new PointD(100, 80));
        marker.Center.ShouldBe(new PointD(30, 30));
    }

    [StaFact]
    public void KurzerZug_ErzeugtKeineSpitze()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MarkerTool>();

        tool.OnPointerDown(EditorHarness.Pointer(30, 30));
        tool.OnPointerUp(EditorHarness.Pointer(33, 32));

        h.Document.Annotations.OfType<MarkerAnnotation>().Single().PointerTarget.ShouldBeNull();
    }

    [StaFact]
    public void Neunummerieren_NachLoeschenLueckenlos_EinUndoSchritt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MarkerTool>();
        foreach (var x in new double[] { 20, 80, 140, 200 })
        {
            tool.OnPointerDown(EditorHarness.Pointer(x, 50));
            tool.OnPointerUp(EditorHarness.Pointer(x, 50));
        }

        h.Document.Annotations.Remove(h.Document.Annotations[1]);
        h.Document.Annotations.Remove(h.Document.Annotations[1]);
        h.Document.Annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ShouldBe([1, 4]);
        var before = h.Document.Undo.UndoCount;

        h.ViewModel.MarkerProperties.RenumberCommand.Execute(MarkerOrder.ZOrder);

        h.Document.Annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ShouldBe([1, 2]);
        h.Document.Undo.UndoCount.ShouldBe(before + 1);
        h.Document.Undo.Undo();
        h.Document.Annotations.OfType<MarkerAnnotation>().Select(m => m.Number).ShouldBe([1, 4]);
    }

    [StaFact]
    public void Eigenschaften_NummerUndFormAendernUndStandardwerte()
    {
        var h = new EditorHarness();
        var marker = new MarkerAnnotation { Center = new PointD(50, 50), Number = 1 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, marker));
        h.Context.Selection.Set(marker);

        h.ViewModel.MarkerProperties.IsApplicable.ShouldBeTrue();
        h.ViewModel.MarkerProperties.Number = 9;
        h.ViewModel.MarkerProperties.Shape = MarkerShape.Square;
        marker.Number.ShouldBe(9);
        marker.Shape.ShouldBe(MarkerShape.Square);

        h.Context.Selection.Clear();
        h.ViewModel.Activate(h.Tools.Get<MarkerTool>());
        h.ViewModel.MarkerProperties.Size = 48;
        h.Defaults.GetMarker().Size.ShouldBe(48);
    }
}

public sealed class TextPropertiesTests
{
    private static (EditorHarness Harness, TextAnnotation Text) WithText()
    {
        var h = new EditorHarness();
        var text = new TextAnnotation { Text = "abc", Position = new PointD(10, 10), FontFamilyName = "Segoe UI", FontSize = 20 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        return (h, text);
    }

    [StaFact]
    public void FettKursivUnterstrichen_SchaltenSchnitteUndDekorationen()
    {
        var (h, text) = WithText();
        var props = h.ViewModel.TextProperties;

        props.IsBold = true;
        props.IsItalic = true;
        props.IsUnderline = true;
        props.IsStrikethrough = true;

        text.FontWeight.ShouldBe(700);
        text.FontStyle.ShouldBe(FontStyleKind.Italic);
        text.Decorations.ShouldBe(TextDecorationKind.Underline | TextDecorationKind.Strikethrough);

        props.IsUnderline = false;
        text.Decorations.ShouldBe(TextDecorationKind.Strikethrough);
        props.IsBold.ShouldBeTrue();
    }

    [StaFact]
    public void Schriftart_WirdAlsZuletztVerwendetGemerkt()
    {
        var (h, text) = WithText();

        h.ViewModel.TextProperties.FontFamilyName = "Consolas";

        text.FontFamilyName.ShouldBe("Consolas");
        h.Defaults.RecentFonts[0].ShouldBe("Consolas");
        h.ViewModel.TextProperties.Fonts[0].Key.ShouldBe("Consolas");
    }

    [StaFact]
    public void Schriftgroesse_WirdUebernommenUndBegrenzt()
    {
        var (h, text) = WithText();

        h.ViewModel.TextProperties.FontSize = 36.5;
        text.FontSize.ShouldBe(36.5);

        h.ViewModel.TextProperties.FontSize = 5000;
        text.FontSize.ShouldBe(999);
    }

    [StaFact]
    public void Schnitt_SetztGewichtStilUndBreiteGemeinsam_EinUndoSchritt()
    {
        var (h, text) = WithText();
        var faces = h.ViewModel.TextProperties.Typefaces;
        var boldItalic = faces.First(f => f.Weight == 700 && f.Style == FontStyleKind.Italic);
        var before = h.Document.Undo.UndoCount;

        h.ViewModel.TextProperties.SelectedTypeface = boldItalic;

        text.FontWeight.ShouldBe(700);
        text.FontStyle.ShouldBe(FontStyleKind.Italic);
        h.Document.Undo.UndoCount.ShouldBe(before + 1);
        h.ViewModel.TextProperties.SelectedTypeface.ShouldNotBeNull().Name.ShouldBe(boldItalic.Name);
    }

    [StaFact]
    public void Farben_KonturUndHintergrund_InklusiveTransparenz()
    {
        var (h, text) = WithText();
        var props = h.ViewModel.TextProperties;

        props.TextColor = new ArgbColor(128, 10, 20, 30);
        props.OutlineColor = ArgbColor.White;
        props.OutlineThickness = 5;
        props.BackgroundColor = new ArgbColor(100, 255, 255, 0);

        text.Fill.ShouldBe(new ArgbColor(128, 10, 20, 30));
        text.Stroke.ShouldBe(new StrokeStyle(ArgbColor.White, 5));
        text.Background.ShouldBe(new ArgbColor(100, 255, 255, 0));
    }

    [StaFact]
    public void Ausrichtung_UndGemischteWerte()
    {
        var (h, text) = WithText();
        var second = new TextAnnotation { Text = "zwei", Position = new PointD(10, 50), FontSize = 40 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, second));
        h.Context.Selection.Set([text, second]);
        var props = h.ViewModel.TextProperties;

        props.FontSize.ShouldBeNull("unterschiedliche Größen sind gemischt");
        props.IsAlignCenter = true;

        text.TextAlignment.ShouldBe(TextAlignmentKind.Center);
        second.TextAlignment.ShouldBe(TextAlignmentKind.Center);
    }

    [StaFact]
    public void Vorlage_UebernimmtGanzenStilAlsEineUndoAktion()
    {
        var (h, text) = WithText();
        text.FontFamilyName = "Consolas";
        var presets = h.ViewModel.Presets;
        var warning = presets.Items.First(p => p.Preset.Text!.Stroke.Thickness > 0);

        presets.ApplyCommand.Execute(warning);

        text.FontFamilyName.ShouldBe(warning.Preset.Text!.FontFamilyName);
        text.Stroke.Thickness.ShouldBe(warning.Preset.Text!.Stroke.Thickness);
        text.FontWeight.ShouldBe(warning.Preset.Text!.FontWeight);
        h.Document.Undo.Undo();
        text.Stroke.Thickness.ShouldBe(0);
        text.FontFamilyName.ShouldBe("Consolas");
    }

    [StaFact]
    public void OhneAuswahl_BearbeitetDasPanelDieTextStandardwerte()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<TextTool>());
        var props = h.ViewModel.TextProperties;

        props.IsApplicable.ShouldBeTrue();
        props.FontFamilyName = "Arial";
        props.IsBold = true;

        h.Defaults.GetText().FontFamilyName.ShouldBe("Arial");
        h.Defaults.GetText().FontWeight.ShouldBe(700);
        h.Document.Undo.UndoCount.ShouldBe(0);
    }

    [StaFact]
    public void FehlendeSchrift_WirdAlsFehlendGemeldet()
    {
        var (h, text) = WithText();
        text.FontFamilyName = "Diese Schrift gibt es nicht 123";

        h.ViewModel.TextProperties.Refresh();

        h.ViewModel.TextProperties.IsFontMissing.ShouldBeTrue();
        h.ViewModel.TextProperties.FontFamilyName.ShouldBe("Diese Schrift gibt es nicht 123");
    }

    [StaFact]
    public void Panel_ZeigtPassendeAbschnitteJeNachAuswahl()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(0, 0, 10, 10);
        var text = new TextAnnotation { Text = "t", Position = new PointD(50, 50) };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));

        h.Context.Selection.Set(rect);
        h.ViewModel.Properties.IsApplicable.ShouldBeTrue();
        h.ViewModel.TextProperties.IsApplicable.ShouldBeFalse();

        h.Context.Selection.Set(text);
        h.ViewModel.Properties.IsApplicable.ShouldBeFalse();
        h.ViewModel.TextProperties.IsApplicable.ShouldBeTrue();
        h.ViewModel.Properties.ShowOpacity.ShouldBeTrue("Deckkraft gilt für alle Objekte");
        h.ViewModel.HasPanelContent.ShouldBeTrue();

        h.Context.Selection.Clear();
        h.ViewModel.HasPanelContent.ShouldBeFalse();
    }
}
