using System.IO.Compression;
using System.Text.Json.Nodes;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Tests;

public sealed class FrameStyleTests
{
    [Fact]
    public void LeererRahmen_WirdZuNull()
    {
        new FrameStyle().IsEmpty.ShouldBeTrue();
        new FrameStyle().Normalized().ShouldBeNull();

        // Neutrale Werte einzelner Effekte zählen nicht als Effekt.
        new FrameStyle { BorderColor = ArgbColor.Red, TornHeight = 30, VignetteSize = 0.3, Shadow = new ShadowStyle(ArgbColor.Transparent, 9, 9) }.Normalized().ShouldBeNull();
    }

    [Fact]
    public void Graustufen_SindEinEffekt()
    {
        new FrameStyle { Grayscale = true }.Normalized().ShouldNotBeNull();
    }

    [Fact]
    public void Normalized_BegrenztWerte()
    {
        var frame = new FrameStyle
        {
            BorderLeft = 5000,
            BorderTop = -3,
            CornerRadius = 1e9,
            TornSides = (FrameSides)255,
            TornHeight = 0,
            TornWidth = 1,
            VignetteStrength = 7,
            VignetteSize = 0,
            Shadow = new ShadowStyle(ArgbColor.Black, 9999, -4),
        }.Normalized()!;

        frame.BorderLeft.ShouldBe(FrameStyle.MaxBorder);
        frame.BorderTop.ShouldBe(0);
        frame.CornerRadius.ShouldBe(FrameStyle.MaxCornerRadius);
        frame.TornSides.ShouldBe(FrameSides.All);
        frame.TornHeight.ShouldBe(FrameStyle.MinTornHeight);
        frame.TornWidth.ShouldBe(FrameStyle.MinTornWidth);
        frame.VignetteStrength.ShouldBe(1);
        frame.VignetteSize.ShouldBe(FrameStyle.MinVignetteSize);
        frame.Shadow.Size.ShouldBe(ShadowStyle.MaxSize);
        frame.Shadow.Blur.ShouldBe(0);
    }

    [Fact]
    public void Normalized_NichtEndlicheZahlen_WerdenNeutral()
    {
        var frame = new FrameStyle { CornerRadius = double.NaN, VignetteStrength = double.PositiveInfinity, Grayscale = true }.Normalized()!;

        frame.CornerRadius.ShouldBe(0);
        frame.VignetteStrength.ShouldBe(0);
    }
}

public sealed class FrameLayoutTests
{
    private static readonly PixelRect Content = new(0, 0, 100, 80);

    [Fact]
    public void OhneRahmen_AlleRechteckeGleich()
    {
        var layout = FrameLayout.Compute(Content, null);

        layout.Content.ShouldBe(Content);
        layout.Border.ShouldBe(Content);
        layout.Output.ShouldBe(Content);
    }

    [Fact]
    public void Rand_WaechstJeSeite()
    {
        var layout = FrameLayout.Compute(Content, new FrameStyle { BorderLeft = 10, BorderTop = 5, BorderRight = 20, BorderBottom = 30 });

        layout.Border.ShouldBe(PixelRect.FromLtrb(-10, -5, 120, 110));
        layout.Output.ShouldBe(layout.Border);
        layout.Content.ShouldBe(Content);
    }

    [Fact]
    public void Schatten_ErweitertNachRechtsUntenUndUmDieUnschaerfe()
    {
        var layout = FrameLayout.Compute(Content, new FrameStyle { Shadow = new ShadowStyle(ArgbColor.Black, 10, 6) });

        // Versatz 10 plus Unschärfe 6: links/oben nur die Unschärfe über den Versatz hinaus.
        layout.Output.Right.ShouldBe(116);
        layout.Output.Bottom.ShouldBe(96);
        layout.Output.X.ShouldBe(0);
        layout.Output.Y.ShouldBe(0);
        layout.Border.ShouldBe(Content);
    }

