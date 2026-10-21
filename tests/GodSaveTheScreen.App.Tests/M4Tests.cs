#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

internal static class TestDoubles
{
    public static IRecentFilesService EmptyRecents()
    {
        var recents = Substitute.For<IRecentFilesService>();
        recents.Items.Returns([]);
        return recents;
    }
}

/// <summary>Baut Kontext, Werkzeuge und ViewModel eines Editors mit gefälschten Diensten.</summary>
internal sealed class EditorHarness
{
    public EditorHarness(ImageDocument? document = null)
    {
        Document = document ?? new ImageDocument(new PixelBuffer(200, 150));
        Settings = new FakeSettings();
        Defaults = new ToolDefaultsService(Settings);
        Exporter = new DocumentExporter();
        Dialogs = Substitute.For<IDialogService>();
        Clipboard = Substitute.For<IClipboardService>();
        Codec = Substitute.For<IImageCodecService>();
        Editors = Substitute.For<IEditorService>();
        Capture = Substitute.For<ICaptureLauncher>();
        Projects = Substitute.For<IProjectService>();
        Recents = Substitute.For<IRecentFilesService>();
        Recents.Items.Returns([]);
        ImageDialogs = Substitute.For<IImageInsertDialogs>();
        ImageDialogs.PickImageFiles().Returns([]);
        Context = new EditorContext(Document, Defaults, Settings, Exporter);
        Tools = ToolRegistry.Create(Context);
        ViewModel = new EditorViewModel(Context, Tools, Dialogs, Clipboard, Codec, Exporter, Editors, Settings, Capture, Projects, Recents, NullLogger<EditorViewModel>.Instance, ImageDialogs);
    }

    public IImageInsertDialogs ImageDialogs { get; }

    public ImageDocument Document { get; }

    public FakeSettings Settings { get; }

    public ToolDefaultsService Defaults { get; }

    public DocumentExporter Exporter { get; }

    public IDialogService Dialogs { get; }

    public IClipboardService Clipboard { get; }

    public IImageCodecService Codec { get; }

    public IEditorService Editors { get; }

    public ICaptureLauncher Capture { get; }

    public IProjectService Projects { get; }

    public IRecentFilesService Recents { get; }

    public EditorContext Context { get; }

    public ToolRegistry Tools { get; }

    public EditorViewModel ViewModel { get; }

    public RectangleAnnotation AddRectangle(double x, double y, double w, double h, ArgbColor? fill = null)
    {
        var rect = new RectangleAnnotation
        {
            Rect = new RectD(x, y, w, h),
            Stroke = new StrokeStyle(ArgbColor.Red, 4),
            Fill = fill ?? ArgbColor.FromRgb(0, 0, 255),
        };
        Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(Document.Annotations, rect));
        return rect;
    }

    public static EditorPointerArgs Pointer(double x, double y, System.Windows.Input.ModifierKeys modifiers = System.Windows.Input.ModifierKeys.None, int clicks = 1) =>
        new(new PointD(x, y), modifiers, 1, clicks);
}

