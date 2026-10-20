using System.Collections;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;

namespace GodSaveTheScreen.Core.Tests;

internal static class ProjectSamples
{
    /// <summary>1×1-PNG als Platzhalter (der Core dekodiert keine Bilder).</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static List<Annotation> OneOfEach()
    {
        var stroke = new StrokeStyle(ArgbColor.Parse("#80336699"), 3.5, DashKind.DashDot, LineJoinKind.Bevel, StrokeCapKind.Square);
        return
        [
            new RectangleAnnotation { Name = "Rechteck", Opacity = 0.75, IsLocked = true, Rect = new RectD(10, 20, 30.5, 40.25), CornerRadius = 6, Fill = ArgbColor.Parse("#40FF8800"), Stroke = stroke, Shadow = new ShadowStyle(ArgbColor.Parse("#80000000"), 5, 7.5) },
            new EllipseAnnotation { Name = "Ellipse", IsVisible = false, Rect = new RectD(1, 2, 3, 4), Fill = ArgbColor.Parse("#FF00FF00"), Stroke = stroke },
            new TriangleAnnotation { Rect = new RectD(5, 6, 70, 80), Kind = TriangleKind.Right, IsFlipped = true, Fill = ArgbColor.White, Stroke = stroke },
            new LineAnnotation { Start = new PointD(1.5, 2.5), End = new PointD(100, 90), StartCap = LineEndStyle.Circle, EndCap = LineEndStyle.OpenArrow, CapSize = 7, Stroke = stroke },
            new PolygonAnnotation { Points = [new PointD(0, 0), new PointD(50, 10), new PointD(30, 60)], IsClosed = false, Fill = ArgbColor.Red, Stroke = stroke },
            new TextAnnotation
            {
                Text = "Hallo\nWelt – ä ö ü",
                Position = new PointD(12, 34),
                FontFamilyName = "Consolas",
                FontWeight = 700,
                FontStyle = FontStyleKind.Italic,
                FontStretch = 7,
                FontSize = 31.5,
                Fill = ArgbColor.Parse("#FF112233"),
                Stroke = new StrokeStyle(ArgbColor.White, 2),
                OutlineBlur = 3.5,
                Shadow = new ShadowStyle(ArgbColor.Parse("#FF334455"), 2, 0),
                TextAlignment = TextAlignmentKind.Center,
                Background = ArgbColor.Parse("#99FFFF00"),
                BackgroundPadding = 9,
                BackgroundCornerRadius = 3,
                Decorations = TextDecorationKind.Underline,
                WrapWidth = 180,
            },
            new MarkerAnnotation
            {
                Center = new PointD(200, 150),
                Number = 7,
                Shape = MarkerShape.Rounded,
                Size = 40,
                Fill = ArgbColor.Parse("#FF008800"),
                Stroke = new StrokeStyle(ArgbColor.Black, 3),
                TextColor = ArgbColor.Parse("#FFFFEEDD"),
                FontFamilyName = "Arial",
                PointerTarget = new PointD(260, 190),
                Shadow = new ShadowStyle(ArgbColor.Black, 3, 4),
            },
            new StrokeAnnotation
            {
                Points = [new StrokePoint(1, 2, 0.5), new StrokePoint(3, 4, 1), new StrokePoint(5.5, 6.5, 0.25)],
                IsHighlighter = true,
                Smoothing = 0.8,
                UsePressure = false,
                Stroke = new StrokeStyle(ArgbColor.Parse("#80FFFF00"), 14),
            },
            new BlurAnnotation
            {
                Rect = new RectD(40, 50, 60, 70),
                Mode = BlurMode.GaussianBlur,
                Strength = 33,
                Shape = RegionShape.Ellipse,
                FeatherEdge = 4,
                RedactColor = ArgbColor.Parse("#FF202020"),
            },
            new MagnifierAnnotation
            {
                SourceRect = new RectD(10, 10, 40, 30),
                TargetCenter = new PointD(150, 120),
                Zoom = 3.5,
                Shape = MagnifierShape.Rectangle,
                Border = new StrokeStyle(ArgbColor.Parse("#FFAA00AA"), 5),
                ShowConnector = false,
                ShowSourceFrame = false,
                BaseImageOnly = true,
                SmoothScaling = false,
            },
            new EmojiAnnotation
            {
                Name = "Daumen",
                Sequence = "👍🏽",
                Rect = new RectD(30, 40, 96, 64),
                Rotation = 37.5,
                FlipHorizontal = true,
                FlipVertical = true,
                Opacity = 0.6,
                Shadow = new ShadowStyle(ArgbColor.Parse("#40000000"), 6, 10),
            },
        ];
    }

