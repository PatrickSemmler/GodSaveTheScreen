#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.Windows.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

internal static class PixelTools
{
    public static PixelBuffer Solid(int width, int height, byte r, byte g, byte b)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Set(buffer, x, y, r, g, b);
            }
        }

        return buffer;
    }

    public static void Set(PixelBuffer buffer, int x, int y, byte r, byte g, byte b)
    {
        var i = (y * buffer.Stride) + (x * 4);
        buffer.Pixels[i] = b;
        buffer.Pixels[i + 1] = g;
        buffer.Pixels[i + 2] = r;
        buffer.Pixels[i + 3] = 255;
    }

    public static (byte R, byte G, byte B, byte A) Get(PixelBuffer buffer, int x, int y)
    {
        var i = (y * buffer.Stride) + (x * 4);
        return (buffer.Pixels[i + 2], buffer.Pixels[i + 1], buffer.Pixels[i], buffer.Pixels[i + 3]);
    }
}

public sealed class RegionEffectProcessorTests
{
    private static PixelBuffer Checkerboard(int size)
    {
        var buffer = new PixelBuffer(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var v = (byte)(((x + y) % 2) == 0 ? 0 : 255);
                PixelTools.Set(buffer, x, y, v, v, v);
            }
        }

        return buffer;
    }

    [Fact]
    public void Gauss_Schachbrett_ErgibtMittelwert()
    {
        var source = Checkerboard(60);
        var region = new PixelRect(15, 15, 30, 30);
        var result = new PixelBuffer(30, 30);

        RegionEffectProcessor.GaussianBlur(source, region, 4, result);

        for (var y = 0; y < 30; y += 7)
        {
            for (var x = 0; x < 30; x += 7)
            {
                PixelTools.Get(result, x, y).R.ShouldBeInRange((byte)120, (byte)135);
            }
        }
    }

    [Fact]
    public void Gauss_EinfarbigesBild_BleibtAuchAnDenBildraendernUnveraendert()
    {
        var source = PixelTools.Solid(40, 30, 200, 100, 50);
        var region = new PixelRect(0, 0, 40, 30);
        var result = new PixelBuffer(40, 30);

        RegionEffectProcessor.GaussianBlur(source, region, 10, result);

        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                PixelTools.Get(result, x, y).ShouldBe(((byte)200, (byte)100, (byte)50, (byte)255));
            }
        }
    }

    [Fact]
    public void Gauss_Randbehandlung_KlemmtAnBildgrenzen()
    {
        // Linke Hälfte schwarz, rechte weiß: am Bildrand darf keine Aufhellung/Abdunklung durch Außenwerte entstehen.
        var source = new PixelBuffer(40, 10);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var v = (byte)(x < 20 ? 0 : 255);
                PixelTools.Set(source, x, y, v, v, v);
            }
        }

        var result = new PixelBuffer(40, 10);
        RegionEffectProcessor.GaussianBlur(source, new PixelRect(0, 0, 40, 10), 5, result);

        PixelTools.Get(result, 0, 5).R.ShouldBe((byte)0);
        PixelTools.Get(result, 39, 5).R.ShouldBe((byte)255);
        PixelTools.Get(result, 20, 5).R.ShouldBeInRange((byte)110, (byte)145);
    }

    [Fact]
    public void Verpixeln_BlockmittelExakt()
    {
        var source = new PixelBuffer(32, 32);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                PixelTools.Set(source, x, y, (byte)(x * 8), (byte)(y * 8), 10);
            }
        }

        var result = new PixelBuffer(32, 32);
        RegionEffectProcessor.Pixelate(source, new PixelRect(0, 0, 32, 32), 8, result);

        // Block (1,1) umfasst x,y = 8..15: Mittelwert von x*8 ist 11.5*8 = 92.
        var (r, g, b, _) = PixelTools.Get(result, 10, 12);
        r.ShouldBe((byte)92);
        g.ShouldBe((byte)92);
        b.ShouldBe((byte)10);
        PixelTools.Get(result, 8, 8).ShouldBe(PixelTools.Get(result, 15, 15));
    }

    [Fact]
    public void Verpixeln_RasterIstAmBildursprungAusgerichtet_StabilBeiVerschiebungUmEinPixel()
    {
        var source = new PixelBuffer(64, 64);
        var random = new Random(7);
        random.NextBytes(source.Pixels);

        var a = new PixelBuffer(30, 30);
        var b = new PixelBuffer(30, 30);
        RegionEffectProcessor.Pixelate(source, new PixelRect(10, 10, 30, 30), 8, a);
        RegionEffectProcessor.Pixelate(source, new PixelRect(11, 11, 30, 30), 8, b);

        // Bildpixel (20,20) liegt in a bei (10,10), in b bei (9,9).
        PixelTools.Get(a, 10, 10).ShouldBe(PixelTools.Get(b, 9, 9));
        PixelTools.Get(a, 25, 12).ShouldBe(PixelTools.Get(b, 24, 11));
    }

    [Fact]
    public void Verpixeln_BlockAmBildrandMitteltNurUeberVorhandenePixel()
    {
        var source = PixelTools.Solid(20, 20, 40, 80, 120);

        var result = new PixelBuffer(20, 20);
        RegionEffectProcessor.Pixelate(source, new PixelRect(0, 0, 20, 20), 16, result);

        PixelTools.Get(result, 19, 19).ShouldBe(((byte)40, (byte)80, (byte)120, (byte)255));
    }

    [Fact]
    public void Schwaerzen_FuelltMitFarbe()
    {
        var source = PixelTools.Solid(20, 20, 10, 20, 30);
        var annotation = new BlurAnnotation { Rect = new RectD(5, 5, 10, 10), Mode = BlurMode.Redact, RedactColor = ArgbColor.FromRgb(1, 2, 3) };
        var region = RegionEffectProcessor.RegionOf(annotation, 20, 20);

        var result = RegionEffectProcessor.Process(source, region, annotation.Mode, annotation.Strength, annotation.Shape, 0, annotation.RedactColor, annotation.Rect);

        result.Width.ShouldBe(10);
        PixelTools.Get(result, 0, 0).ShouldBe(((byte)1, (byte)2, (byte)3, (byte)255));
        PixelTools.Get(result, 9, 9).ShouldBe(((byte)1, (byte)2, (byte)3, (byte)255));
    }

    [Fact]
    public void Ellipse_MaskiertDieEcken()
    {
        var source = PixelTools.Solid(40, 40, 255, 255, 255);
        var annotation = new BlurAnnotation { Rect = new RectD(0, 0, 40, 40), Mode = BlurMode.Redact, RedactColor = ArgbColor.Black, Shape = RegionShape.Ellipse };
        var region = RegionEffectProcessor.RegionOf(annotation, 40, 40);

        var result = RegionEffectProcessor.Process(source, region, annotation.Mode, 10, annotation.Shape, 0, annotation.RedactColor, annotation.Rect);

        PixelTools.Get(result, 20, 20).A.ShouldBe((byte)255);
        PixelTools.Get(result, 0, 0).A.ShouldBe((byte)0);
    }

    [Fact]
    public void WeicherRand_BlendetZumRandHinAus()
    {
        var source = PixelTools.Solid(60, 60, 255, 255, 255);
        var annotation = new BlurAnnotation { Rect = new RectD(10, 10, 40, 40), Mode = BlurMode.Redact, RedactColor = ArgbColor.Black, FeatherEdge = 10 };
        var region = RegionEffectProcessor.RegionOf(annotation, 60, 60);

        var result = RegionEffectProcessor.Process(source, region, annotation.Mode, 10, annotation.Shape, annotation.FeatherEdge, annotation.RedactColor, annotation.Rect);

        PixelTools.Get(result, 20, 20).A.ShouldBe((byte)255);
        var edge = PixelTools.Get(result, 0, 20).A;
        edge.ShouldBeGreaterThan((byte)0);
        edge.ShouldBeLessThan((byte)100);
    }

    [Fact]
    public void RegionOf_KlemmtAnDieBildgrenzen()
    {
        var annotation = new BlurAnnotation { Rect = new RectD(-20, -20, 60, 60) };

        RegionEffectProcessor.RegionOf(annotation, 30, 30).ShouldBe(new PixelRect(0, 0, 30, 30));
        RegionEffectProcessor.RegionOf(new BlurAnnotation { Rect = new RectD(100, 100, 10, 10) }, 30, 30).IsEmpty.ShouldBeTrue();
    }
}