    [Fact]
    public void Riss_MachtDenRandEinheitlichBreit()
    {
        var style = new FrameStyle { BorderLeft = 4, BorderBottom = 12, TornSides = FrameSides.Bottom };

        FrameLayout.EffectiveBorders(style).ShouldBe((12, 12, 12, 12));
        FrameLayout.Compute(Content, style).Border.ShouldBe(PixelRect.FromLtrb(-12, -12, 112, 92));
    }

    [Fact]
    public void NegativeEffectiveBounds_UndZuschnitt()
    {
        var shifted = new PixelRect(-40, -10, 60, 50);

        FrameLayout.Compute(shifted, new FrameStyle { BorderLeft = 5, BorderTop = 5, BorderRight = 5, BorderBottom = 5 }).Output
            .ShouldBe(PixelRect.FromLtrb(-45, -15, 25, 45));
    }

    [Fact]
    public void Ueberstand_WirdAufDieObergrenzeBegrenzt()
    {
        var content = new PixelRect(0, 0, 10, 10);
        var layout = FrameLayout.Compute(content, new FrameStyle { BorderLeft = 1000, Shadow = new ShadowStyle(ArgbColor.Black, 200, 100) });

        layout.Output.X.ShouldBeGreaterThanOrEqualTo(-ImageDocument.MaxCanvasOverhang);
        layout.Output.Right.ShouldBeLessThanOrEqualTo(10 + ImageDocument.MaxCanvasOverhang);
    }

    [Fact]
    public void OhneSichtbarenRand_BleibtDerInhalt()
    {
        // Randbreite ohne Farbe zählt nicht.
        FrameLayout.Compute(Content, new FrameStyle { BorderColor = ArgbColor.Transparent, BorderLeft = 9, Grayscale = true }).Output.ShouldBe(Content);
    }
}

public sealed class FrameGeometryTests
{
    private static readonly RectD Rect = new(0, 0, 200, 100);

    [Fact]
    public void Riss_IstBeiGleichemSeedGleichUndBeiAnderemVerschieden()
    {
        var a = FrameGeometry.TornOutline(Rect, FrameSides.Bottom, 10, 16, seed: 7);
        var b = FrameGeometry.TornOutline(Rect, FrameSides.Bottom, 10, 16, seed: 7);
        var c = FrameGeometry.TornOutline(Rect, FrameSides.Bottom, 10, 16, seed: 8);

        a.ShouldBe(b);
        a.SequenceEqual(c).ShouldBeFalse();
    }

    [Fact]
    public void Riss_AendertNurDieGewaehltenSeiten()
    {
        var points = FrameGeometry.TornOutline(Rect, FrameSides.Bottom, 12, 16, seed: 3);

        // Alle Punkte liegen im Rechteck; Spitzen nur nahe der unteren Kante.
        points.ShouldAllBe(p => p.X >= 0 && p.X <= 200 && p.Y >= 0 && p.Y <= 100);
        points.Where(p => p.Y is > 0 and < 100 && p.X is > 0 and < 200).ShouldNotBeEmpty();
        points.Where(p => p.Y is > 0 and < 100 && p.X is > 0 and < 200).ShouldAllBe(p => p.Y >= 100 - 12 - 0.001);
        points.Take(4).ShouldContain(new PointD(200, 0));
    }

    [Fact]
    public void Riss_OhneSeiten_IstDasRechteck()
    {
        FrameGeometry.TornOutline(Rect, FrameSides.None, 10, 16, 1)
            .ShouldBe([new PointD(0, 0), new PointD(200, 0), new PointD(200, 100), new PointD(0, 100)]);
    }

    [Fact]
    public void Riss_AlleSeiten_HatZackenAnJederKante()
    {
        var points = FrameGeometry.TornOutline(Rect, FrameSides.All, 10, 16, 5);

        points.Count.ShouldBeGreaterThan(20);
        points.Any(p => p.Y > 0 && p.Y <= 10 && p.X > 0 && p.X < 200).ShouldBeTrue();
        points.Any(p => p.X > 0 && p.X <= 10 && p.Y > 0 && p.Y < 100).ShouldBeTrue();
        points.Any(p => p.X >= 190 && p.X < 200 && p.Y > 0 && p.Y < 100).ShouldBeTrue();
    }