public sealed class ImageCodecTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-codec-" + Guid.NewGuid().ToString("N"));
    private readonly ImageCodecService _codec = new();

    public ImageCodecTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static PixelBuffer Gradient(int width = 16, int height = 12)
    {
        var buffer = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * buffer.Stride) + (x * 4);
                buffer.Pixels[i] = (byte)(x * 15);
                buffer.Pixels[i + 1] = (byte)(y * 20);
                buffer.Pixels[i + 2] = (byte)((x + y) * 8);
                buffer.Pixels[i + 3] = 255;
            }
        }

        return buffer;
    }

    private static int MaxDifference(PixelBuffer a, PixelBuffer b)
    {
        a.Width.ShouldBe(b.Width);
        a.Height.ShouldBe(b.Height);
        var max = 0;
        for (var i = 0; i < a.Width * a.Height * 4; i++)
        {
            max = Math.Max(max, Math.Abs(a.Pixels[i] - b.Pixels[i]));
        }

        return max;
    }

    [StaTheory]
    [InlineData(ImageFormatSetting.Png, 0)]
    [InlineData(ImageFormatSetting.Bmp, 0)]
    [InlineData(ImageFormatSetting.Tiff, 0)]
    [InlineData(ImageFormatSetting.Jpeg, 40)]
    [InlineData(ImageFormatSetting.Gif, 60)]
    public async Task SaveLoad_Roundtrip_ErhaeltPixelImRahmenDerToleranz(ImageFormatSetting format, int tolerance)
    {
        var original = Gradient();
        var path = Path.Combine(_directory, "bild" + ImageFormats.Extensions(format)[0]);

        await _codec.SaveAsync(original, 96, 96, path, new ImageSaveOptions(format, Quality: 100));
        var loaded = await _codec.LoadAsync(path);

        File.Exists(path).ShouldBeTrue();
        Directory.GetFiles(_directory, "*.tmp").ShouldBeEmpty("keine temporären Reste");
        MaxDifference(original, loaded.Image).ShouldBeLessThanOrEqualTo(tolerance);
        loaded.FrameCount.ShouldBe(1);
    }

    [StaFact]
    public async Task SaveLoad_JpegXrVerlustfrei()
    {
        var original = Gradient();
        var path = Path.Combine(_directory, "bild.jxr");

        await _codec.SaveAsync(original, 96, 96, path, new ImageSaveOptions(ImageFormatSetting.JpegXr, 90, JxrLossless: true));
        var loaded = await _codec.LoadAsync(path);

        MaxDifference(original, loaded.Image).ShouldBeLessThanOrEqualTo(2);
    }

    [StaFact]
    public void Encode_TiffMitJederKompression_IstLesbar()
    {
        foreach (var compression in Enum.GetValues<TiffCompressionSetting>())
        {
            var bytes = _codec.Encode(Gradient(), 96, 96, new ImageSaveOptions(ImageFormatSetting.Tiff, TiffCompression: compression));
            var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            decoder.Frames[0].PixelWidth.ShouldBe(16, compression.ToString());
        }
    }

    [StaFact]
    public void Encode_JpegOhneAlpha_VerrechnetTransparenzAufHintergrund()
    {
        var transparent = new PixelBuffer(4, 4); // komplett transparent
        var bytes = _codec.Encode(transparent, 96, 96, new ImageSaveOptions(ImageFormatSetting.Jpeg, 100));
        var decoded = DecodeBytes(bytes);
        decoded.Pixels[0].ShouldBeGreaterThan((byte)240);
        decoded.Pixels[1].ShouldBeGreaterThan((byte)240);
        decoded.Pixels[2].ShouldBeGreaterThan((byte)240);

        var custom = _codec.Encode(transparent, 96, 96, new ImageSaveOptions(ImageFormatSetting.Bmp, Background: new ArgbColor(255, 255, 0, 0)));
        var bmp = DecodeBytes(custom);
        bmp.Pixels[2].ShouldBe((byte)255);
        bmp.Pixels[1].ShouldBe((byte)0);
    }

    [StaFact]
    public void Encode_Png_BehaeltTransparenz()
    {
        var bytes = _codec.Encode(new PixelBuffer(2, 2), 96, 96, new ImageSaveOptions(ImageFormatSetting.Png));
        DecodeBytes(bytes).Pixels[3].ShouldBe((byte)0);
    }

    [StaFact]
    public void Encode_SchreibtDpiMetadaten()
    {
        var bytes = _codec.Encode(Gradient(), 144, 144, new ImageSaveOptions(ImageFormatSetting.Png));
        var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        decoder.Frames[0].DpiX.ShouldBe(144, 0.5);
    }

    [StaFact]
    public async Task Load_ExifOrientierung_DrehtDasBild()
    {
        // 8 × 4 Pixel mit Orientierung 6 (90° im Uhrzeigersinn) → geladen 4 × 8.
        var source = BitmapSource.Create(8, 4, 96, 96, PixelFormats.Bgr24, null, new byte[8 * 4 * 3], 8 * 3);
        var metadata = new BitmapMetadata("jpg");
        metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)6);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
        var path = Path.Combine(_directory, "exif.jpg");
        await using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        var loaded = await _codec.LoadAsync(path);

        loaded.Image.Width.ShouldBe(4);
        loaded.Image.Height.ShouldBe(8);
    }

    [StaFact]
    public async Task Load_MehrseitigesTiff_LiefertErstenFrameUndAnzahl()
    {
        var encoder = new TiffBitmapEncoder();
        for (var i = 0; i < 3; i++)
        {
            encoder.Frames.Add(BitmapFrame.Create(Gradient(10 + i, 10).ToBitmapSource()));
        }

        var path = Path.Combine(_directory, "multi.tif");
        await using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        var loaded = await _codec.LoadAsync(path);

        loaded.FrameCount.ShouldBe(3);
        loaded.Image.Width.ShouldBe(10);
    }

    [StaFact]
    public async Task Save_ErsetztVorhandeneDatei()
    {
        var path = Path.Combine(_directory, "x.png");
        await File.WriteAllTextAsync(path, "alt");
        await _codec.SaveAsync(Gradient(), 96, 96, path, new ImageSaveOptions(ImageFormatSetting.Png));
        new FileInfo(path).Length.ShouldBeGreaterThan(10);
    }

    private static PixelBuffer DecodeBytes(byte[] bytes)
    {
        var decoder = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return PixelBufferExtensions.FromBitmapSource(decoder.Frames[0]);
    }
}

public sealed class ImageFormatTests
{
    [Theory]
    [InlineData(".PNG", ImageFormatSetting.Png)]
    [InlineData(".jpeg", ImageFormatSetting.Jpeg)]
    [InlineData(".tif", ImageFormatSetting.Tiff)]
    [InlineData(".wdp", ImageFormatSetting.JpegXr)]
    [InlineData(".gif", ImageFormatSetting.Gif)]
    public void FromExtension_ErkenntAlleEndungen(string extension, ImageFormatSetting expected) =>
        ImageFormats.FromExtension(extension).ShouldBe(expected);

    [Fact]
    public void FromExtension_Unbekannt_LiefertNull() => ImageFormats.FromExtension(".webp").ShouldBeNull();

    [Fact]
    public void OpenExtensions_EnthaltenGaengigeFormate()
    {
        var extensions = ImageFileFilter.GetOpenExtensions();
        extensions.ShouldContain(".png");
        extensions.ShouldContain(".jpg");
        extensions.ShouldContain(".webp");
    }
}