    public static ProjectContent Content(IReadOnlyList<Annotation>? annotations = null) => new()
    {
        AppVersion = "1.2.3",
        CreatedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(2)),
        ModifiedAt = new DateTimeOffset(2026, 10, 19, 8, 30, 0, TimeSpan.FromHours(2)),
        Width = 640,
        Height = 480,
        DpiX = 120,
        DpiY = 110,
        CropRect = new PixelRect(5, 6, 300, 200),
        HighlightersBehind = false,
        Metadata = new DocumentMetadata("Window", "Mein Fenster – Ünïcode"),
        Annotations = annotations ?? OneOfEach(),
        BasePng = Png,
        PreviewPng = Png,
    };

    public static byte[] Save(ProjectContent content)
    {
        using var stream = new MemoryStream();
        ProjectSerializer.Write(stream, content);
        return stream.ToArray();
    }

    public static ProjectContent Load(byte[] data, IEnumerable<IProjectMigration>? migrations = null)
    {
        using var stream = new MemoryStream(data);
        return ProjectSerializer.Read(stream, migrations);
    }

    /// <summary>Baut ein ZIP mit den angegebenen Einträgen (Name → Bytes).</summary>
    public static byte[] Zip(params (string Name, byte[] Data)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(data);
            }
        }

        return stream.ToArray();
    }

    public static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    public static string Manifest(int version = 1, string format = ProjectSerializer.FormatName) =>
        $$"""{"format":"{{format}}","formatVersion":{{version}},"appVersion":"t","createdAt":"2026-01-01T00:00:00+00:00","modifiedAt":"2026-01-01T00:00:00+00:00"}""";

    public static string Document(string annotations = "[]", int width = 10, int height = 10) =>
        $$"""{"width":{{width}},"height":{{height}},"dpiX":96,"dpiY":96,"annotations":{{annotations}}}""";

    /// <summary>Vergleicht alle öffentlichen lesbaren Eigenschaften (rekursiv für Listen) per Wert.</summary>
    public static void ShouldHaveSameValues(Annotation actual, Annotation expected)
    {
        actual.GetType().ShouldBe(expected.GetType());
        foreach (var property in expected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null)
            {
                continue;
            }

            var a = property.GetValue(actual);
            var e = property.GetValue(expected);
            if (e is IEnumerable enumerable and not string)
            {
                var left = enumerable.Cast<object>().ToList();
                var right = ((IEnumerable)a!).Cast<object>().ToList();
                right.ShouldBe(left, $"Eigenschaft {property.Name} von {expected.GetType().Name}");
            }
            else
            {
                a.ShouldBe(e, $"Eigenschaft {property.Name} von {expected.GetType().Name}");
            }
        }
    }
}

public sealed class ProjectSerializerTests
{
    [Fact]
    public void Roundtrip_JederAnnotationstyp_MitAllenEigenschaften()
    {
        var originals = ProjectSamples.OneOfEach();
        originals.Select(a => a.GetType()).Distinct().Count().ShouldBe(11);

        var restored = ProjectSamples.Load(ProjectSamples.Save(ProjectSamples.Content(originals)));

        restored.Annotations.Count.ShouldBe(originals.Count);
        restored.Warnings.ShouldBeEmpty();
        for (var i = 0; i < originals.Count; i++)
        {
            ProjectSamples.ShouldHaveSameValues(restored.Annotations[i], originals[i]);
        }
    }

    [Fact]
    public void Roundtrip_BehaeltIdsUndReihenfolge()
    {
        var originals = ProjectSamples.OneOfEach();
        var restored = ProjectSamples.Load(ProjectSamples.Save(ProjectSamples.Content(originals)));

        restored.Annotations.Select(a => a.Id).ShouldBe(originals.Select(a => a.Id));
    }

    [Fact]
    public void Roundtrip_Dokumentfelder()
    {
        var content = ProjectSamples.Content();

        var restored = ProjectSamples.Load(ProjectSamples.Save(content));

        restored.AppVersion.ShouldBe("1.2.3");
        restored.CreatedAt.ShouldBe(content.CreatedAt);
        restored.ModifiedAt.ShouldBe(content.ModifiedAt);
        restored.Width.ShouldBe(640);
        restored.Height.ShouldBe(480);
        restored.DpiX.ShouldBe(120);
        restored.DpiY.ShouldBe(110);
        restored.CropRect.ShouldBe(new PixelRect(5, 6, 300, 200));
        restored.HighlightersBehind.ShouldBeFalse();
        restored.Metadata.ShouldBe(new DocumentMetadata("Window", "Mein Fenster – Ünïcode"));
        restored.BasePng.ShouldBe(ProjectSamples.Png);
        restored.PreviewPng.ShouldBe(ProjectSamples.Png);
    }