    [Fact]
    public void Eckenradius_WirdAufDieHalbeKuerzereSeiteBegrenzt()
    {
        FrameGeometry.ClampRadius(Rect, 500).ShouldBe(50);
        FrameGeometry.ClampRadius(Rect, -4).ShouldBe(0);
        FrameGeometry.ClampRadius(Rect, double.NaN).ShouldBe(0);
    }
}

public sealed class FrameDocumentTests
{
    private static ImageDocument Document() => new(new PixelBuffer(40, 30));

    [Fact]
    public void Frame_NormalisiertLeerenWertZuNull()
    {
        var document = Document();

        document.Frame = new FrameStyle();
        document.Frame.ShouldBeNull();
        document.OutputBounds.ShouldBe(document.EffectiveBounds);
    }

    [Fact]
    public void OutputBounds_FolgtRahmenUndZuschnitt()
    {
        var document = Document();
        var changes = new List<string?>();
        document.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        document.Frame = new FrameStyle { BorderLeft = 4, BorderTop = 4, BorderRight = 4, BorderBottom = 4 };
        document.OutputBounds.ShouldBe(new PixelRect(-4, -4, 48, 38));
        changes.ShouldContain(nameof(ImageDocument.OutputBounds));

        changes.Clear();
        document.CropRect = new PixelRect(10, 10, 20, 10);
        document.OutputBounds.ShouldBe(new PixelRect(6, 6, 28, 18));
        changes.ShouldContain(nameof(ImageDocument.OutputBounds));
        document.EffectiveBounds.ShouldBe(new PixelRect(10, 10, 20, 10));
    }

    [Fact]
    public void EffectiveImage_OhneGraustufen_IstDasMaskierteBild()
    {
        var document = Document();

        document.EffectiveImage.ShouldBeSameAs(document.MaskedImage);
    }

    [Fact]
    public void EffectiveImage_Graustufen_GecachtJeMaskenstandUndAlphaBleibt()
    {
        var buffer = new PixelBuffer(4, 4);
        for (var i = 0; i < 16; i++)
        {
            buffer.Pixels[(i * 4)] = 0;
            buffer.Pixels[(i * 4) + 1] = 0;
            buffer.Pixels[(i * 4) + 2] = 255;
            buffer.Pixels[(i * 4) + 3] = i == 0 ? (byte)128 : (byte)255;
        }

        var document = new ImageDocument(buffer) { Frame = new FrameStyle { Grayscale = true } };

        var gray = document.EffectiveImage;
        gray.ShouldBeSameAs(document.EffectiveImage);
        gray.Pixels[2].ShouldBe((byte)54); // Rot (255,0,0) -> 0,2126 * 255
        gray.Pixels[0].ShouldBe(gray.Pixels[2]);
        gray.Pixels[1].ShouldBe(gray.Pixels[2]);
        gray.Pixels[3].ShouldBe((byte)128);

        // MaskedImage bleibt farbig, das Original ebenfalls.
        document.MaskedImage.Pixels[2].ShouldBe((byte)255);
        document.MaskedImage.Pixels[0].ShouldBe((byte)0);

        // Neuer Maskenstand -> neuer Puffer.
        document.Undo.Execute(new MaskEditAction(document.Mask, new PixelRect(0, 0, 1, 1), [255], [0], "Entfernen"));
        var after = document.EffectiveImage;
        after.ShouldNotBeSameAs(gray);
        after.Pixels[3].ShouldBe((byte)0);

        // Graustufen aus -> wieder das maskierte Bild.
        document.Frame = null;
        document.EffectiveImage.ShouldBeSameAs(document.MaskedImage);
    }