public sealed class RendererTests
{
    private static ImageDocument WhiteDocument(int size = 40)
    {
        var buffer = new PixelBuffer(size, size);
        Array.Fill(buffer.Pixels, (byte)255);
        return new ImageDocument(buffer);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(PixelBuffer buffer, int x, int y)
    {
        var i = (y * buffer.Stride) + (x * 4);
        return (buffer.Pixels[i + 2], buffer.Pixels[i + 1], buffer.Pixels[i], buffer.Pixels[i + 3]);
    }

    [StaFact]
    public void Export_HalbtransparentesRotesRechteckAufWeiss_ErgibtHellesRot()
    {
        var document = WhiteDocument();
        document.Annotations.Add(new RectangleAnnotation
        {
            Rect = new RectD(5, 5, 30, 30),
            Fill = new ArgbColor(128, 255, 0, 0),
            Stroke = new StrokeStyle(ArgbColor.Red, 0),
        });

        var result = new DocumentExporter().Render(document);

        var (r, g, b, a) = Pixel(result, 20, 20);
        r.ShouldBe((byte)255);
        g.ShouldBeInRange((byte)125, (byte)130);
        b.ShouldBeInRange((byte)125, (byte)130);
        a.ShouldBe((byte)255);
        Pixel(result, 1, 1).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255), "außerhalb des Rechtecks unverändert");
    }

    [StaFact]
    public void Export_OhneSichtbareObjekte_LiefertDasBasisbildUnveraendert()
    {
        var document = WhiteDocument();
        var exporter = new DocumentExporter();
        exporter.Render(document).ShouldBeSameAs(document.BaseImage);

        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(0, 0, 10, 10), IsVisible = false });
        exporter.Render(document).ShouldBeSameAs(document.BaseImage);
    }

    [StaFact]
    public void Export_ObjektDeckkraftWirdBeruecksichtigt()
    {
        var document = WhiteDocument();
        document.Annotations.Add(new RectangleAnnotation
        {
            Rect = new RectD(0, 0, 40, 40),
            Fill = ArgbColor.Black,
            Stroke = new StrokeStyle(ArgbColor.Black, 0),
            Opacity = 0.5,
        });

        var (r, _, _, _) = Pixel(new DocumentExporter().Render(document), 20, 20);
        r.ShouldBeInRange((byte)125, (byte)130);
    }

    [StaFact]
    public void Export_Linie_ZeichnetStrichUndSpitze()
    {
        var document = WhiteDocument(100);
        document.Annotations.Add(new LineAnnotation
        {
            Start = new PointD(5, 50),
            End = new PointD(95, 50),
            Stroke = new StrokeStyle(ArgbColor.Black, 6),
            EndCap = LineEndStyle.Arrow,
        });

        var result = new DocumentExporter().Render(document);

        Pixel(result, 30, 50).R.ShouldBeLessThan((byte)30, "Linienmitte");
        Pixel(result, 30, 20).R.ShouldBe((byte)255, "weit neben der Linie");
        Pixel(result, 88, 50).R.ShouldBeLessThan((byte)30, "Pfeilspitze nahe dem Ende");
    }

    [StaFact]
    public void RenderObjects_ZuschneidetAufAusdehnungMitTransparentemGrund()
    {
        var exporter = new DocumentExporter();
        var rect = new RectangleAnnotation { Rect = new RectD(20, 30, 10, 10), Fill = ArgbColor.Black, Stroke = new StrokeStyle(ArgbColor.Black, 0) };

        var result = exporter.RenderObjects([rect])!;

        result.Width.ShouldBe(10);
        result.Height.ShouldBe(10);
        Pixel(result, 5, 5).A.ShouldBe((byte)255);
    }

    [Fact]
    public void LineLayout_PfeilEndetAnDerSpitzenbasis()
    {
        var layout = LineLayout.Compute(new PointD(0, 0), new PointD(100, 0), 4, LineEndStyle.None, LineEndStyle.Arrow, 4);

        // Spitzenlänge = Stärke × Faktor = 16.
        layout.LineEnd.X.ShouldBe(84, 1e-9);
        layout.EndCap.ShouldNotBeNull();
        layout.EndCap!.Points[0].ShouldBe(new PointD(100, 0));
        layout.EndCap.Points[1].X.ShouldBe(84, 1e-9);
        layout.EndCap.Points[2].X.ShouldBe(84, 1e-9);
        Math.Abs(layout.EndCap.Points[1].Y).ShouldBe(Math.Abs(layout.EndCap.Points[2].Y), 1e-9);
        layout.LineStart.ShouldBe(new PointD(0, 0));
    }

    [Fact]
    public void LineLayout_OhneSpitzen_BleibtUnveraendert()
    {
        var layout = LineLayout.Compute(new PointD(1, 2), new PointD(50, 60), 3, LineEndStyle.None, LineEndStyle.None, 4);
        layout.LineStart.ShouldBe(new PointD(1, 2));
        layout.LineEnd.ShouldBe(new PointD(50, 60));
        layout.StartCap.ShouldBeNull();
    }
}

public sealed class ShapeGeometryTests
{
    [Fact]
    public void ConstrainBox_Umschalt_ErzeugtQuadrat()
    {
        var rect = ShapeGeometry.ConstrainBox(new PointD(10, 10), new PointD(60, 30), System.Windows.Input.ModifierKeys.Shift);
        rect.Width.ShouldBe(50);
        rect.Height.ShouldBe(50);
    }

    [Fact]
    public void ConstrainBox_Alt_ZeichnetVomMittelpunkt()
    {
        var rect = ShapeGeometry.ConstrainBox(new PointD(50, 50), new PointD(60, 70), System.Windows.Input.ModifierKeys.Alt);
        rect.ShouldBe(new RectD(40, 30, 20, 40));
    }

    [Fact]
    public void ConstrainLine_Umschalt_RastetAuf15Grad()
    {
        var (start, end) = ShapeGeometry.ConstrainLine(new PointD(0, 0), new PointD(100, 3), System.Windows.Input.ModifierKeys.Shift);
        start.ShouldBe(new PointD(0, 0));
        end.Y.ShouldBe(0, 1e-9);
        end.X.ShouldBeGreaterThan(99);

        var (_, diagonal) = ShapeGeometry.ConstrainLine(new PointD(0, 0), new PointD(50, 48), System.Windows.Input.ModifierKeys.Shift);
        diagonal.X.ShouldBe(diagonal.Y, 1e-9);
    }
}

public sealed class SelectToolTests
{
    private static void Drag(ToolBase tool, double fromX, double fromY, double toX, double toY, System.Windows.Input.ModifierKeys modifiers = System.Windows.Input.ModifierKeys.None)
    {
        tool.OnPointerDown(EditorHarness.Pointer(fromX, fromY, modifiers));
        tool.OnPointerMove(EditorHarness.Pointer((fromX + toX) / 2, (fromY + toY) / 2, modifiers));
        tool.OnPointerMove(EditorHarness.Pointer(toX, toY, modifiers));
        tool.OnPointerUp(EditorHarness.Pointer(toX, toY, modifiers));
    }

    [StaFact]
    public void Klick_WaehltOberstesObjektUnterDemZeiger()
    {
        var h = new EditorHarness();
        var below = h.AddRectangle(10, 10, 80, 80);
        var above = h.AddRectangle(30, 30, 80, 80);
        var tool = h.Tools.Get<SelectTool>();

        tool.OnPointerDown(EditorHarness.Pointer(50, 50));
        tool.OnPointerUp(EditorHarness.Pointer(50, 50));

        h.Context.Selection.Items.ShouldBe([above]);
        h.Context.Selection.Contains(below).ShouldBeFalse();
    }