public sealed class EffectCacheTests
{
    private static System.Windows.Media.Imaging.BitmapSource Bitmap(int size) =>
        new PixelBuffer(size, size).ToBitmapSource();

    private static EffectKey Key(int n) => new(1, new PixelRect(n, 0, 10, 10), default, BlurMode.Pixelate, 8, RegionShape.Rectangle, 0, default);

    [StaFact]
    public void GleicherSchluessel_WirdNurEinmalBerechnet()
    {
        var cache = new EffectCache();
        var calls = 0;

        var first = cache.GetOrCreate(Key(1), () =>
        {
            calls++;
            return Bitmap(10);
        });
        var second = cache.GetOrCreate(Key(1), () =>
        {
            calls++;
            return Bitmap(10);
        });

        calls.ShouldBe(1);
        second.ShouldBeSameAs(first);
        cache.Misses.ShouldBe(1);
    }

    [StaFact]
    public void Lru_VerdraengtDenAeltestenEintragBeiUeberschreitungDerGrenze()
    {
        // Jede Bitmap belegt 10*10*4 = 400 Byte; Platz für zwei.
        var cache = new EffectCache(capacityBytes: 900);

        cache.GetOrCreate(Key(1), () => Bitmap(10));
        cache.GetOrCreate(Key(2), () => Bitmap(10));
        cache.GetOrCreate(Key(1), () => Bitmap(10)); // 1 ist jetzt der jüngste Treffer
        cache.GetOrCreate(Key(3), () => Bitmap(10));

        cache.Contains(Key(1)).ShouldBeTrue();
        cache.Contains(Key(2)).ShouldBeFalse();
        cache.Contains(Key(3)).ShouldBeTrue();
        cache.Count.ShouldBe(2);
    }
}