    [Fact]
    public void Graustufen_Weiss_BleibtWeissUndSchwarzSchwarz()
    {
        var buffer = new PixelBuffer(2, 1);
        Array.Fill(buffer.Pixels, (byte)255, 0, 4);
        buffer.Pixels[4] = 0;
        buffer.Pixels[5] = 0;
        buffer.Pixels[6] = 0;
        buffer.Pixels[7] = 255;

        var gray = buffer.ToGrayscale();

        gray.Pixels.Take(4).ShouldAllBe(b => b == 255);
        gray.Pixels.Skip(4).Take(3).ShouldAllBe(b => b == 0);
        buffer.Pixels[0].ShouldBe((byte)255);
    }

    [Fact]
    public void Undo_FasstReglerfolgenZusammenUndRedoStelltWiederHer()
    {
        var document = Document();
        FrameStyle? Current() => document.Frame;
        void Set(FrameStyle? value) => document.Undo.Execute(new PropertyChangeAction<FrameStyle?>(document, "Frame", Current(), value?.Normalized(), v => document.Frame = v, "Rahmen"));

        for (var radius = 1; radius <= 10; radius++)
        {
            Set(new FrameStyle { CornerRadius = radius });
        }

        document.Frame!.CornerRadius.ShouldBe(10);
        document.Undo.Undo();
        document.Frame.ShouldBeNull();
        document.Undo.CanUndo.ShouldBeFalse();
        document.Undo.Redo();
        document.Frame!.CornerRadius.ShouldBe(10);
    }
}

public sealed class FrameProjectTests
{
    private static ProjectContent Content(FrameStyle? frame) => new()
    {
        Width = 200,
        Height = 100,
        BasePng = ProjectSamples.Png,
        Frame = frame,
    };

    private static byte[] ReplaceFrame(byte[] data, JsonNode? replacement)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName);
                using var read = entry.Open();
                using var write = copy.Open();
                if (entry.FullName == "document.json")
                {
                    var node = JsonNode.Parse(read)!.AsObject();
                    node["frame"] = replacement;
                    write.Write(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()));
                }
                else
                {
                    read.CopyTo(write);
                }
            }
        }

        return output.ToArray();
    }

    [Fact]
    public void Rahmen_UeberstehtDenRoundtripMitAllenEffekten()
    {
        var frame = new FrameStyle
        {
            BorderColor = ArgbColor.Parse("#FF336699"),
            BorderLeft = 4,
            BorderTop = 5,
            BorderRight = 6,
            BorderBottom = 70,
            Shadow = new ShadowStyle(ArgbColor.Parse("#80000000"), 6, 12),
            CornerRadius = 14.5,
            TornSides = FrameSides.Bottom | FrameSides.Left,
            TornHeight = 9,
            TornWidth = 21,
            TornSeed = 12345,
            VignetteStrength = 0.4,
            VignetteSize = 0.7,
            Grayscale = true,
        };

        var restored = ProjectSamples.Load(ProjectSamples.Save(Content(frame)));

        restored.Warnings.ShouldBeEmpty();
        restored.Frame.ShouldBe(frame.Normalized());
    }

    [Fact]
    public void OhneRahmen_StehtKeinFeldImDokument()
    {
        var data = ProjectSamples.Save(Content(null));

        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        var json = new StreamReader(zip.GetEntry("document.json")!.Open()).ReadToEnd();
        json.ShouldNotContain("frame");
        ProjectSamples.Load(data).Frame.ShouldBeNull();
    }

    [Fact]
    public void DefekterRahmen_ErgibtWarnungOhneAbbruch()
    {
        var data = ReplaceFrame(ProjectSamples.Save(Content(null)), JsonValue.Create("kaputt"));

        var restored = ProjectSamples.Load(data);

        restored.Frame.ShouldBeNull();
        restored.Warnings.ShouldContain(w => w.Contains("Rahmen", StringComparison.Ordinal));
        restored.Width.ShouldBe(200);
    }

    [Fact]
    public void LeererRahmenInDerDatei_WirdZuNull()
    {
        var data = ReplaceFrame(ProjectSamples.Save(Content(null)), new JsonObject());

        ProjectSamples.Load(data).Frame.ShouldBeNull();
    }
}