    [StaFact]
    public void KlickInsLeere_HebtAuswahlAuf()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 30, 30);
        h.Context.Selection.Set(rect);
        var tool = h.Tools.Get<SelectTool>();

        tool.OnPointerDown(EditorHarness.Pointer(150, 120));
        tool.OnPointerUp(EditorHarness.Pointer(150, 120));

        h.Context.Selection.IsEmpty.ShouldBeTrue();
    }

    [StaFact]
    public void Verschieben_ErzeugtGenauEineUndoAktion()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 40);
        var tool = h.Tools.Get<SelectTool>();
        var countBefore = h.Document.Undo.UndoCount;

        Drag(tool, 30, 30, 60, 55);

        rect.Rect.ShouldBe(new RectD(40, 35, 50, 40));
        h.Document.Undo.UndoCount.ShouldBe(countBefore + 1);
        h.Document.Undo.Undo();
        rect.Rect.ShouldBe(new RectD(10, 10, 50, 40));
        h.Document.Undo.Redo();
        rect.Rect.ShouldBe(new RectD(40, 35, 50, 40));
    }

    [StaFact]
    public void KlickOhneBewegung_ErzeugtKeineUndoAktion()
    {
        var h = new EditorHarness();
        h.AddRectangle(10, 10, 50, 40);
        var tool = h.Tools.Get<SelectTool>();
        var countBefore = h.Document.Undo.UndoCount;

        tool.OnPointerDown(EditorHarness.Pointer(30, 30));
        tool.OnPointerUp(EditorHarness.Pointer(30, 30));

        h.Document.Undo.UndoCount.ShouldBe(countBefore);
    }

    [StaFact]
    public void GesperrtesObjekt_LaesstSichNichtVerschieben()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 40);
        rect.IsLocked = true;
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 30, 30, 90, 90);

        rect.Rect.ShouldBe(new RectD(10, 10, 50, 40));
    }

    [StaFact]
    public void Rahmenauswahl_WaehltNurVollstaendigEnthaltene()
    {
        var h = new EditorHarness();
        var inside = h.AddRectangle(20, 20, 20, 20);
        var partly = h.AddRectangle(80, 80, 60, 60);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 5, 5, 70, 70);

        h.Context.Selection.Items.ShouldBe([inside]);
        h.Context.Selection.Contains(partly).ShouldBeFalse();
        h.Context.Interaction.RubberBand.ShouldBeNull("Rahmen nach dem Loslassen weg");
    }

    [StaFact]
    public void StrgKlick_ErweitertDieAuswahl()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(10, 10, 30, 30);
        var b = h.AddRectangle(100, 60, 30, 30);
        var tool = h.Tools.Get<SelectTool>();
        h.Context.Selection.Set(a);

        tool.OnPointerDown(EditorHarness.Pointer(110, 70, System.Windows.Input.ModifierKeys.Control));
        tool.OnPointerUp(EditorHarness.Pointer(110, 70, System.Windows.Input.ModifierKeys.Control));

        h.Context.Selection.Items.ShouldBe([a, b]);
    }

    [StaFact]
    public void EckgriffZiehen_SkaliertDasObjekt()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(20, 20, 40, 40);
        h.Context.Selection.Set(rect);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 60, 60, 100, 100); // Griff unten rechts

        rect.Rect.ShouldBe(new RectD(20, 20, 80, 80));
        h.Document.Undo.Undo();
        rect.Rect.ShouldBe(new RectD(20, 20, 40, 40));
    }

    [StaFact]
    public void KantengriffZiehen_AendertNurEineRichtung()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(20, 20, 40, 40);
        h.Context.Selection.Set(rect);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 60, 40, 90, 70); // rechter Griff (Mitte der rechten Kante)

        rect.Rect.ShouldBe(new RectD(20, 20, 70, 40));
    }

    [StaFact]
    public void MehrfachauswahlSkalierenProportionalZurGruppe()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 20, 20);
        var b = h.AddRectangle(40, 40, 20, 20);
        h.Context.Selection.Set([a, b]);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 60, 60, 120, 120); // Gruppe 60 × 60 → 120 × 120

        a.Rect.ShouldBe(new RectD(0, 0, 40, 40));
        b.Rect.ShouldBe(new RectD(80, 80, 40, 40));
    }

    [StaFact]
    public void LinienEndpunktGriff_VerschiebtNurDenEndpunkt()
    {
        var h = new EditorHarness();
        var line = new LineAnnotation { Start = new PointD(10, 10), End = new PointD(100, 10), Stroke = new StrokeStyle(ArgbColor.Red, 4) };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, line));
        h.Context.Selection.Set(line);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 100, 10, 120, 60);

        line.Start.ShouldBe(new PointD(10, 10));
        line.End.ShouldBe(new PointD(120, 60));
    }

    [StaFact]
    public void PolygonPunktGriff_VerschiebtEinenEckpunkt()
    {
        var h = new EditorHarness();
        var polygon = new PolygonAnnotation { Points = [new PointD(10, 10), new PointD(100, 10), new PointD(50, 90)] };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, polygon));
        h.Context.Selection.Set(polygon);
        var tool = h.Tools.Get<SelectTool>();

        Drag(tool, 50, 90, 60, 120);

        polygon.Points[2].ShouldBe(new PointD(60, 120));
        polygon.Points[0].ShouldBe(new PointD(10, 10));
    }

    [StaFact]
    public void EntfLoeschtAuswahlUndIstRueckgaengigMachbar()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 40);
        h.Context.Selection.Set(rect);
        var tool = h.Tools.Get<SelectTool>();

        tool.OnKeyDown(System.Windows.Input.Key.Delete, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();

        h.Document.Annotations.ShouldBeEmpty();
        h.Context.Selection.IsEmpty.ShouldBeTrue();
        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldContain(rect);
    }

    [StaFact]
    public void Pfeiltasten_VerschiebenUmEinsBzwZehn()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(10, 10, 50, 40);
        h.Context.Selection.Set(rect);
        var tool = h.Tools.Get<SelectTool>();

        tool.OnKeyDown(System.Windows.Input.Key.Right, System.Windows.Input.ModifierKeys.None);
        tool.OnKeyDown(System.Windows.Input.Key.Down, System.Windows.Input.ModifierKeys.Shift);

        rect.Rect.X.ShouldBe(11);
        rect.Rect.Y.ShouldBe(20);
    }

    [StaFact]
    public void StrgA_WaehltAlleSichtbarenObjekte()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        var hidden = h.AddRectangle(20, 20, 10, 10);
        hidden.IsVisible = false;
        var tool = h.Tools.Get<SelectTool>();

        tool.OnKeyDown(System.Windows.Input.Key.A, System.Windows.Input.ModifierKeys.Control);

        h.Context.Selection.Items.ShouldBe([a]);
    }
}