public sealed class RegionRenderingTests
{
    private static ImageDocument Document(int width, int height, byte r = 255, byte g = 255, byte b = 255) =>
        new(PixelTools.Solid(width, height, r, g, b));

    [StaFact]
    public void Export_EnthaeltDieSchwaerzung()
    {
        var document = Document(100, 60);
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(20, 10, 40, 30), Mode = BlurMode.Redact, RedactColor = ArgbColor.Black });

        var result = new DocumentExporter().Render(document);

        PixelTools.Get(result, 30, 20).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 70, 20).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Export_UnschaerfeNutztDasBasisbildUndNichtDarunterliegendeObjekte()
    {
        var document = Document(100, 60);
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(0, 0, 100, 60), Fill = ArgbColor.FromRgb(255, 0, 0), Stroke = new StrokeStyle(ArgbColor.Red, 0) });
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(20, 10, 40, 30), Mode = BlurMode.GaussianBlur, Strength = 6 });

        var result = new DocumentExporter().Render(document);

        // Das weiße Basisbild wird weichgezeichnet und deckt das rote Rechteck an dieser Stelle ab.
        PixelTools.Get(result, 40, 25).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        PixelTools.Get(result, 80, 25).R.ShouldBe((byte)255);
        PixelTools.Get(result, 80, 25).G.ShouldBe((byte)0);
    }

    [StaFact]
    public void Export_VerpixelnVeraendertDenBereich()
    {
        var buffer = PixelTools.Solid(64, 64, 255, 255, 255);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x += 2)
            {
                PixelTools.Set(buffer, x, y, 0, 0, 0);
            }
        }

        var document = new ImageDocument(buffer);
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(0, 0, 32, 32), Mode = BlurMode.Pixelate, Strength = 8 });

        var result = new DocumentExporter().Render(document);

        // Streifenmuster wird zu Grau gemittelt; außerhalb bleibt das Muster.
        PixelTools.Get(result, 4, 4).R.ShouldBeInRange((byte)120, (byte)135);
        PixelTools.Get(result, 40, 40).R.ShouldBe((byte)0);
        PixelTools.Get(result, 41, 40).R.ShouldBe((byte)255);
    }

    [StaFact]
    public void Lupe_MitteDesZielsZeigtDenMittelpunktDerQuelle()
    {
        var buffer = PixelTools.Solid(300, 200, 0, 160, 0);
        for (var y = 48; y < 52; y++)
        {
            for (var x = 48; x < 52; x++)
            {
                PixelTools.Set(buffer, x, y, 255, 0, 0);
            }
        }

        var document = new ImageDocument(buffer);
        document.Annotations.Add(new MagnifierAnnotation
        {
            SourceRect = new RectD(40, 40, 20, 20),
            TargetCenter = new PointD(200, 100),
            Zoom = 4,
            Shape = MagnifierShape.Rectangle,
            ShowConnector = false,
            ShowSourceFrame = false,
            Border = new StrokeStyle(ArgbColor.Black, 0),
            SmoothScaling = false,
        });

        var result = new DocumentExporter().Render(document);

        PixelTools.Get(result, 200, 100).R.ShouldBe((byte)255);
        PixelTools.Get(result, 165, 65).G.ShouldBe((byte)160);
        PixelTools.Get(result, 250, 140).G.ShouldBe((byte)160);
    }

    [StaFact]
    public void Lupe_VergroessertDarunterliegendeMarkierungen_ObjektUeberDerLupeNicht()
    {
        var document = Document(300, 200);
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(45, 45, 10, 10), Fill = ArgbColor.FromRgb(0, 0, 255), Stroke = new StrokeStyle(ArgbColor.Red, 0) });
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(40, 40, 20, 20),
            TargetCenter = new PointD(200, 100),
            Zoom = 4,
            Shape = MagnifierShape.Rectangle,
            ShowConnector = false,
            ShowSourceFrame = false,
            Border = new StrokeStyle(ArgbColor.Black, 0),
        };
        document.Annotations.Add(magnifier);
        document.Annotations.Add(new EllipseAnnotation { Rect = new RectD(190, 90, 20, 20), Fill = ArgbColor.FromRgb(255, 0, 0), Stroke = new StrokeStyle(ArgbColor.Red, 0) });

        var withLayers = new DocumentExporter().Render(document);
        PixelTools.Get(withLayers, 185, 100).B.ShouldBe((byte)255);
        PixelTools.Get(withLayers, 185, 100).R.ShouldBe((byte)0);

        magnifier.BaseImageOnly = true;
        var baseOnly = new DocumentExporter().Render(document);
        PixelTools.Get(baseOnly, 185, 100).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Lupe_SieHatKeinenZugriffAufLupenDarueber()
    {
        var document = Document(300, 200);
        var lower = new MagnifierAnnotation
        {
            SourceRect = new RectD(10, 10, 30, 30),
            TargetCenter = new PointD(60, 150),
            Zoom = 2,
            Shape = MagnifierShape.Rectangle,
            ShowConnector = false,
            ShowSourceFrame = false,
            Border = new StrokeStyle(ArgbColor.Black, 0),
        };
        var upper = new MagnifierAnnotation
        {
            SourceRect = new RectD(200, 100, 40, 40),
            TargetCenter = new PointD(60, 150),
            Zoom = 2,
            Shape = MagnifierShape.Rectangle,
            ShowConnector = false,
            ShowSourceFrame = false,
            Border = new StrokeStyle(ArgbColor.Black, 0),
        };
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(0, 0, 300, 200), Fill = ArgbColor.FromRgb(0, 200, 0), Stroke = new StrokeStyle(ArgbColor.Red, 0) });
        document.Annotations.Add(lower);
        document.Annotations.Add(upper);

        // Rendert ohne Endlosrekursion; die obere Lupe überdeckt die untere am gleichen Ziel.
        var result = new DocumentExporter().Render(document);

        PixelTools.Get(result, 60, 150).G.ShouldBe((byte)200);
    }

    [StaFact]
    public void Textmarker_UeberlappungImselbenStrichBleibtGleichmaessigHell()
    {
        var document = Document(100, 100);
        document.Annotations.Add(new StrokeAnnotation
        {
            IsHighlighter = true,
            Smoothing = 0,
            Stroke = new StrokeStyle(ArgbColor.FromRgb(255, 235, 59), 10, Cap: StrokeCapKind.Square),
            Opacity = 0.4,
            Points = [new StrokePoint(10, 50), new StrokePoint(90, 50), new StrokePoint(50, 10), new StrokePoint(50, 90)],
        });

        var result = new DocumentExporter().Render(document);

        var arm = PixelTools.Get(result, 25, 50);
        var crossing = PixelTools.Get(result, 50, 50);
        crossing.ShouldBe(arm, "Überlappung innerhalb eines Strichs darf nicht dunkler werden");
        arm.B.ShouldBeLessThan((byte)255);
        arm.R.ShouldBe((byte)255);
    }

    [StaFact]
    public void Textmarker_LaesstUnterliegendenTextLesbar()
    {
        var document = Document(120, 60);
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(20, 20, 80, 20), Fill = ArgbColor.Black, Stroke = new StrokeStyle(ArgbColor.Black, 0) });
        document.Annotations.Add(new StrokeAnnotation
        {
            IsHighlighter = true,
            Smoothing = 0,
            Stroke = new StrokeStyle(ArgbColor.FromRgb(255, 235, 59), 30, Cap: StrokeCapKind.Square),
            Opacity = 0.4,
            Points = [new StrokePoint(10, 30), new StrokePoint(110, 30)],
        });

        // Mit "hinter Objekten" (Standard) bleibt das schwarze Rechteck unverändert schwarz.
        PixelTools.Get(new DocumentExporter().Render(document), 60, 30).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));

        document.HighlightersBehind = false;
        var over = PixelTools.Get(new DocumentExporter().Render(document), 60, 30);
        over.R.ShouldBeGreaterThan((byte)0);
    }

    [StaFact]
    public void Stift_MitDruck_IstBeiHoeheremDruckBreiter()
    {
        var document = Document(100, 40);
        document.Annotations.Add(new StrokeAnnotation
        {
            Smoothing = 0,
            Stroke = new StrokeStyle(ArgbColor.Black, 20),
            Points = [new StrokePoint(10, 20, 0.2), new StrokePoint(90, 20, 1.0)],
        });

        var result = new DocumentExporter().Render(document);

        var thinSide = Enumerable.Range(0, 40).Count(y => PixelTools.Get(result, 25, y).R < 128);
        var thickSide = Enumerable.Range(0, 40).Count(y => PixelTools.Get(result, 80, y).R < 128);
        thickSide.ShouldBeGreaterThan(thinSide + 5);
    }

    [StaFact]
    public void Stift_GeglaetteterStrichZeichnetDurchAllePunkte()
    {
        var document = Document(100, 100);
        document.Annotations.Add(new StrokeAnnotation
        {
            Smoothing = 1,
            Stroke = new StrokeStyle(ArgbColor.Black, 6),
            Points = [new StrokePoint(10, 10), new StrokePoint(50, 80), new StrokePoint(90, 10)],
        });

        var result = new DocumentExporter().Render(document);

        PixelTools.Get(result, 50, 80).R.ShouldBeLessThan((byte)60);
        PixelTools.Get(result, 10, 10).R.ShouldBeLessThan((byte)60);
    }

    [Fact]
    public void Reihenfolge_TextmarkerZuerstWennHinten()
    {
        var rect = new RectangleAnnotation();
        var marker = new StrokeAnnotation { IsHighlighter = true };
        var pen = new StrokeAnnotation();

        AnnotationOrdering.RenderOrder([rect, marker, pen], highlightersBehind: true).ShouldBe([marker, rect, pen]);
        AnnotationOrdering.RenderOrder([rect, marker, pen], highlightersBehind: false).ShouldBe([rect, marker, pen]);
    }
}

