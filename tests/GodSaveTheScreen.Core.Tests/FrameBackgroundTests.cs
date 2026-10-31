using System.IO.Compression;
using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;

namespace GodSaveTheScreen.Core.Tests;

public sealed class FrameBackgroundStyleTests
{
    private static FrameBackground Solid => new() { Kind = FrameBackgroundKind.Solid, ColorA = ArgbColor.Red };

    [Fact]
    public void Hintergrund_PaddingUndFormat_SindEffekte()
    {
        new FrameStyle { Background = Solid }.Normalized().ShouldNotBeNull();
        new FrameStyle { Padding = 12 }.Normalized().ShouldNotBeNull();
        new FrameStyle { AspectRatio = new FrameAspect { Kind = FrameAspectKind.Square } }.Normalized().ShouldNotBeNull();
    }

    [Fact]
    public void NeutraleWerte_ZaehlenNichtAlsEffekt()
    {
        new FrameStyle { Background = FrameBackground.None }.Normalized().ShouldBeNull();
        new FrameStyle { Background = Solid with { ColorA = ArgbColor.Transparent } }.HasBackground.ShouldBeFalse();
        new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image } }.HasBackground.ShouldBeFalse();
        new FrameStyle { AspectRatio = FrameAspect.Free }.Normalized().ShouldBeNull();
        new FrameStyle { Padding = 0 }.Normalized().ShouldBeNull();
    }

    [Fact]
    public void Normalized_BegrenztPaddingUndVerhaeltnis()
    {
        var frame = new FrameStyle
        {
            Padding = 99999,
            AspectRatio = new FrameAspect { Kind = FrameAspectKind.Custom, Width = 5000, Height = -3 },
            Background = Solid with { AngleDegrees = double.NaN },
        }.Normalized()!;

        frame.Padding.ShouldBe(FrameStyle.MaxPadding);
        frame.AspectRatio!.Ratio.ShouldBe((FrameAspect.MaxPart, 1));
        frame.Background!.AngleDegrees.ShouldBe(FrameBackground.DefaultAngle);
    }

    [Fact]
    public void Verhaeltnisse_LiefernDieErwartetenWerte()
    {
        new FrameAspect { Kind = FrameAspectKind.Wide16x9 }.Ratio.ShouldBe((16, 9));
        new FrameAspect { Kind = FrameAspectKind.Standard4x3 }.Ratio.ShouldBe((4, 3));
        new FrameAspect { Kind = FrameAspectKind.Square }.Ratio.ShouldBe((1, 1));
        new FrameAspect { Kind = FrameAspectKind.Tall9x16 }.Ratio.ShouldBe((9, 16));
        new FrameAspect { Kind = FrameAspectKind.Custom, Width = 21, Height = 9 }.Ratio.ShouldBe((21, 9));
        FrameAspect.Free.Ratio.ShouldBeNull();
    }

    [Fact]
    public void WithoutImage_BehaeltDieArt()
    {
        var asset = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 10 };

        var stripped = frame.WithoutImage();

        stripped.Background!.Kind.ShouldBe(FrameBackgroundKind.Image);
        stripped.Background.Image.ShouldBeNull();
        stripped.Clamped().Background.ShouldNotBeNull();
    }

    [Fact]
    public void FromPreset_BildVorlage_BehaeltDasAktuelleBild()
    {
        var asset = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var preset = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, ColorA = ArgbColor.Red }, Padding = 20 };
        var current = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset } };

        FrameStyle.FromPreset(preset, current).Background!.Image.ShouldBe(asset);
    }

    [Fact]
    public void FromPreset_BildVorlageOhneBild_WirdEinfarbig()
    {
        var preset = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, ColorA = ArgbColor.Red }, Padding = 20 };

        var result = FrameStyle.FromPreset(preset, null);

        result.Background!.Kind.ShouldBe(FrameBackgroundKind.Solid);
        result.Background.ColorA.ShouldBe(ArgbColor.Red);
        result.HasBackground.ShouldBeTrue();
    }

    [Fact]
    public void FromPreset_HintergrundOhneBild_BleibtUnveraendert()
    {
        var preset = new FrameStyle { Background = Solid, Padding = 20 };

        FrameStyle.FromPreset(preset, null).ShouldBe(preset);
    }
}