public sealed class ShapeToolTests
{
    [StaFact]
    public void Rechteck_Aufziehen_FuegtObjektMitStandardwertenEinUndWaehltEsAus()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<RectangleTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerMove(EditorHarness.Pointer(60, 50));
        h.Context.Interaction.Preview.ShouldNotBeNull("Vorschau während des Ziehens");
        tool.OnPointerUp(EditorHarness.Pointer(60, 50));

        var rect = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<RectangleAnnotation>();
        rect.Rect.ShouldBe(new RectD(10, 10, 50, 40));
        rect.Name.ShouldNotBeNullOrEmpty();
        h.Context.Selection.Items.ShouldBe([rect]);
        h.Context.Interaction.Preview.ShouldBeNull();
        h.Document.Undo.UndoCount.ShouldBe(1);
    }

    [StaFact]
    public void ReinerKlick_ErzeugtKeinObjekt()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<EllipseTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(10, 10));

        h.Document.Annotations.ShouldBeEmpty();
        h.Document.Undo.UndoCount.ShouldBe(0);
    }

    [StaFact]
    public void Pfeilwerkzeug_ErzeugtLinieMitSpitzeAmEnde()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<ArrowTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(90, 10));

        var line = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<LineAnnotation>();
        line.EndCap.ShouldBe(LineEndStyle.Arrow);
        line.StartCap.ShouldBe(LineEndStyle.None);
    }

    [StaFact]
    public void Polygon_KlickProPunkt_EnterSchliesst()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PolygonTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerDown(EditorHarness.Pointer(80, 10));
        tool.OnPointerMove(EditorHarness.Pointer(40, 70));
        tool.OnPointerDown(EditorHarness.Pointer(40, 70));
        h.Document.Annotations.ShouldBeEmpty("noch nicht abgeschlossen");

        tool.OnKeyDown(System.Windows.Input.Key.Enter, System.Windows.Input.ModifierKeys.None).ShouldBeTrue();

        var polygon = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<PolygonAnnotation>();
        polygon.Points.Length.ShouldBe(3);
        polygon.IsClosed.ShouldBeTrue();
    }

    [StaFact]
    public void Polygon_EscBrichtAb()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PolygonTool>();
        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerDown(EditorHarness.Pointer(80, 10));

        tool.OnKeyDown(System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None);

        h.Document.Annotations.ShouldBeEmpty();
        h.Context.Interaction.Preview.ShouldBeNull();
    }

    [StaFact]
    public void Polygon_DoppelklickSchliesstOhneDoppelterEckpunkte()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<PolygonTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerDown(EditorHarness.Pointer(80, 10));
        tool.OnPointerDown(EditorHarness.Pointer(40, 70));
        tool.OnPointerDown(EditorHarness.Pointer(40, 70, clicks: 2));

        h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<PolygonAnnotation>().Points.Length.ShouldBe(3);
    }

    [StaFact]
    public void Standardwerte_WerdenJeWerkzeugGespeichertUndAngewendet()
    {
        var h = new EditorHarness();
        h.Defaults.SetShape(RectangleTool.ToolId, new ShapeStyle { Stroke = new StrokeStyle(ArgbColor.FromRgb(0, 128, 0), 9), Fill = new ArgbColor(100, 1, 2, 3), Opacity = 0.6 });
        var tool = h.Tools.Get<RectangleTool>();

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(60, 60));

        var rect = (RectangleAnnotation)h.Document.Annotations.Single();
        rect.Stroke.Thickness.ShouldBe(9);
        rect.Fill.ShouldBe(new ArgbColor(100, 1, 2, 3));
        rect.Opacity.ShouldBe(0.6);
        h.Settings.Current.ShapeDefaults.ShouldContainKey(RectangleTool.ToolId);
    }

    [StaFact]
    public void NachDemZeichnen_WechselZurAuswahl_WennEingestellt()
    {
        var h = new EditorHarness();
        h.Settings.Current = h.Settings.Current with { SwitchToSelectAfterDraw = true };
        var tool = h.Tools.Get<RectangleTool>();
        h.ViewModel.Activate(tool);

        tool.OnPointerDown(EditorHarness.Pointer(10, 10));
        tool.OnPointerUp(EditorHarness.Pointer(60, 60));

        h.ViewModel.ActiveTool.ShouldBeOfType<SelectTool>();
    }

    [StaFact]
    public void StrgKlickAufVorhandenesObjekt_WaehltStattZuZeichnen()
    {
        var h = new EditorHarness();
        var existing = h.AddRectangle(10, 10, 80, 80);
        var tool = h.Tools.Get<EllipseTool>();

        tool.OnPointerDown(EditorHarness.Pointer(50, 50, System.Windows.Input.ModifierKeys.Control));
        tool.OnPointerUp(EditorHarness.Pointer(50, 50, System.Windows.Input.ModifierKeys.Control));

        h.Document.Annotations.ShouldHaveSingleItem().ShouldBe(existing);
        h.Context.Selection.Items.ShouldBe([existing]);
    }
}

public sealed class SelectionPropertiesTests
{
    [StaFact]
    public void GemischteWerte_ErscheinenAlsNull_GleicheAlsWert()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        var b = h.AddRectangle(20, 20, 10, 10);
        b.Stroke = b.Stroke with { Thickness = 9 };
        h.Context.Selection.Set([a, b]);