    [Fact]
    public void Roundtrip_OhneZuschnittOhneVorschau()
    {
        var content = ProjectSamples.Content([]) with { CropRect = null, PreviewPng = null };

        var restored = ProjectSamples.Load(ProjectSamples.Save(content));

        restored.CropRect.ShouldBeNull();
        restored.PreviewPng.ShouldBeNull();
        restored.Annotations.ShouldBeEmpty();
    }

    [Fact]
    public void Datei_IstEinZipMitDenVierFestenEintraegen()
    {
        var data = ProjectSamples.Save(ProjectSamples.Content());

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Order().ShouldBe(["base.png", "document.json", "manifest.json", "preview.png"]);
        zip.GetEntry("base.png")!.Length.ShouldBe(ProjectSamples.Png.Length);

        var manifest = JsonNode.Parse(new StreamReader(zip.GetEntry("manifest.json")!.Open()).ReadToEnd())!.AsObject();
        manifest["format"]!.GetValue<string>().ShouldBe("GodSaveTheScreen.Project");
        manifest["formatVersion"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void Json_IstKompakt_FarbenAlsHexUndGeometrieAlsArray()
    {
        var data = ProjectSamples.Save(ProjectSamples.Content([new RectangleAnnotation { Rect = new RectD(1, 2, 3, 4), Fill = ArgbColor.Parse("#80FF0000") }]));

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        var json = new StreamReader(zip.GetEntry("document.json")!.Open()).ReadToEnd();

        json.ShouldContain("\"type\":\"rect\"");
        json.ShouldContain("\"#80FF0000\"");
        json.ShouldContain("[1,2,3,4]");
        json.ShouldContain("\"cropRect\":[5,6,300,200]");
    }

    [Fact]
    public void UnbekannterTyp_WirdUebersprungenMitWarnung()
    {
        var annotations = """[{"type":"rect","Rect":[1,2,3,4]},{"type":"hologramm","foo":1},{"type":"ellipse","Rect":[5,6,7,8]}]""";
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document(annotations))),
            ("base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Annotations.Select(a => a.GetType()).ShouldBe([typeof(RectangleAnnotation), typeof(EllipseAnnotation)]);
        restored.Warnings.Count.ShouldBe(1);
        restored.Warnings[0].ShouldContain("hologramm");
    }

    [Fact]
    public void DefektesObjekt_WirdUebersprungen()
    {
        var annotations = """[{"type":"rect","Rect":"kaputt"},{"type":"line","Start":[1,2],"End":[3,4]}]""";
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document(annotations))),
            ("base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Annotations.Count.ShouldBe(1);
        restored.Annotations[0].ShouldBeOfType<LineAnnotation>();
        restored.Warnings.Count.ShouldBe(1);
    }