public sealed class FrameLayoutBackgroundTests
{
    private static readonly PixelRect Content = new(0, 0, 400, 300);

    [Fact]
    public void Innenabstand_UmschliesstDenRand()
    {
        var layout = FrameLayout.Compute(Content, new FrameStyle { BorderLeft = 10, BorderTop = 10, BorderRight = 10, BorderBottom = 10, Padding = 32 });

        layout.Border.ShouldBe(new PixelRect(-10, -10, 420, 320));
        layout.Output.ShouldBe(new PixelRect(-42, -42, 484, 384));
    }

    [Fact]
    public void Innenabstand_OhneRandUndSchatten()
    {
        FrameLayout.Compute(Content, new FrameStyle { Padding = 20 }).Output.ShouldBe(new PixelRect(-20, -20, 440, 340));
    }

    [Fact]
    public void Schatten_BleibtImInnenabstand()
    {
        var shadow = new ShadowStyle(new ArgbColor(100, 0, 0, 0), 4, 8);
        var withoutPadding = FrameLayout.Compute(Content, new FrameStyle { Shadow = shadow }).Output;
        var withPadding = FrameLayout.Compute(Content, new FrameStyle { Shadow = shadow, Padding = 200 }).Output;

        withoutPadding.Width.ShouldBeGreaterThan(Content.Width);
        withPadding.ShouldBe(new PixelRect(-200, -200, 800, 700));
    }

    [Theory]
    [InlineData(FrameAspectKind.Wide16x9, 16, 9)]
    [InlineData(FrameAspectKind.Standard4x3, 4, 3)]
    [InlineData(FrameAspectKind.Square, 1, 1)]
    [InlineData(FrameAspectKind.Tall9x16, 9, 16)]
    public void Verhaeltnis_QuerUndHochformat(FrameAspectKind kind, int w, int h)
    {
        foreach (var content in new[] { new PixelRect(0, 0, 400, 300), new PixelRect(0, 0, 300, 500), new PixelRect(5, 7, 333, 211) })
        {
            var layout = FrameLayout.Compute(content, new FrameStyle { Padding = 16, AspectRatio = new FrameAspect { Kind = kind } });

            layout.AspectLimited.ShouldBeFalse();
            var output = layout.Output;
            Math.Abs(((double)output.Width / output.Height) - ((double)w / h)).ShouldBeLessThan(1.0 / Math.Min(output.Width, output.Height) + 1e-9);
            output.Width.ShouldBeGreaterThanOrEqualTo(content.Width + 32);
            output.Height.ShouldBeGreaterThanOrEqualTo(content.Height + 32);
        }
    }

    [Fact]
    public void Verhaeltnis_VergroessertNurEineAchse_UndZentriert()
    {
        var layout = FrameLayout.Compute(Content, new FrameStyle { AspectRatio = new FrameAspect { Kind = FrameAspectKind.Square } });

        layout.Output.ShouldBe(new PixelRect(0, -50, 400, 400));
        layout.Content.ShouldBe(Content);
    }

    [Fact]
    public void Verhaeltnis_UngeraderRest_KommtNachRechtsUndUnten()
    {
        // 300 × 200 auf 1:1: 100 Pixel Differenz; 301 × 200: 101 -> 50 links, 51 rechts.
        var output = FrameLayout.Compute(new PixelRect(0, 0, 200, 301), new FrameStyle { AspectRatio = new FrameAspect { Kind = FrameAspectKind.Square } }).Output;

        output.Width.ShouldBe(301);
        output.X.ShouldBe(-50);
        output.Right.ShouldBe(251);
    }