        h.ViewModel.Properties.Thickness.ShouldBeNull();
        h.ViewModel.Properties.StrokeColor.ShouldBe(ArgbColor.Red);
    }

    [StaFact]
    public void Aenderung_GiltFuerAlleUndIstEinUndoSchritt()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        var b = h.AddRectangle(20, 20, 10, 10);
        h.Context.Selection.Set([a, b]);
        var before = h.Document.Undo.UndoCount;

        h.ViewModel.Properties.Thickness = 12;

        a.Stroke.Thickness.ShouldBe(12);
        b.Stroke.Thickness.ShouldBe(12);
        h.Document.Undo.UndoCount.ShouldBe(before + 1);
        h.Document.Undo.Undo();
        a.Stroke.Thickness.ShouldBe(4);
        b.Stroke.Thickness.ShouldBe(4);
    }

    [StaFact]
    public void OhneAuswahl_BearbeitetPanelDieStandardwerteDesWerkzeugs()
    {
        var h = new EditorHarness();
        h.ViewModel.Activate(h.Tools.Get<RectangleTool>());

        h.ViewModel.Properties.IsDefaultsMode.ShouldBeTrue();
        h.ViewModel.Properties.StrokeColor = ArgbColor.FromRgb(1, 2, 3);
        h.ViewModel.Properties.Thickness = 7;

        var style = h.Defaults.GetShape(RectangleTool.ToolId, new ShapeStyle());
        style.Stroke.Color.ShouldBe(ArgbColor.FromRgb(1, 2, 3));
        style.Stroke.Thickness.ShouldBe(7);
        h.Document.Undo.UndoCount.ShouldBe(0, "Standardwerte sind kein Dokument-Undo");
    }

    [StaFact]
    public void Deckkraft_GiltFuerAlleObjekte()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        h.Context.Selection.Set(a);

        h.ViewModel.Properties.OpacityPercent = 40;

        a.Opacity.ShouldBe(0.4, 1e-9);
    }

    [StaFact]
    public void Eckenradius_NurFuerRechtecke()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(0, 0, 50, 50);
        h.Context.Selection.Set(rect);
        h.ViewModel.Properties.ShowCornerRadius.ShouldBeTrue();
        h.ViewModel.Properties.CornerRadius = 8;
        rect.CornerRadius.ShouldBe(8);

        var ellipse = new EllipseAnnotation { Rect = new RectD(0, 0, 5, 5) };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, ellipse));
        h.Context.Selection.Set(ellipse);
        h.ViewModel.Properties.ShowCornerRadius.ShouldBeFalse();
    }

    [StaFact]
    public void ZuletztVerwendeteFarben_MaximalZehnOhneDubletten()
    {
        var h = new EditorHarness();
        for (var i = 0; i < 12; i++)
        {
            h.Defaults.AddRecentColor(ArgbColor.FromRgb((byte)i, 0, 0));
        }

        h.Defaults.AddRecentColor(ArgbColor.FromRgb(11, 0, 0));

        h.Defaults.RecentColors.Count.ShouldBe(10);
        h.Defaults.RecentColors[0].ShouldBe(ArgbColor.FromRgb(11, 0, 0));
        h.Defaults.RecentColors.Distinct().Count().ShouldBe(10);
    }
}

public sealed class ZOrderTests
{
    private static List<Annotation> Items(int count) => [.. Enumerable.Range(0, count).Select(i => (Annotation)new RectangleAnnotation { Name = i.ToString() })];

    private static string Names(IEnumerable<Annotation> items) => string.Join(',', items.Select(a => a.Name));

    [Fact]
    public void NachVorne_VerschiebtAuswahlAnsEnde()
    {
        var items = Items(4);
        Names(ZOrder.Apply(items, [items[0], items[1]], OrderOperation.ToFront)).ShouldBe("2,3,0,1");
    }

    [Fact]
    public void NachHinten_VerschiebtAuswahlAnsEnde()
    {
        var items = Items(4);
        Names(ZOrder.Apply(items, [items[2], items[3]], OrderOperation.ToBack)).ShouldBe("2,3,0,1");
    }

    [Fact]
    public void EineEbeneVor_TauschtMitNaechstemNichtAusgewaehlten()
    {
        var items = Items(4);
        Names(ZOrder.Apply(items, [items[1]], OrderOperation.Forward)).ShouldBe("0,2,1,3");
        Names(ZOrder.Apply(items, [items[3]], OrderOperation.Forward)).ShouldBe("0,1,2,3");
        Names(ZOrder.Apply(items, [items[0], items[1]], OrderOperation.Forward)).ShouldBe("2,0,1,3");
    }

    [Fact]
    public void EineEbeneZurueck_TauschtMitVorigemNichtAusgewaehlten()
    {
        var items = Items(4);
        Names(ZOrder.Apply(items, [items[2]], OrderOperation.Backward)).ShouldBe("0,2,1,3");
        Names(ZOrder.Apply(items, [items[0]], OrderOperation.Backward)).ShouldBe("0,1,2,3");
    }
}

public sealed class EditorViewModelTests
{
    [StaFact]
    public void Zoom_FestlegenSchaltetEinpassenAus_FitSchaltetEsEin()
    {
        var h = new EditorHarness();
        h.ViewModel.IsFit.ShouldBeTrue();

        h.ViewModel.SetZoom(2);
        h.ViewModel.IsFit.ShouldBeFalse();
        h.ViewModel.Zoom.ShouldBe(2);
        h.ViewModel.ZoomText.ShouldBe("200 %");

        h.ViewModel.FitCommand.Execute(null);
        h.ViewModel.IsFit.ShouldBeTrue();
    }

    [StaFact]
    public void Zoom_WirdAufGueltigenBereichBegrenzt()
    {
        var h = new EditorHarness();
        h.ViewModel.SetZoom(1000);
        h.ViewModel.Zoom.ShouldBe(EditorViewModel.MaxZoom);
        h.ViewModel.SetZoom(0.001);
        h.ViewModel.Zoom.ShouldBe(EditorViewModel.MinZoom);
    }

    [StaFact]
    public void ZoomText_EingabeWirdGeparst()
    {
        var h = new EditorHarness();
        h.ViewModel.ZoomText = "150 %";
        h.ViewModel.Zoom.ShouldBe(1.5);
        h.ViewModel.ZoomText = "unsinn";
        h.ViewModel.Zoom.ShouldBe(1.5);
    }