public sealed class FreehandToolTests
{
    [StaFact]
    public void Stift_Zeichnen_ErzeugtGenauEineAnnotationUndEinenUndoSchritt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PenTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        for (var i = 1; i <= 30; i++)
        {
            tool.OnPointerMove(EditorHarness.Pointer(10 + (i * 3), 10 + (i * i * 0.1)));
        }

        h.Context.Interaction.Preview.ShouldBeOfType<StrokeAnnotation>();
        tool.OnPointerUp(EditorHarness.Pointer(100, 100));

        h.Document.Annotations.Count.ShouldBe(1);
        var stroke = h.Document.Annotations[0].ShouldBeOfType<StrokeAnnotation>();
        stroke.IsHighlighter.ShouldBeFalse();
        stroke.Points.Length.ShouldBeGreaterThan(2);
        stroke.Points.Length.ShouldBeLessThan(32, "Punktreduktion beim Abschluss");
        h.Context.Interaction.Preview.ShouldBeNull();

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBeEmpty();
    }

    [StaFact]
    public void Umschalt_ZeichnetGeradeLinieVomStartpunkt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PenTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10, ModifierKeys.Shift));
        tool.OnPointerMove(EditorHarness.Pointer(40, 90, ModifierKeys.Shift));
        tool.OnPointerMove(EditorHarness.Pointer(20, 30, ModifierKeys.Shift));
        tool.OnPointerUp(EditorHarness.Pointer(120, 60, ModifierKeys.Shift));

        var stroke = (StrokeAnnotation)h.Document.Annotations.Single();
        stroke.Points.Length.ShouldBe(2);
        stroke.Points[0].Point.ShouldBe(new PointD(10, 10));
        stroke.Points[1].Point.ShouldBe(new PointD(120, 60));
    }

    [StaFact]
    public void StrgUmschalt_RastetDenWinkelAufFuenfzehnGradEin()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PenTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10, ModifierKeys.Shift | ModifierKeys.Control));
        tool.OnPointerMove(EditorHarness.Pointer(100, 12, ModifierKeys.Shift | ModifierKeys.Control));
        tool.OnPointerUp(EditorHarness.Pointer(100, 12, ModifierKeys.Shift | ModifierKeys.Control));

        var stroke = (StrokeAnnotation)h.Document.Annotations.Single();
        stroke.Points[1].Y.ShouldBe(10, 1e-6);
    }

    [StaFact]
    public void EinzelnerKlick_ErzeugtEinenPunkt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PenTool>();

        tool.OnPointerDown(EditorHarness.Pointer(30, 30));
        tool.OnPointerUp(EditorHarness.Pointer(30, 30));

        ((StrokeAnnotation)h.Document.Annotations.Single()).Points.Length.ShouldBe(1);
    }

    [StaFact]
    public void Lineal_StrichAnDerKanteWirdAufDieGeradeProjiziert()
    {
        var h = new EditorHarness();
        h.Context.Scale = 1;
        h.Context.Ruler.Center = new PointD(100, 75);
        h.Context.Ruler.Angle = 0;
        h.Context.Ruler.Length = 160;
        h.Context.Ruler.IsVisible = true;
        var tool = h.Tools.Get<PenTool>();
        var thickness = h.Defaults.GetShape(tool.Id, tool.BuiltInStyle).Stroke.Thickness;

        // Obere Kante liegt bei y = 75 - 40 = 35; Start 3 px darüber, danach wackelnde y-Werte.
        tool.OnPointerDown(EditorHarness.Pointer(40, 32));
        tool.OnPointerMove(EditorHarness.Pointer(70, 40));
        tool.OnPointerMove(EditorHarness.Pointer(100, 30));
        tool.OnPointerMove(EditorHarness.Pointer(130, 36));
        tool.OnPointerUp(EditorHarness.Pointer(150, 34));

        var stroke = (StrokeAnnotation)h.Document.Annotations.Single();
        foreach (var point in stroke.Points)
        {
            point.Y.ShouldBe(35 - (thickness / 2), 1e-9);
        }

        stroke.Points[^1].X.ShouldBe(150, 1e-9);
    }

    [StaFact]
    public void Lineal_UnsichtbarOderZuWeitWeg_RastetNichtEin()
    {
        var h = new EditorHarness();
        h.Context.Ruler.Center = new PointD(100, 75);
        h.Context.Ruler.IsVisible = false;
        var tool = h.Tools.Get<PenTool>();

        tool.OnPointerDown(EditorHarness.Pointer(40, 32));
        tool.OnPointerMove(EditorHarness.Pointer(70, 40));
        tool.OnPointerUp(EditorHarness.Pointer(100, 20));

        var stroke = (StrokeAnnotation)h.Document.Annotations.Single();
        stroke.Points.Select(p => p.Y).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [StaFact]
    public void Textmarker_VerwendetEigeneStandardwerte()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<HighlighterTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerMove(EditorHarness.Pointer(80, 10));
        tool.OnPointerUp(EditorHarness.Pointer(80, 10));

        var stroke = (StrokeAnnotation)h.Document.Annotations.Single();
        stroke.IsHighlighter.ShouldBeTrue();
        stroke.Opacity.ShouldBe(0.4);
        stroke.Stroke.Thickness.ShouldBe(16);
        stroke.Stroke.Cap.ShouldBe(StrokeCapKind.Square);
        stroke.Stroke.Color.ShouldBe(ArgbColor.FromRgb(0xFF, 0xEB, 0x3B));
    }

    [StaFact]
    public void Radierer_EntferntUeberstricheneStricheInEinerUndoAktion()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 100, 50, 30);
        var first = new StrokeAnnotation { Points = [new StrokePoint(20, 20), new StrokePoint(80, 20)] };
        var second = new StrokeAnnotation { Points = [new StrokePoint(20, 50), new StrokePoint(80, 50)] };
        var untouched = new StrokeAnnotation { Points = [new StrokePoint(150, 20), new StrokePoint(190, 20)] };
        foreach (var stroke in new[] { first, second, untouched })
        {
            h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, stroke));
        }

        var undoCount = h.Document.Undo.UndoCount;
        var tool = h.Tools.Get<EraserTool>();
        tool.OnPointerDown(EditorHarness.Pointer(50, 10));
        tool.OnPointerMove(EditorHarness.Pointer(50, 35));
        h.Context.Interaction.Marked.ShouldContain(first);
        h.Document.Annotations.ShouldContain(first, "erst beim Loslassen entfernen");
        tool.OnPointerMove(EditorHarness.Pointer(50, 60));
        tool.OnPointerUp(EditorHarness.Pointer(50, 60));

        h.Document.Annotations.ShouldNotContain(first);
        h.Document.Annotations.ShouldNotContain(second);
        h.Document.Annotations.ShouldContain(untouched);
        h.Document.Annotations.ShouldContain(rect, "Formen bleiben standardmäßig erhalten");
        h.Document.Undo.UndoCount.ShouldBe(undoCount + 1);
        h.Context.Interaction.Marked.ShouldBeEmpty();

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldContain(first);
        h.Document.Annotations.ShouldContain(second);
    }

    [StaFact]
    public void Radierer_AlleObjekteOption_EntferntAuchFormen()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 30);
        h.Context.EraserAllObjects = true;
        var tool = h.Tools.Get<EraserTool>();

        tool.OnPointerDown(EditorHarness.Pointer(30, 25));
        tool.OnPointerUp(EditorHarness.Pointer(30, 25));

        h.Document.Annotations.ShouldNotContain(rect);
    }
}