    [Fact]
    public void Verhaeltnis_BenutzerdefiniertEntsprichtDemVerhaeltnis()
    {
        var output = FrameLayout.Compute(Content, new FrameStyle { AspectRatio = new FrameAspect { Kind = FrameAspectKind.Custom, Width = 21, Height = 9 } }).Output;

        output.Height.ShouldBe(300);
        output.Width.ShouldBe(700);
    }

    [Fact]
    public void Verhaeltnis_VerkleinertNie()
    {
        foreach (var kind in Enum.GetValues<FrameAspectKind>())
        {
            var output = FrameLayout.Compute(Content, new FrameStyle { Padding = 10, AspectRatio = new FrameAspect { Kind = kind } }).Output;

            output.Width.ShouldBeGreaterThanOrEqualTo(420);
            output.Height.ShouldBeGreaterThanOrEqualTo(320);
        }
    }

    [Fact]
    public void Verhaeltnis_ZuGrosseAusgabe_WirdNichtAngewendet()
    {
        // 9:16 auf ein 12000 Pixel breites Bild ergäbe über 21000 Pixel Höhe.
        var wide = new PixelRect(0, 0, 12000, 400);
        var layout = FrameLayout.Compute(wide, new FrameStyle { Padding = 10, AspectRatio = new FrameAspect { Kind = FrameAspectKind.Tall9x16 } });

        layout.AspectLimited.ShouldBeTrue();
        layout.Output.ShouldBe(new PixelRect(-10, -10, 12020, 420));
    }

    [Fact]
    public void OhneFixesVerhaeltnis_IstNichtsBegrenzt()
    {
        FrameLayout.Compute(Content, new FrameStyle { Padding = 10 }).AspectLimited.ShouldBeFalse();
        FrameLayout.Compute(Content, null).AspectLimited.ShouldBeFalse();
    }

    [Fact]
    public void OutputBounds_FolgtDemDokument()
    {
        var document = new ImageDocument(new PixelBuffer(200, 100))
        {
            Frame = new FrameStyle { Padding = 8, AspectRatio = new FrameAspect { Kind = FrameAspectKind.Square } },
        };

        document.OutputBounds.Width.ShouldBe(document.OutputBounds.Height);
        document.OutputBounds.Width.ShouldBe(216);
    }
}

public sealed class FrameBackgroundProjectTests
{
    private static readonly byte[] Svg = System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><rect width='10' height='10'/></svg>");

    private static ProjectContent Content(FrameStyle? frame, params Annotation[] annotations) => new()
    {
        Width = 200,
        Height = 100,
        BasePng = ProjectSamples.Png,
        Frame = frame,
        Annotations = annotations,
    };

    [Theory]
    [InlineData(FrameBackgroundKind.Solid)]
    [InlineData(FrameBackgroundKind.Gradient)]
    public void Roundtrip_FarbHintergruende(FrameBackgroundKind kind)
    {
        var frame = new FrameStyle
        {
            Background = new FrameBackground { Kind = kind, ColorA = ArgbColor.Parse("#FF112233"), ColorB = ArgbColor.Parse("#FF445566"), AngleDegrees = 45 },
            Padding = 40,
            AspectRatio = new FrameAspect { Kind = FrameAspectKind.Custom, Width = 5, Height = 4 },
        };

        var restored = ProjectSamples.Load(ProjectSamples.Save(Content(frame)));

        restored.Warnings.ShouldBeEmpty();
        restored.Frame.ShouldBe(frame.Normalized());
    }

    [Theory]
    [InlineData(ImageAssetFormat.Png)]
    [InlineData(ImageAssetFormat.Svg)]
    public void Roundtrip_Hintergrundbild_WirdAlsEintragGespeichert(ImageAssetFormat format)
    {
        var asset = new ImageAsset(format, format == ImageAssetFormat.Svg ? Svg : ProjectSamples.Png);
        var frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 24 };

        var data = ProjectSamples.Save(Content(frame));
        var restored = ProjectSamples.Load(data);