    [StaFact]
    public void Aenderung_SetztDirty_SpeichernSetztEsZurueck()
    {
        var h = new EditorHarness();
        h.Document.IsDirty.ShouldBeFalse();

        h.AddRectangle(0, 0, 10, 10);
        h.Document.IsDirty.ShouldBeTrue();
        h.ViewModel.WindowTitle.ShouldContain("*");

        h.Document.MarkSaved();
        h.Document.IsDirty.ShouldBeFalse();
        h.ViewModel.WindowTitle.ShouldNotContain("*");
    }

    [StaFact]
    public async Task SchliesseOhneAenderung_FragtNicht()
    {
        var h = new EditorHarness();

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeTrue();

        h.Dialogs.DidNotReceiveWithAnyArgs().AskSaveChanges(default!, default!);
    }

    [StaFact]
    public async Task SchliesseMitAenderung_Abbrechen_BleibtOffen()
    {
        var h = new EditorHarness();
        h.AddRectangle(0, 0, 10, 10);
        h.Dialogs.AskSaveChanges(default!, default!).ReturnsForAnyArgs(SaveChangesAnswer.Cancel);

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeFalse();
    }

    [StaFact]
    public async Task SchliesseMitAenderung_NichtSpeichern_Schliesst()
    {
        var h = new EditorHarness();
        h.AddRectangle(0, 0, 10, 10);
        h.Dialogs.AskSaveChanges(default!, default!).ReturnsForAnyArgs(SaveChangesAnswer.Discard);

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeTrue();
        await h.Codec.DidNotReceiveWithAnyArgs().SaveAsync(default!, 0, 0, default!, default!);
    }

    [StaFact]
    public async Task SchliesseMitAenderung_Speichern_SchreibtDateiUndSchliesst()
    {
        var h = new EditorHarness();
        h.AddRectangle(0, 0, 10, 10);
        h.Dialogs.AskSaveChanges(default!, default!).ReturnsForAnyArgs(SaveChangesAnswer.Save);
        h.Dialogs.PickDocumentSavePath(default!, default, default, default).ReturnsForAnyArgs((@"C:\temp\test.jpg", (ImageFormatSetting?)ImageFormatSetting.Jpeg));

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeTrue();

        await h.Codec.Received(1).SaveAsync(Arg.Any<PixelBuffer>(), Arg.Any<double>(), Arg.Any<double>(), @"C:\temp\test.jpg", Arg.Is<ImageSaveOptions>(o => o.Format == ImageFormatSetting.Jpeg));
        h.Document.IsDirty.ShouldBeFalse();
        h.Document.SourcePath.ShouldBe(@"C:\temp\test.jpg");
        h.Settings.Current.LastSaveFormat.ShouldBe(ImageFormatSetting.Jpeg);
    }

    [StaFact]
    public async Task Speichern_BekanntesZiel_SchreibtDirektOhneDialog()
    {
        var h = new EditorHarness();
        h.Document.SourcePath = @"C:\temp\bekannt.png";

        await h.ViewModel.TrySaveAsync(forceDialog: false);

        h.Dialogs.DidNotReceiveWithAnyArgs().PickDocumentSavePath(default!, default, default, default);
        await h.Codec.Received(1).SaveAsync(Arg.Any<PixelBuffer>(), Arg.Any<double>(), Arg.Any<double>(), @"C:\temp\bekannt.png", Arg.Any<ImageSaveOptions>());
    }

    [StaFact]
    public async Task SpeichernUnter_FragtImmerNachPfad_Abbruch_LaesstDokumentUnveraendert()
    {
        var h = new EditorHarness();
        h.Document.SourcePath = @"C:\temp\bekannt.png";
        h.AddRectangle(0, 0, 10, 10);
        h.Dialogs.PickDocumentSavePath(default!, default, default, default).ReturnsForAnyArgs((ValueTuple<string, ImageFormatSetting?>?)null);

        (await h.ViewModel.TrySaveAsync(forceDialog: true)).ShouldBeFalse();

        h.Document.IsDirty.ShouldBeTrue();
        await h.Codec.DidNotReceiveWithAnyArgs().SaveAsync(default!, 0, 0, default!, default!);
    }