    [Fact]
    public void UnbekannteZusatzeigenschaften_WerdenIgnoriert()
    {
        var annotations = """[{"type":"rect","Rect":[1,2,3,4],"zukunft":{"a":1}}]""";
        var document = ProjectSamples.Document(annotations).Replace("\"annotations\"", "\"neuesFeld\":42,\"annotations\"", StringComparison.Ordinal);
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(document)),
            ("base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Annotations.Count.ShouldBe(1);
        restored.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void NeuereFormatversion_WirdMitWarnungGelesen()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest(version: 99))),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.Warnings.ShouldContain(w => w.Contains("neueren", StringComparison.Ordinal));
    }

    private sealed class ZeroToOneMigration : IProjectMigration
    {
        public int FromVersion => 0;

        public int ToVersion => 1;

        public void Migrate(JsonObject manifest, JsonObject document)
        {
            // Version 0 nannte die Größe "size": [w, h].
            var size = document["size"]!.AsArray();
            document["width"] = size[0]!.GetValue<int>();
            document["height"] = size[1]!.GetValue<int>();
            document.Remove("size");
        }
    }

    [Fact]
    public void Migration_V0NachV1_WirdAufJsonEbeneAngewendet()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest(version: 0))),
            ("document.json", ProjectSamples.Text("""{"size":[20,30],"dpiX":96,"dpiY":96,"annotations":[]}""")),
            ("base.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data, [new ZeroToOneMigration()]);

        restored.Width.ShouldBe(20);
        restored.Height.ShouldBe(30);
    }

    [Fact]
    public void AlteVersionOhneMigration_WirdAbgelehnt()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest(version: 0))),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data));
    }

    [Fact]
    public void FremdesFormat_WirdAbgelehnt()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest(format: "Anderes"))),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data));
    }

    [Fact]
    public void KeinZip_WirdAlsBeschaedigtGemeldet() =>
        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(ProjectSamples.Text("das ist kein zip")));

    [Fact]
    public void DefektesJson_WirdAlsBeschaedigtGemeldet()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text("{nicht json")),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data));
    }

    [Fact]
    public void FehlenderPflichteintrag_WirdAbgelehnt()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data)).Message.ShouldContain("base.png");
    }

    [Fact]
    public void ZipBombe_ZuGrosserEintrag_WirdAbgelehnt()
    {
        // 100 KB Nullen komprimieren auf wenige Bytes, überschreiten aber die Manifest-Grenze (64 KB).
        var data = ProjectSamples.Zip(
            ("manifest.json", new byte[100 * 1024]),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data)).Message.ShouldContain("zu groß");
    }

    [Theory]
    [InlineData(40000, 10)]
    [InlineData(10, 40000)]
    [InlineData(20000, 20000)]
    [InlineData(0, 10)]
    public void UnsinnigeBildgroesse_WirdAbgelehnt(int width, int height)
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document(width: width, height: height))),
            ("base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data));
    }

    [Fact]
    public void PfadeImZip_WerdenIgnoriert()
    {
        var data = ProjectSamples.Zip(
            ("unterordner/manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("../document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("/base.png", ProjectSamples.Png));

        Should.Throw<ProjectFormatException>(() => ProjectSamples.Load(data));
    }

    [Fact]
    public void ZusaetzlicheEintraege_WerdenIgnoriert()
    {
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(ProjectSamples.Document())),
            ("base.png", ProjectSamples.Png),
            ("..\\..\\evil.exe", ProjectSamples.Text("x")),
            ("virus/preview.png", ProjectSamples.Png));

        var restored = ProjectSamples.Load(data);

        restored.PreviewPng.ShouldBeNull();
    }

    [Fact]
    public void ZuschnittAusserhalbDesBilds_WirdBeimLesenGeklemmt()
    {
        var document = ProjectSamples.Document(width: 100, height: 80).Replace("\"annotations\"", "\"cropRect\":[50,40,500,500],\"annotations\"", StringComparison.Ordinal);
        var data = ProjectSamples.Zip(
            ("manifest.json", ProjectSamples.Text(ProjectSamples.Manifest())),
            ("document.json", ProjectSamples.Text(document)),
            ("base.png", ProjectSamples.Png));

        ProjectSamples.Load(data).CropRect.ShouldBe(new PixelRect(50, 40, 50, 40));
    }

    [Fact]
    public void ReadPreview_LiefertNurDieVorschau()
    {
        using var stream = new MemoryStream(ProjectSamples.Save(ProjectSamples.Content()));
        ProjectSerializer.ReadPreview(stream).ShouldBe(ProjectSamples.Png);

        using var broken = new MemoryStream(ProjectSamples.Text("kein zip"));
        ProjectSerializer.ReadPreview(broken).ShouldBeNull();
    }

    [Fact]
    public void Objektliste_IstDasGleicheFormatWieImProjekt()
    {
        var originals = ProjectSamples.OneOfEach();

        var json = AnnotationSerializer.Serialize(originals);
        var restored = AnnotationSerializer.Deserialize(json);

        restored.Count.ShouldBe(originals.Count);
        ProjectSerializer.DeserializeAnnotations(json).Count.ShouldBe(originals.Count);
        Should.Throw<ProjectFormatException>(() => AnnotationSerializer.Deserialize("{kaputt"));
    }

    // ---- Golden File: verhindert unbeabsichtigte Formatänderungen ----

    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "TestData", "golden-v1.gsts");

    /// <summary>Feste Annotation-Ids und Zeiten, damit die Ausgabe reproduzierbar ist.</summary>
    internal static ProjectContent GoldenContent()
    {
        var annotations = new List<Annotation>
        {
            new RectangleAnnotation { Rect = new RectD(10, 20, 100, 50), Fill = ArgbColor.Parse("#40FF0000"), Stroke = new StrokeStyle(ArgbColor.Red, 4), CornerRadius = 8, Name = "Kasten" },
            new LineAnnotation { Start = new PointD(1, 2), End = new PointD(30, 40), EndCap = LineEndStyle.Arrow, Stroke = new StrokeStyle(ArgbColor.Black, 2, DashKind.Dash) },
            new MarkerAnnotation { Center = new PointD(60, 60), Number = 3, Created = 639000000000000000 },
            new StrokeAnnotation { Points = [new StrokePoint(1, 1, 0.5), new StrokePoint(5, 5, 1)], Stroke = new StrokeStyle(ArgbColor.Parse("#80FFFF00"), 12), IsHighlighter = true },
        };
        foreach (var (annotation, index) in annotations.Select((a, i) => (a, i)))
        {
            typeof(Annotation).GetProperty(nameof(Annotation.Id))!.SetValue(annotation, new Guid(index + 1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));
        }

        return new ProjectContent
        {
            AppVersion = "0.1.0",
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            ModifiedAt = new DateTimeOffset(2026, 1, 3, 3, 4, 5, TimeSpan.Zero),
            Width = 200,
            Height = 100,
            DpiX = 96,
            DpiY = 96,
            CropRect = new PixelRect(10, 10, 150, 80),
            Metadata = new DocumentMetadata("Rectangle", "Titel"),
            Annotations = annotations,
            BasePng = ProjectSamples.Png,
            PreviewPng = ProjectSamples.Png,
        };
    }

    [Fact]
    public void GoldenFile_FormatVersion1_LaesstSichLesen()
    {
        File.Exists(GoldenPath).ShouldBeTrue("TestData/golden-v1.gsts fehlt");

        var restored = ProjectSamples.Load(File.ReadAllBytes(GoldenPath));

        restored.Warnings.ShouldBeEmpty();
        restored.Width.ShouldBe(200);
        restored.Height.ShouldBe(100);
        restored.CropRect.ShouldBe(new PixelRect(10, 10, 150, 80));
        restored.Metadata.ShouldBe(new DocumentMetadata("Rectangle", "Titel"));
        restored.Annotations.Count.ShouldBe(4);
        var rect = restored.Annotations[0].ShouldBeOfType<RectangleAnnotation>();
        rect.Rect.ShouldBe(new RectD(10, 20, 100, 50));
        rect.Fill.ShouldBe(ArgbColor.Parse("#40FF0000"));
        rect.CornerRadius.ShouldBe(8);
        rect.Name.ShouldBe("Kasten");
        var line = restored.Annotations[1].ShouldBeOfType<LineAnnotation>();
        line.EndCap.ShouldBe(LineEndStyle.Arrow);
        line.Stroke.Dash.ShouldBe(DashKind.Dash);
        restored.Annotations[2].ShouldBeOfType<MarkerAnnotation>().Number.ShouldBe(3);
        var stroke = restored.Annotations[3].ShouldBeOfType<StrokeAnnotation>();
        stroke.Points.ShouldBe([new StrokePoint(1, 1, 0.5), new StrokePoint(5, 5, 1)]);
        stroke.IsHighlighter.ShouldBeTrue();
    }

    [Fact]
    public void GoldenFile_AktuellerSerializerErzeugtDieselbenJsonDateien()
    {
        var golden = ReadEntries(File.ReadAllBytes(GoldenPath));
        var current = ReadEntries(ProjectSamples.Save(GoldenContent()));

        current["manifest.json"].ShouldBe(golden["manifest.json"]);
        current["document.json"].ShouldBe(golden["document.json"]);
    }

    private static Dictionary<string, string> ReadEntries(byte[] data)
    {
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        return zip.Entries
            .Where(e => e.FullName.EndsWith(".json", StringComparison.Ordinal))
            .ToDictionary(e => e.FullName, e => new StreamReader(e.Open()).ReadToEnd().ReplaceLineEndings("\n"));
    }

    /// <summary>Erzeugt die Golden-Datei neu, wenn die Umgebungsvariable GSTS_GOLDEN_OUT auf den Zielpfad zeigt (bewusste Formatänderung).</summary>
    [Fact]
    public void ErzeugeGoldenFile_NurMitUmgebungsvariable()
    {
        if (Environment.GetEnvironmentVariable("GSTS_GOLDEN_OUT") is { Length: > 0 } target)
        {
            File.WriteAllBytes(target, ProjectSamples.Save(GoldenContent()));
        }
    }
}

public sealed class ProjectImmutableTests
{
    [Fact]
    public void ImmutableArray_Punkte_WerdenAlsListeGeschrieben()
    {
        var polygon = new PolygonAnnotation { Points = ImmutableArray.Create(new PointD(1, 2), new PointD(3, 4)) };

        var json = AnnotationSerializer.Serialize([polygon]);

        json.ShouldContain("[[1,2],[3,4]]");
    }
}