        restored.Warnings.ShouldBeEmpty();
        restored.Frame!.Background!.Image.ShouldBe(asset);
        restored.Frame.Background.Image!.Data.ShouldBe(asset.Data);

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        zip.Entries.Count(e => e.FullName.StartsWith("image-", StringComparison.Ordinal)).ShouldBe(1);
        var json = new StreamReader(zip.GetEntry("document.json")!.Open()).ReadToEnd();
        json.ShouldNotContain("\"data\"");
    }

    [Fact]
    public void GleicheDateiAlsObjektUndHintergrund_WirdNurEinmalGespeichert()
    {
        var asset = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var annotation = new ImageAnnotation { Source = asset, Rect = new RectD(0, 0, 10, 10), OriginalSize = new SizeD(1, 1) };
        var frame = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 24 };

        var data = ProjectSamples.Save(Content(frame, annotation));
        var restored = ProjectSamples.Load(data);

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        zip.Entries.Count(e => e.FullName.StartsWith("image-", StringComparison.Ordinal)).ShouldBe(1);
        restored.Warnings.ShouldBeEmpty();
        restored.Annotations.ShouldHaveSingleItem();
        restored.Frame!.Background!.Image.ShouldBe(asset);
    }

    [Fact]
    public void FehlendeBilddatei_ErgibtWarnung_UndDerRestDesRahmensBleibt()
    {
        var asset = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var frame = new FrameStyle
        {
            Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset },
            Padding = 24,
            CornerRadius = 12,
        };
        var data = RemoveEntries(ProjectSamples.Save(Content(frame)), asset.EntryName);

        var restored = ProjectSamples.Load(data);

        restored.Warnings.ShouldContain(w => w.Contains("Hintergrundbild", StringComparison.Ordinal));
        restored.Frame.ShouldNotBeNull();
        restored.Frame.Background.ShouldBeNull();
        restored.Frame.Padding.ShouldBe(24);
        restored.Frame.CornerRadius.ShouldBe(12);
    }

    [Fact]
    public void RahmenOhneNeueFelder_WirdUnveraendertGelesen()
    {
        // Datei im Stand von M14: nur Rand und Ecken, keines der neuen Felder.
        var frame = new FrameStyle { BorderLeft = 4, BorderTop = 4, BorderRight = 4, BorderBottom = 4, CornerRadius = 9 };
        var data = ProjectSamples.Save(Content(frame));

        using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
        {
            var json = new StreamReader(zip.GetEntry("document.json")!.Open()).ReadToEnd();
            json.ShouldNotContain("\"Background\"");
            json.ShouldNotContain("\"Padding\"");
            json.ShouldNotContain("\"AspectRatio\"");
        }

        var restored = ProjectSamples.Load(data);
        restored.Frame.ShouldBe(frame.Normalized());
        restored.Frame!.Background.ShouldBeNull();
        restored.Frame.Padding.ShouldBe(0);
        restored.Frame.AspectRatio.ShouldBeNull();
    }

    [Fact]
    public void Einstellungen_SpeichernKeinHintergrundbild()
    {
        var asset = new ImageAsset(ImageAssetFormat.Png, ProjectSamples.Png);
        var settings = new Core.Settings.AppSettings
        {
            FrameDefaults = new FrameStyle { Background = new FrameBackground { Kind = FrameBackgroundKind.Image, Image = asset }, Padding = 30 },
        }.Normalized();

        settings.FrameDefaults.Background.ShouldNotBeNull();
        settings.FrameDefaults.Background.Image.ShouldBeNull();
        settings.FrameDefaults.Background.Kind.ShouldBe(FrameBackgroundKind.Image);
    }

    private static byte[] RemoveEntries(byte[] data, string name)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries.Where(e => e.FullName != name))
            {
                var copy = target.CreateEntry(entry.FullName);
                using var read = entry.Open();
                using var write = copy.Open();
                read.CopyTo(write);
            }
        }

        return output.ToArray();
    }
}