    [StaFact]
    public async Task Speichern_Fehler_ZeigtMeldungUndBleibtDirty()
    {
        var h = new EditorHarness();
        h.Document.SourcePath = @"C:\temp\bekannt.png";
        h.AddRectangle(0, 0, 10, 10);
        h.Codec.SaveAsync(default!, 0, 0, default!, default!).ReturnsForAnyArgs(Task.FromException(new IOException("voll")));

        (await h.ViewModel.TrySaveAsync(forceDialog: false)).ShouldBeFalse();

        h.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("voll")));
        h.Document.IsDirty.ShouldBeTrue();
    }

    [StaFact]
    public void KopierenOhneAuswahl_KopiertGerendertesBild()
    {
        var h = new EditorHarness();

        h.ViewModel.CopyCommand.Execute(null);

        h.Clipboard.Received(1).SetImage(Arg.Any<PixelBuffer>());
    }

    [StaFact]
    public void KopierenMitAuswahl_KopiertObjekte_StrgUmschaltC_KopiertBild()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(5, 5, 20, 20);
        h.Context.Selection.Set(rect);

        h.ViewModel.CopyCommand.Execute(null);
        h.Clipboard.Received(1).SetAnnotations(Arg.Is<IReadOnlyList<Annotation>>(l => l.Count == 1), Arg.Any<PixelBuffer?>());

        h.ViewModel.CopyImageCommand.Execute(null);
        h.Clipboard.Received(1).SetImage(Arg.Any<PixelBuffer>());
    }

    [StaFact]
    public void EinfuegenVonObjekten_VersetztUmZehnPixelUndIstUndoFaehig()
    {
        var h = new EditorHarness();
        var original = new RectangleAnnotation { Rect = new RectD(5, 5, 20, 20) };
        h.Clipboard.GetAnnotations().Returns([original]);

        h.ViewModel.PasteCommand.Execute(null);
        h.ViewModel.PasteCommand.Execute(null);

        h.Document.Annotations.Count.ShouldBe(2);
        ((RectangleAnnotation)h.Document.Annotations[0]).Rect.X.ShouldBe(15);
        ((RectangleAnnotation)h.Document.Annotations[1]).Rect.X.ShouldBe(25, "jedes weitere Einfügen versetzt weiter");
        h.Context.Selection.Items.Count.ShouldBe(1);
        h.Document.Undo.Undo();
        h.Document.Annotations.Count.ShouldBe(1);
    }

    [StaFact]
    public void EinfuegenOhneObjekte_OeffnetBildAusDerZwischenablageImNeuenEditor()
    {
        var h = new EditorHarness();
        h.Clipboard.GetAnnotations().Returns([]);

        h.ViewModel.PasteCommand.Execute(null);

        h.Editors.Received(1).OpenFromClipboard();
    }

    [StaFact]
    public void Duplizieren_UndLoeschen_UndAusschneiden()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(5, 5, 20, 20);
        h.Context.Selection.Set(rect);

        h.ViewModel.DuplicateCommand.Execute(null);
        h.Document.Annotations.Count.ShouldBe(2);
        h.Document.Annotations[1].Id.ShouldNotBe(rect.Id);

        h.ViewModel.DeleteSelectionCommand.Execute(null);
        h.Document.Annotations.Count.ShouldBe(1);

        h.Context.Selection.Set(h.Document.Annotations[0]);
        h.ViewModel.CutCommand.Execute(null);
        h.Document.Annotations.ShouldBeEmpty();
        h.Clipboard.Received().SetAnnotations(Arg.Any<IReadOnlyList<Annotation>>(), Arg.Any<PixelBuffer?>());
    }

    [StaFact]
    public void Reihenfolge_BefehleAendernDieZOrderMitUndo()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        var b = h.AddRectangle(5, 5, 10, 10);
        var c = h.AddRectangle(9, 9, 10, 10);
        h.Context.Selection.Set(a);

        h.ViewModel.BringToFrontCommand.Execute(null);
        h.Document.Annotations.ShouldBe([b, c, a]);

        h.ViewModel.SendToBackCommand.Execute(null);
        h.Document.Annotations.ShouldBe([a, b, c]);

        h.Document.Undo.Undo();
        h.Document.Annotations.ShouldBe([b, c, a]);
    }

    [StaFact]
    public void Sperren_UndEbenenliste_Umbenennen_Sichtbarkeit()
    {
        var h = new EditorHarness();
        var rect = h.AddRectangle(0, 0, 10, 10);
        h.Context.Selection.Set(rect);

        h.ViewModel.ToggleLockSelectionCommand.Execute(null);
        rect.IsLocked.ShouldBeTrue();

        var item = h.ViewModel.Layers.Items.ShouldHaveSingleItem();
        h.ViewModel.Layers.Rename(item, "Mein Kasten");
        rect.Name.ShouldBe("Mein Kasten");

        h.ViewModel.Layers.ToggleVisibilityCommand.Execute(item);
        rect.IsVisible.ShouldBeFalse();
        h.Context.Selection.IsEmpty.ShouldBeTrue("ausgeblendete Objekte verlassen die Auswahl");
        h.Document.Undo.Undo();
        rect.IsVisible.ShouldBeTrue();
    }

    [StaFact]
    public void Ebenenliste_ZeigtUmgekehrteReihenfolgeUndSortiertPerDragUm()
    {
        var h = new EditorHarness();
        var a = h.AddRectangle(0, 0, 10, 10);
        var b = h.AddRectangle(5, 5, 10, 10);
        var c = h.AddRectangle(9, 9, 10, 10);

        h.ViewModel.Layers.Items.Select(i => i.Annotation).ShouldBe([c, b, a]);

        h.ViewModel.Layers.MoveTo(a, 0); // ganz nach oben
        h.Document.Annotations.ShouldBe([b, c, a]);
        h.ViewModel.Layers.Items.Select(i => i.Annotation).ShouldBe([a, c, b]);
    }

    [StaFact]
    public void Pipette_LiefertFarbeDesGerendertenBilds_UndKehrtZumWerkzeugZurueck()
    {
        var buffer = new PixelBuffer(10, 10);
        Array.Fill(buffer.Pixels, (byte)255);
        var h = new EditorHarness(new ImageDocument(buffer));
        h.AddRectangle(0, 0, 10, 10, ArgbColor.FromRgb(0, 0, 255)).Stroke = new StrokeStyle(ArgbColor.Red, 0);
        h.ViewModel.Activate(h.Tools.Get<RectangleTool>());
        ArgbColor? picked = null;

        h.ViewModel.Properties.PipetteCommand.Execute((Action<ArgbColor>)(c => picked = c));
        h.ViewModel.ActiveTool.ShouldBeOfType<PipetteTool>();
        h.ViewModel.ActiveTool.OnPointerDown(EditorHarness.Pointer(5, 5));

        picked.ShouldNotBeNull();
        picked!.Value.B.ShouldBe((byte)255);
        picked.Value.R.ShouldBe((byte)0);
        h.ViewModel.ActiveTool.ShouldBeOfType<RectangleTool>();
    }

    [StaFact]
    public void WerkzeugKuerzel_FindenWerkzeuge()
    {
        var h = new EditorHarness();
        h.Tools.FindByKey(System.Windows.Input.Key.R).ShouldBeOfType<RectangleTool>();
        h.Tools.FindByKey(System.Windows.Input.Key.V).ShouldBeOfType<SelectTool>();
        h.Tools.FindByKey(System.Windows.Input.Key.F12).ShouldBeNull();
        h.Tools.PaletteTools.ShouldNotContain(t => t is PipetteTool);
    }

    [StaFact]
    public void Dokumentgroesse_StatusText()
    {
        var h = new EditorHarness(new ImageDocument(new PixelBuffer(321, 123)));
        h.ViewModel.ImageSizeText.ShouldContain("321");
        h.ViewModel.ImageSizeText.ShouldContain("123");
    }
}
