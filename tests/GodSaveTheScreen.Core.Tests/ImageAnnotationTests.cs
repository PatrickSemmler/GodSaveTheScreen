using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Tests;

public sealed class ImageAnnotationTests
{
    private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10'/></svg>");

    private static ImageAnnotation Sample(ImageAsset? asset = null) => new()
    {
        Name = "Logo",
        Source = asset ?? new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png),
        Rect = new RectD(10, 20, 120, 60),
        OriginalSize = new SizeD(240, 120),
        Rotation = 30,
        FlipHorizontal = true,
        Opacity = 0.8,
        Shadow = new ShadowStyle(ArgbColor.Parse("#80000000"), 5, 8),
        Outline = new StrokeStyle(ArgbColor.Parse("#FFFFFFFF"), 4),
        OutlineBlur = 2,
    };

    [Fact]
    public void Asset_HashUndGleichheit()
    {
        var a = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var b = new ImageAsset(ImageAssetFormat.Png, [.. ProjectSamples.Png]);
        var svg = new ImageAsset(ImageAssetFormat.Svg, Svg);

        a.Hash.Length.ShouldBe(64);
        ImageAsset.IsValidHash(a.Hash).ShouldBeTrue();
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldNotBe(svg);
        a.EntryName.ShouldBe($"image-{a.Hash}.png");
        svg.EntryName.ShouldEndWith(".svg");
        new ImageAsset(ImageAssetFormat.Jpeg, [1]).Extension.ShouldBe(".jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../../evil")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public void Asset_UngueltigeHashes(string? hash) => ImageAsset.IsValidHash(hash).ShouldBeFalse();

    [Fact]
    public void Objektliste_EnthaeltBilddatenInline()
    {
        var original = Sample();
        var json = AnnotationSerializer.Serialize([original]);

        json.ShouldContain("\"type\":\"image\"");
        json.ShouldContain("\"data\":");
        var restored = AnnotationSerializer.Deserialize(json).ShouldHaveSingleItem().ShouldBeOfType<ImageAnnotation>();
        ProjectSamples.ShouldHaveSameValues(restored, original);
        restored.Source!.Data.ShouldBe(ProjectSamples.Png);
    }

    [Fact]
    public void Objektliste_OhneDaten_WirdUebersprungen()
    {
        var hash = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png).Hash;
        var json = $$$"""[{"type":"image","Rect":[0,0,10,10],"Source":{"format":"Png","hash":"{{{hash}}}"}},{"type":"rect","Rect":[0,0,5,5]}]""";
        var warnings = new List<string>();

        var restored = ProjectSerializer.DeserializeAnnotations(json, warnings);

        restored.ShouldHaveSingleItem().ShouldBeOfType<RectangleAnnotation>();
        warnings.ShouldHaveSingleItem().ShouldContain("image");
    }

    [Fact]
    public void Projekt_SpeichertBilderAlsEintraege_GleicheNurEinmal()
    {
        var png = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var svg = new ImageAsset(ImageAssetFormat.Svg, Svg);
        var first = Sample(png);
        var copy = (ImageAnnotation)first.Clone();
        copy.Rect = new RectD(200, 200, 30, 30);
        var vector = Sample(svg);
        var data = ProjectSamples.Save(ProjectSamples.Content([first, copy, vector]));

        using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
        {
            var images = zip.Entries.Where(e => e.FullName.StartsWith("image-", StringComparison.Ordinal)).Select(e => e.FullName).ToList();
            images.Count.ShouldBe(2);
            images.ShouldContain(png.EntryName);
            images.ShouldContain(svg.EntryName);
            using var reader = new StreamReader(zip.GetEntry(ProjectSerializer.DocumentEntry)!.Open());
            var document = JsonNode.Parse(reader.ReadToEnd())!;
            document.ToJsonString().ShouldNotContain("\"data\"");
        }

        var restored = ProjectSamples.Load(data);
        restored.Warnings.ShouldBeEmpty();
        restored.Annotations.Count.ShouldBe(3);
        ProjectSamples.ShouldHaveSameValues(restored.Annotations[0], first);
        ProjectSamples.ShouldHaveSameValues(restored.Annotations[1], copy);
        ProjectSamples.ShouldHaveSameValues(restored.Annotations[2], vector);
        ((ImageAnnotation)restored.Annotations[2]).Source!.Data.ShouldBe(Svg);
    }

    [Fact]
    public void Projekt_FehlenderBildeintrag_WirdMitWarnungUebersprungen()
    {
        var image = Sample();
        var json = ProjectSerializer.SerializeAnnotations([image]);

        // Objektliste ohne die Bytes, wie sie im Projekt steht – der passende Eintrag fehlt im ZIP.
        var node = JsonNode.Parse(json)!.AsArray();
        node[0]!["Source"]!.AsObject().Remove("data");
        var data = ProjectSamples.Zip(
            (ProjectSerializer.ManifestEntry, ProjectSamples.Text(ProjectSamples.Manifest())),
            (ProjectSerializer.DocumentEntry, ProjectSamples.Text(ProjectSamples.Document(node.ToJsonString()))),
            (ProjectSerializer.BaseEntry, ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Annotations.ShouldBeEmpty();
        restored.Warnings.ShouldHaveSingleItem().ShouldContain("image");
    }

    [Fact]
    public void Projekt_HashMitPfad_WirdNichtAufgeloest()
    {
        var data = ProjectSamples.Zip(
            (ProjectSerializer.ManifestEntry, ProjectSamples.Text(ProjectSamples.Manifest())),
            (ProjectSerializer.DocumentEntry, ProjectSamples.Text(ProjectSamples.Document("""[{"type":"image","Rect":[0,0,5,5],"Source":{"format":"Png","hash":"../base"}}]"""))),
            (ProjectSerializer.BaseEntry, ProjectSamples.Png),
            ("image-../base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Annotations.ShouldBeEmpty();
        restored.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void Kopie_TeiltDieBilddatei()
    {
        var original = Sample();
        var copy = (ImageAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Source.ShouldBeSameAs(original.Source);
        copy.Outline.ShouldBe(original.Outline);
    }

    [Fact]
    public void Geometrie_GedrehtUndMitKontur()
    {
        var image = new ImageAnnotation { Rect = new RectD(0, 0, 100, 50), Rotation = 90 };

        image.Bounds.X.ShouldBe(25, 1e-9);
        image.Bounds.Width.ShouldBe(50, 1e-9);
        image.Bounds.Height.ShouldBe(100, 1e-9);
        image.HitTest(new PointD(50, 70), 0).ShouldBeTrue();
        image.HitTest(new PointD(95, 25), 0).ShouldBeFalse();
        image.VisualBounds.ShouldBe(image.Bounds);

        image.Outline = new StrokeStyle(ArgbColor.Black, 6);
        image.OutlineBlur = 4;
        image.VisualMargin.ShouldBe(10);
        image.Shadow = new ShadowStyle(ArgbColor.Black, 5, 3);
        image.VisualBounds.Right.ShouldBe(image.Bounds.Right + 10 + 5 + 3, 1e-9);
    }

    [Fact]
    public void Platzieren_OriginalgroesseHoechstens80Prozent()
    {
        var small = ImageAnnotation.PlaceAt(new SizeD(100, 50), new PointD(500, 400), new SizeD(1000, 800));
        small.ShouldBe(new RectD(450, 375, 100, 50));

        var large = ImageAnnotation.PlaceAt(new SizeD(2000, 1000), new PointD(500, 400), new SizeD(1000, 800));
        large.Width.ShouldBe(800, 1e-9);
        large.Height.ShouldBe(400, 1e-9);
        large.Center.ShouldBe(new PointD(500, 400));

        var fitted = ImageAnnotation.FitInto(new SizeD(200, 100), new RectD(0, 0, 100, 100));
        fitted.ShouldBe(new RectD(0, 25, 100, 50));
    }

    [Fact]
    public void Einstellungen_BildvorgabenNormalisiert()
    {
        var settings = new AppSettings { ImageDefaults = null! }.Normalized();
        settings.ImageDefaults.ShouldBe(new ImageStyle());

        var broken = new AppSettings { ImageDefaults = new ImageStyle { Outline = null!, Shadow = null!, Opacity = double.NaN } }.Normalized();
        broken.ImageDefaults.Outline.ShouldBe(ImageAnnotation.NoOutline);
        broken.ImageDefaults.Shadow.ShouldBe(ShadowStyle.None);
        broken.ImageDefaults.Opacity.ShouldBe(1);
    }
}