public sealed class RegionToolTests
{
    [StaFact]
    public void Unschaerfe_Aufziehen_ErzeugtVerpixelnAlsStandard()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<BlurTool>();

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerMove(EditorHarness.Pointer(100, 80));
        tool.OnPointerUp(EditorHarness.Pointer(100, 80));

        var blur = h.Document.Annotations.Single().ShouldBeOfType<BlurAnnotation>();
        blur.Rect.ShouldBe(new RectD(20, 20, 80, 60));
        blur.Mode.ShouldBe(BlurMode.Pixelate);
        blur.Strength.ShouldBeGreaterThanOrEqualTo(12);
        h.Context.Selection.Items.ShouldContain(blur);
    }

    [StaFact]
    public void Unschaerfe_VorlageWirdFuerNeueObjekteUebernommen()
    {
        var h = new EditorHarness();
        h.Context.BlurTemplate.Mode = BlurMode.Redact;
        h.Context.BlurTemplate.RedactColor = ArgbColor.FromRgb(10, 20, 30);
        h.Context.BlurTemplate.Shape = RegionShape.Ellipse;
        var tool = h.Tools.Get<BlurTool>();

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerUp(EditorHarness.Pointer(70, 70));

        var blur = (BlurAnnotation)h.Document.Annotations.Single();
        blur.Mode.ShouldBe(BlurMode.Redact);
        blur.RedactColor.ShouldBe(ArgbColor.FromRgb(10, 20, 30));
        blur.Shape.ShouldBe(RegionShape.Ellipse);
    }

    [StaFact]
    public void Lupe_Aufziehen_SetztQuelleUndVersetzteLupeImBild()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<MagnifierTool>();

        tool.OnPointerDown(EditorHarness.Pointer(20, 20));
        tool.OnPointerMove(EditorHarness.Pointer(50, 40));
        tool.OnPointerUp(EditorHarness.Pointer(50, 40));

        var magnifier = h.Document.Annotations.Single().ShouldBeOfType<MagnifierAnnotation>();
        magnifier.SourceRect.Width.ShouldBe(magnifier.SourceRect.Height, "runde Lupe: quadratischer Ausschnitt");
        var target = magnifier.TargetRect;
        target.X.ShouldBeGreaterThanOrEqualTo(0);
        target.Y.ShouldBeGreaterThanOrEqualTo(0);
        target.Right.ShouldBeLessThanOrEqualTo(200);
        target.Bottom.ShouldBeLessThanOrEqualTo(150);
    }

    [StaFact]
    public void Lupe_QuelleUndZielLassenSichGetrenntVerschieben()
    {
        var h = new EditorHarness(new ImageDocument(new PixelBuffer(400, 300)));
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(20, 20, 40, 40),
            TargetCenter = new PointD(250, 150),
            Zoom = 2,
        };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, magnifier));
        h.Context.Selection.Set(magnifier);
        var select = h.Tools.Get<SelectTool>();

        // Am Lupenkörper ziehen: nur das Ziel bewegt sich.
        select.OnPointerDown(EditorHarness.Pointer(250, 150));
        select.OnPointerMove(EditorHarness.Pointer(280, 170));
        select.OnPointerUp(EditorHarness.Pointer(280, 170));
        magnifier.SourceRect.ShouldBe(new RectD(20, 20, 40, 40));
        magnifier.TargetCenter.ShouldBe(new PointD(280, 170));

        // Am Quellrahmen ziehen: nur die Quelle bewegt sich.
        select.OnPointerDown(EditorHarness.Pointer(20, 40));
        select.OnPointerMove(EditorHarness.Pointer(30, 50));
        select.OnPointerUp(EditorHarness.Pointer(30, 50));
        magnifier.SourceRect.ShouldBe(new RectD(30, 30, 40, 40));
        magnifier.TargetCenter.ShouldBe(new PointD(280, 170));

        h.Document.Undo.Undo();
        magnifier.SourceRect.ShouldBe(new RectD(20, 20, 40, 40));
    }

    [StaFact]
    public void Lupe_QuellgriffeAendernNurDieQuelle()
    {
        var h = new EditorHarness(new ImageDocument(new PixelBuffer(400, 300)));
        var magnifier = new MagnifierAnnotation
        {
            SourceRect = new RectD(20, 20, 40, 40),
            TargetCenter = new PointD(250, 150),
            Zoom = 2,
            Shape = MagnifierShape.Rectangle,
        };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, magnifier));
        h.Context.Selection.Set(magnifier);
        var select = h.Tools.Get<SelectTool>();

        select.OnPointerDown(EditorHarness.Pointer(60, 60));
        select.OnPointerMove(EditorHarness.Pointer(100, 80));
        select.OnPointerUp(EditorHarness.Pointer(100, 80));

        magnifier.SourceRect.ShouldBe(new RectD(20, 20, 80, 60));
        magnifier.TargetCenter.ShouldBe(new PointD(250, 150));
    }

    [StaFact]
    public void Unschaerfe_Auswahl_LaesstSichFreiSkalieren()
    {
        var h = new EditorHarness();
        var blur = new BlurAnnotation { Rect = new RectD(20, 20, 40, 40) };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, blur));
        h.Context.Selection.Set(blur);
        var select = h.Tools.Get<SelectTool>();

        select.OnPointerDown(EditorHarness.Pointer(60, 40));
        select.OnPointerMove(EditorHarness.Pointer(100, 40));
        select.OnPointerUp(EditorHarness.Pointer(100, 40));

        blur.Rect.ShouldBe(new RectD(20, 20, 80, 40));
    }
}

public sealed class RulerViewModelTests
{
    [Theory]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    [InlineData(180, 180)]
    [InlineData(360, 0)]
    [InlineData(45, 45)]
    public void Normalize_HaeltDenWinkelImBereich(double input, double expected) =>
        RulerViewModel.Normalize(input).ShouldBe(expected);

    [Fact]
    public void Placement_BreiteIstZoomUnabhaengig()
    {
        var ruler = new RulerViewModel { Center = new PointD(50, 50), Length = 300 };

        ruler.Placement(1).Width.ShouldBe(80);
        ruler.Placement(0.5).Width.ShouldBe(160);
        ruler.Placement(4).Width.ShouldBe(20);
    }
}
