#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Bildobjekte: Darstellung (Schatten/Kontur folgen der Transparenz), SVG, Import, Einfügen und Bearbeiten.</summary>
public sealed class ImageObjectTests
{
    private static readonly (byte, byte, byte, byte) White = (255, 255, 255, 255);
    private static readonly (byte, byte, byte, byte) Blue = (0, 0, 255, 255);

    /// <summary>40 × 20: linke Hälfte transparent, rechte Hälfte deckend blau.</summary>
    private static ImageAsset HalfTransparentPng()
    {
        var buffer = new PixelBuffer(40, 20);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 20; x < 40; x++)
            {
                PixelTools.Set(buffer, x, y, 0, 0, 255);
            }
        }

        return new ImageAsset(ImageAssetFormat.Png, buffer.EncodePng());
    }

    private static ImageAnnotation Image(ImageAsset asset, RectD rect) => new() { Source = asset, Rect = rect, OriginalSize = new SizeD(rect.Width, rect.Height) };

    private static PixelBuffer Render(params Annotation[] annotations)
    {
        var document = new ImageDocument(PixelTools.Solid(100, 80, 255, 255, 255));
        foreach (var annotation in annotations)
        {
            document.Undo.Execute(new AddAnnotationAction(document.Annotations, annotation));
        }

        return new DocumentExporter().Render(document);
    }

    private static string TempFile(string extension, byte[] data)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gsts-test-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] Gif(params Color[] colors)
    {
        var encoder = new GifBitmapEncoder();
        foreach (var color in colors)
        {
            var buffer = PixelTools.Solid(8, 8, color.R, color.G, color.B);
            encoder.Frames.Add(BitmapFrame.Create(buffer.ToBitmapSource()));
        }

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static (byte R, byte G, byte B, byte A) CenterOf(ImageAsset asset)
    {
        var bitmap = new ImageAssetCache().Get(asset, 8, 8)!;
        var buffer = PixelBufferExtensions.FromBitmapSource(bitmap);
        return PixelTools.Get(buffer, buffer.Width / 2, buffer.Height / 2);
    }

    // ---- Darstellung ----

    [StaFact]
    public void Render_Schatten_NurUnterDeckendenBildteilen()
    {
        // Bild 10..50 × 10..30, deckend nur 30..50; Schatten um 10 versetzt.
        var image = Image(HalfTransparentPng(), new RectD(10, 10, 40, 20));
        image.Shadow = new ShadowStyle(ArgbColor.Black, Size: 10, Blur: 0);

        var result = Render(image);

        PixelTools.Get(result, 40, 20).ShouldBe(Blue);
        PixelTools.Get(result, 15, 15).ShouldBe(White);
        PixelTools.Get(result, 55, 35).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));

        // Schatten des transparenten Teils läge bei 20..40 × 20..40 – dort bleibt es weiß.
        PixelTools.Get(result, 25, 35).ShouldBe(White);
    }

    [StaFact]
    public void Render_Kontur_UmDieDeckendenBildteile()
    {
        var image = Image(HalfTransparentPng(), new RectD(10, 10, 40, 20));
        image.Outline = new StrokeStyle(ArgbColor.Red, 4);

        var result = Render(image);

        PixelTools.Get(result, 40, 20).ShouldBe(Blue);
        PixelTools.Get(result, 28, 20).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 52, 20).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 40, 8).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
        PixelTools.Get(result, 20, 20).ShouldBe(White);
        PixelTools.Get(result, 40, 36).ShouldBe(White);
    }

    [StaFact]
    public void Render_GedrehtUndGespiegelt()
    {
        // Gespiegelt liegt die deckende Hälfte links (10..30).
        var image = Image(HalfTransparentPng(), new RectD(10, 10, 40, 20));
        image.FlipHorizontal = true;
        var flipped = Render(image);
        PixelTools.Get(flipped, 15, 20).ShouldBe(Blue);
        PixelTools.Get(flipped, 45, 20).ShouldBe(White);

        // Um 180° gedreht ebenfalls.
        image.FlipHorizontal = false;
        image.Rotation = 180;
        var rotated = Render(image);
        PixelTools.Get(rotated, 15, 20).ShouldBe(Blue);
        PixelTools.Get(rotated, 45, 20).ShouldBe(White);
    }

    [StaFact]
    public void Render_DefekteDatei_ZeichnetPlatzhalter()
    {
        var image = Image(new ImageAsset(ImageAssetFormat.Png, [1, 2, 3]), new RectD(10, 10, 40, 40));

        AnnotationRenderer.GetImageBitmap(image).ShouldBeNull();
        Should.NotThrow(() => Render(image));
    }

    [StaFact]
    public void Svg_WirdGerastertUndHatFesteGroesse()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='20'><rect width='40' height='20' fill='#ff0000'/></svg>");
        var import = ImageImport.FromBytes(svg, ".svg");
        import.IsSvg.ShouldBeTrue();
        var created = import.Create();
        created.Asset.Format.ShouldBe(ImageAssetFormat.Svg);
        created.Asset.Data.ShouldBe(svg);
        created.Size.ShouldBe(new SizeD(40, 20));

        var result = Render(Image(created.Asset, new RectD(10, 10, 80, 40)));
        PixelTools.Get(result, 50, 30).ShouldBe(((byte)255, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void Svg_OhneFesteGroesse_Bekommt256Pixel()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 12'><rect width='24' height='12'/></svg>");

        ImageImport.FromBytes(svg).Create().Size.ShouldBe(new SizeD(256, 128));
    }

    [StaFact]
    public void Svg_LaedtKeineExternenDateien()
    {
        var lime = PixelTools.Solid(4, 4, 0, 255, 0).EncodePng();
        var path = TempFile(".png", lime);
        try
        {
            var uri = new Uri(path).AbsoluteUri;
            var svg = Encoding.UTF8.GetBytes($"<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='20' height='20'><image width='20' height='20' xlink:href='{uri}'/></svg>");
            var asset = ImageImport.FromBytes(svg, ".svg").Create().Asset;

            CenterOf(asset).ShouldNotBe(((byte)0, (byte)255, (byte)0, (byte)255));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void Svg_Kaputt_WirdAbgelehnt() =>
        Should.Throw<ImageImportException>(() => ImageImport.FromBytes(Encoding.UTF8.GetBytes("<svg"), ".svg"));

    // ---- Import ----

    [StaFact]
    public void Import_PngUndJpegBleibenOriginal_AndereWerdenPng()
    {
        var buffer = PixelTools.Solid(6, 4, 10, 20, 30);
        var png = buffer.EncodePng();
        var pngImport = ImageImport.FromBytes(png).Create();
        pngImport.Asset.Format.ShouldBe(ImageAssetFormat.Png);
        pngImport.Asset.Data.ShouldBe(png);
        pngImport.Size.ShouldBe(new SizeD(6, 4));

        var jpeg = buffer.Encode(ImageFormatSetting.Jpeg);
        var jpegImport = ImageImport.FromBytes(jpeg).Create();
        jpegImport.Asset.Format.ShouldBe(ImageAssetFormat.Jpeg);
        jpegImport.Asset.Data.ShouldBe(jpeg);

        var bmp = buffer.Encode(ImageFormatSetting.Bmp);
        var bmpImport = ImageImport.FromBytes(bmp).Create();
        bmpImport.Asset.Format.ShouldBe(ImageAssetFormat.Png);
        bmpImport.Size.ShouldBe(new SizeD(6, 4));
    }

    [StaFact]
    public void Import_KeinBild_WirdAbgelehnt() =>
        Should.Throw<ImageImportException>(() => ImageImport.FromBytes(Encoding.UTF8.GetBytes("Hallo Welt")));

    [StaFact]
    public void Import_AnimiertesGif_EinzelbildWaehlbar()
    {
        var import = ImageImport.FromBytes(Gif(Colors.Red, Colors.Lime, Colors.Blue));

        import.FrameCount.ShouldBe(3);
        import.Thumbnail(1, 96).PixelWidth.ShouldBe(8);
        var second = import.Create(1);
        second.Asset.Format.ShouldBe(ImageAssetFormat.Png);
        CenterOf(second.Asset).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));
        CenterOf(import.Create(2).Asset).ShouldBe(((byte)0, (byte)0, (byte)255, (byte)255));
    }

    // ---- Einfügen ----

    [StaFact]
    public void Einfuegen_AusDatei_MitVorgabenUndEinemUndoSchritt()
    {
        var h = new EditorHarness();
        h.Context.Image.Style = new ImageStyle { Opacity = 0.5, Shadow = new ShadowStyle(ArgbColor.Black, 3, 2), Outline = new StrokeStyle(ArgbColor.White, 2) };
        var path = TempFile(".png", PixelTools.Solid(30, 20, 1, 2, 3).EncodePng());
        try
        {
            h.ImageDialogs.PickImageFiles().Returns([path]);

            var created = h.ViewModel.InsertImageFromFiles(new PointD(100, 75), null);

            var image = created.ShouldHaveSingleItem();
            h.Document.Annotations.ShouldContain(image);
            image.Rect.ShouldBe(new RectD(85, 65, 30, 20));
            image.Opacity.ShouldBe(0.5);
            image.Shadow.ShouldBe(new ShadowStyle(ArgbColor.Black, 3, 2));
            image.Outline.ShouldBe(new StrokeStyle(ArgbColor.White, 2));
            image.Name.ShouldBe(Path.GetFileNameWithoutExtension(path));
            h.Context.Selection.Items.ShouldBe([image]);

            h.Context.Undo.Undo();
            h.Document.Annotations.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void Einfuegen_InBereich_WirdProportionalEingepasst()
    {
        var h = new EditorHarness();
        var path = TempFile(".png", PixelTools.Solid(40, 20, 1, 2, 3).EncodePng());
        try
        {
            var image = h.ViewModel.InsertImageFiles([path], new PointD(0, 0), new RectD(10, 10, 100, 100)).ShouldHaveSingleItem();

            image.Rect.ShouldBe(new RectD(10, 35, 100, 50));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void Einfuegen_MehrbildDatei_FragtNachEinzelbild()
    {
        var h = new EditorHarness();
        var path = TempFile(".gif", Gif(Colors.Red, Colors.Lime));
        try
        {
            h.ImageDialogs.PickFrame(Arg.Any<ImageImport>(), Arg.Any<string>()).Returns(1);
            var image = h.ViewModel.InsertImageFiles([path], new PointD(50, 50)).ShouldHaveSingleItem();
            CenterOf(image.Source!).ShouldBe(((byte)0, (byte)255, (byte)0, (byte)255));

            h.ImageDialogs.PickFrame(Arg.Any<ImageImport>(), Arg.Any<string>()).Returns((int?)null);
            h.ViewModel.InsertImageFiles([path], new PointD(50, 50)).ShouldBeEmpty();
            h.Document.Annotations.Count.ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void Einfuegen_DefekteDatei_WirdGemeldet()
    {
        var h = new EditorHarness();
        var path = TempFile(".png", [1, 2, 3, 4]);
        try
        {
            h.ViewModel.InsertImageFiles([path], new PointD(50, 50)).ShouldBeEmpty();

            h.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains(Path.GetFileName(path))));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [StaFact]
    public void ZwischenablageAlsObjekt_FuegtBildEin()
    {
        var h = new EditorHarness();
        h.Clipboard.GetAnnotations().Returns([]);
        h.Clipboard.GetImageFiles().Returns([]);
        h.Clipboard.GetSvg().Returns((byte[]?)null);
        h.Clipboard.GetImage().Returns(PixelTools.Solid(30, 20, 9, 9, 9));

        h.ViewModel.PasteAsImageCommand.Execute(null);

        var image = h.Document.Annotations.ShouldHaveSingleItem().ShouldBeOfType<ImageAnnotation>();
        image.Source!.Format.ShouldBe(ImageAssetFormat.Png);
        image.OriginalSize.ShouldBe(new SizeD(30, 20));
        h.ViewModel.ActiveTool.ShouldBeOfType<SelectTool>();
    }

    [StaFact]
    public void ZwischenablageAlsObjekt_OhneBild_Hinweis()
    {
        var h = new EditorHarness();
        h.Clipboard.GetAnnotations().Returns([]);
        h.Clipboard.GetImageFiles().Returns([]);
        h.Clipboard.GetSvg().Returns((byte[]?)null);
        h.Clipboard.GetImage().Returns((PixelBuffer?)null);

        h.ViewModel.PasteAsImageCommand.Execute(null);

        h.Document.Annotations.ShouldBeEmpty();
        h.Dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
    }

    [StaFact]
    public void Werkzeug_KlickUndAufziehen_MeldenPunktBzwBereich()
    {
        var h = new EditorHarness();
        var tool = h.Tools.Get<ImageTool>();
        h.ViewModel.Activate(tool);
        var calls = new List<(PointD Center, RectD? Area)>();
        h.Context.ImageInsertHandler = (c, a) => calls.Add((c, a));

        tool.OnPointerDown(new EditorPointerArgs(new PointD(40, 30), ModifierKeys.None));
        tool.OnPointerUp(new EditorPointerArgs(new PointD(40, 30), ModifierKeys.None));
        tool.OnPointerDown(new EditorPointerArgs(new PointD(10, 10), ModifierKeys.None));
        tool.OnPointerMove(new EditorPointerArgs(new PointD(60, 40), ModifierKeys.None));
        h.Context.Interaction.RubberBand.ShouldNotBeNull();
        tool.OnPointerUp(new EditorPointerArgs(new PointD(60, 40), ModifierKeys.None));

        calls.Count.ShouldBe(2);
        calls[0].ShouldBe((new PointD(40, 30), (RectD?)null));
        calls[1].Area.ShouldBe(new RectD(10, 10, 50, 30));
        h.Context.Interaction.RubberBand.ShouldBeNull();
    }

    // ---- Bearbeiten ----

    private static (EditorHarness H, ImageAnnotation Image) Inserted()
    {
        var h = new EditorHarness();
        var image = h.Context.Image.Insert(h.Context, [(new ImportedImage(HalfTransparentPng(), new SizeD(40, 20)), null)], new PointD(100, 75)).Single();
        return (h, image);
    }

    [StaFact]
    public void Auswahl_UmschaltH_SpiegeltBilder()
    {
        var (h, image) = Inserted();
        var select = h.Tools.Get<SelectTool>();
        h.ViewModel.Activate(select);
        h.Context.Selection.Set(image);

        select.OnKeyDown(Key.H, ModifierKeys.Shift).ShouldBeTrue();
        image.FlipHorizontal.ShouldBeTrue();
        select.OnKeyDown(Key.OemCloseBrackets, ModifierKeys.None).ShouldBeTrue();
        image.Rotation.ShouldBe(ShapeGeometry.AngleStepDegrees);
    }

    [StaFact]
    public void Panel_GroesseMitSeitenverhaeltnisUndOriginalgroesse()
    {
        var (h, image) = Inserted();
        h.Context.Selection.Set(image);
        var panel = h.ViewModel.ImageProperties;
        panel.IsApplicable.ShouldBeTrue();
        panel.HasImages.ShouldBeTrue();

        panel.Width = 80;
        image.Rect.Width.ShouldBe(80);
        image.Rect.Height.ShouldBe(40);
        image.Rect.Center.ShouldBe(new PointD(100, 75));

        panel.KeepAspectRatio = false;
        panel.Height = 10;
        image.Rect.Width.ShouldBe(80);
        image.Rect.Height.ShouldBe(10);

        panel.ResetSizeCommand.Execute(null);
        image.Rect.ShouldBe(new RectD(80, 65, 40, 20));
    }

    [StaFact]
    public void Panel_KonturUndSchatten_AmBildUndAlsVorgabe()
    {
        var (h, image) = Inserted();
        h.Context.Selection.Set(image);
        h.ViewModel.ImageProperties.OutlineThickness = 5;
        h.ViewModel.ShadowProperties.ShadowColor = ArgbColor.Black;

        image.Outline.Thickness.ShouldBe(5);
        image.Shadow.Color.ShouldBe(ArgbColor.Black);

        // Ohne Auswahl bei aktivem Bild-Werkzeug: Vorgaben neuer Bilder.
        h.Context.Selection.Clear();
        h.ViewModel.Activate(h.Tools.Get<ImageTool>());
        h.ViewModel.ImageProperties.ShowDefaults.ShouldBeTrue();
        h.ViewModel.ShadowProperties.IsApplicable.ShouldBeTrue();
        h.ViewModel.ImageProperties.OutlineBlur = 3;
        h.Context.Image.Style.OutlineBlur.ShouldBe(3);
    }

    [StaFact]
    public void Vorlagen_UndStandardWiederherstellen()
    {
        var (h, image) = Inserted();
        var preset = new ToolPreset { Name = "Rahmen", Image = new ImageStyle { Outline = new StrokeStyle(ArgbColor.Red, 6), OutlineBlur = 1 } };
        PresetStyles.KindOf(ImageTool.ToolId).ShouldBe(PresetKind.Image);
        PresetStyles.ToolIdOf(image, null).ShouldBe(ImageTool.ToolId);

        h.Context.Selection.Set(image);
        PresetStyles.Apply(h.Context, ImageTool.ToolId, preset).ShouldBe(1);
        image.Outline.ShouldBe(new StrokeStyle(ArgbColor.Red, 6));
        PresetStyles.Capture(h.Context, ImageTool.ToolId, new ShapeStyle(), "X").Image!.OutlineBlur.ShouldBe(1);

        h.Context.Selection.Clear();
        PresetStyles.Apply(h.Context, ImageTool.ToolId, preset);
        h.Context.Image.Style.Outline.ShouldBe(new StrokeStyle(ArgbColor.Red, 6));
        ToolDefaultsReset.Supports(h.Tools.Get<ImageTool>()).ShouldBeTrue();
        ToolDefaultsReset.Reset(h.Context, h.Tools.Get<ImageTool>());
        h.Context.Image.Style.ShouldBe(new ImageStyle());
    }

    [StaFact]
    public void Ebenenliste_ZeigtVorschauUndName()
    {
        var (h, image) = Inserted();
        AnnotationNames.KindKey(image).ShouldBe("Shape_Image");
        image.Name.ShouldNotBeNullOrWhiteSpace();
        AnnotationRenderer.GetImageBitmap(image).ShouldNotBeNull();
    }
}
