using System.IO.Compression;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace GodSaveTheScreen.Core.Tests;

public sealed class EmojiCatalogTests
{
    private static readonly EmojiCatalog Catalog = EmojiCatalog.Shared;

    [Theory]
    [InlineData("\U0001F600")]
    [InlineData("❤️")]
    [InlineData("1️⃣")]
    [InlineData("\U0001F468‍\U0001F469‍\U0001F467")]
    [InlineData("\U0001F3F3️‍\U0001F308")]
    [InlineData("©️")]
    public void Katalog_EnthaeltWichtigeSequenzen(string sequence) => Catalog.Find(sequence).ShouldNotBeNull();

    [Fact]
    public void Katalog_EnthaeltKeineLaenderflaggenUndKeineTagSequenzen()
    {
        foreach (var emoji in Catalog.Emojis)
        {
            var codePoints = ToCodePoints(emoji.Sequence);
            codePoints.ShouldAllBe(c => c < 0x1F1E6 || c > 0x1F1FF, $"Länderflagge: {emoji.NameEn}");
            codePoints.ShouldAllBe(c => c < 0xE0020 || c > 0xE007F, $"Tag-Sequenz: {emoji.NameEn}");
        }

        Catalog.Groups.ShouldNotContain(g => g.Name == "Component");
        Catalog.Find("\U0001F3C1").ShouldNotBeNull(); // Zielflagge bleibt
    }

    [Fact]
    public void Katalog_HatNamenInBeidenSprachenUndGruppen()
    {
        Catalog.Groups.Length.ShouldBe(9);
        Catalog.Emojis.Length.ShouldBeGreaterThan(1500);
        Catalog.Emojis.ShouldAllBe(e => e.NameDe.Length > 0 && e.NameEn.Length > 0);
        Catalog.Find("\U0001F600")!.NameDe.ShouldBe("grinsendes Gesicht");
        Catalog.Find("\U0001F600")!.NameEn.ShouldBe("grinning face");
    }

    [Fact]
    public void Hauttoene_AnwendenUndEntfernen()
    {
        const string wave = "\U0001F44B";
        const string waveMedium = "\U0001F44B\U0001F3FD";

        Catalog.ApplySkinTone(wave, SkinTone.Medium).ShouldBe(waveMedium);
        Catalog.ApplySkinTone(waveMedium, SkinTone.None).ShouldBe(wave);
        Catalog.ApplySkinTone(waveMedium, SkinTone.Dark).ShouldBe("\U0001F44B\U0001F3FF");
        Catalog.GetBaseSequence(waveMedium).ShouldBe(wave);
        Catalog.GetSkinTone(waveMedium).ShouldBe(SkinTone.Medium);
        Catalog.GetSkinTone(wave).ShouldBe(SkinTone.None);
    }

    [Fact]
    public void Hauttoene_EmojiOhneVariantenBleibtUnveraendert()
    {
        const string grin = "\U0001F600";

        Catalog.ApplySkinTone(grin, SkinTone.Dark).ShouldBe(grin);
        Catalog.Find(grin)!.HasSkinTones.ShouldBeFalse();
        Catalog.ApplySkinTone("unbekannt", SkinTone.Light).ShouldBe("unbekannt");
    }

    [Fact]
    public void Find_IgnoriertVariationSelector()
    {
        Catalog.Find("❤").ShouldBeSameAs(Catalog.Find("❤️"));
    }

    private static List<int> ToCodePoints(string text)
    {
        var result = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            result.Add(char.ConvertToUtf32(text, i));
            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }
        }

        return result;
    }
}

public sealed class EmojiSearchTests
{
    private static readonly EmojiSearch Search = new(EmojiCatalog.Shared);

    private static IEnumerable<string> Sequences(string query) => Search.Find(query).Select(e => e.Sequence);

    [Theory]
    [InlineData("lachen")]
    [InlineData("Lachen")]
    [InlineData("  LACHEN ")]
    [InlineData("smile")]
    public void Suche_FindetGrinsendesGesicht(string query) => Sequences(query).ShouldContain("\U0001F600");

    [Fact]
    public void Suche_Herz_FindetRotesHerz() => Sequences("Herz").ShouldContain("❤️");

    [Fact]
    public void Suche_Hertz_FindetNichts() => Search.Find("Hertz").ShouldBeEmpty();

    [Fact]
    public void Suche_UmlauteAlsAeOeUe()
    {
        Sequences("Haende").ShouldBe(Sequences("Hände"));
        Sequences("Haende").ShouldNotBeEmpty();
        EmojiSearch.Normalize("Größe").ShouldBe("groesse");
    }

    [Fact]
    public void Suche_MehrereWoerter_SindUndVerknuepft()
    {
        var one = Search.Find("gesicht").Count;
        var both = Search.Find("grinsendes gesicht");

        both.Count.ShouldBeGreaterThan(0);
        both.Count.ShouldBeLessThan(one);
        both[0].Sequence.ShouldBe("\U0001F600"); // exakter Name steht vorn
    }

    [Fact]
    public void Suche_Rangfolge_NamensPraefixVorStichwort()
    {
        var results = Search.Find("grinsendes");

        results[0].NameDe.ShouldStartWith("grinsendes");
        results.Select(e => e.Order).Take(3).ShouldBe(results.Select(e => e.Order).Take(3).OrderBy(o => o).ToList());
    }

    [Fact]
    public void Suche_EnglischeUndDeutscheBegriffeUnabhaengigVonDerUiSprache()
    {
        Sequences("thumbs up").ShouldContain("\U0001F44D");
        Sequences("daumen").ShouldContain("\U0001F44D");
    }

    [Fact]
    public void Suche_EingabeDesEmojisSelbst_FindetEs()
    {
        Sequences("\U0001F600").ShouldBe(["\U0001F600"]);
        Sequences("\U0001F44B\U0001F3FD").ShouldBe(["\U0001F44B"]);
    }

    [Fact]
    public void Suche_FindetSynonymeAusDerCsv()
    {
        var info = EmojiCatalog.Shared.Find("😀")!;

        info.Synonyms.ShouldNotBeEmpty();
        Search.Find(info.Synonyms[0]).Select(e => e.Sequence).ShouldContain("😀");
    }

    [Fact]
    public void Suche_Leer_LiefertAlles() => Search.Find("  ").Count.ShouldBe(EmojiCatalog.Shared.Emojis.Length);
}

public sealed class EmojiAnnotationTests
{
    private static EmojiAnnotation Create(double rotation = 0) => new()
    {
        Rect = new RectD(100, 100, 100, 40),
        Rotation = rotation,
    };

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(370, 10)]
    [InlineData(360, 0)]
    [InlineData(45.5, 45.5)]
    public void Rotation_WirdAuf0Bis360Normalisiert(double input, double expected) => Create(input).Rotation.ShouldBe(expected, 1e-9);

    [Fact]
    public void Bounds_OhneDrehung_IstDerRahmen()
    {
        Create().Bounds.ShouldBe(new RectD(100, 100, 100, 40));
    }

    [Fact]
    public void Bounds_Bei90Grad_IstDieGedrehteHuelle()
    {
        var bounds = Create(90).Bounds;

        bounds.X.ShouldBe(130, 1e-9);
        bounds.Y.ShouldBe(70, 1e-9);
        bounds.Width.ShouldBe(40, 1e-9);
        bounds.Height.ShouldBe(100, 1e-9);
    }

    [Fact]
    public void Bounds_Bei45Grad_IstDieAchsparalleleHuelle()
    {
        var bounds = Create(45).Bounds;

        bounds.Width.ShouldBe((100 + 40) / Math.Sqrt(2), 1e-9);
        bounds.Height.ShouldBe((100 + 40) / Math.Sqrt(2), 1e-9);
        bounds.Center.X.ShouldBe(150, 1e-9);
        bounds.Center.Y.ShouldBe(120, 1e-9);
    }

    [Fact]
    public void HitTest_BerueckSichtigtDieDrehung()
    {
        var emoji = Create(90); // Mitte (150, 120): gedreht 40 breit × 100 hoch

        emoji.HitTest(new PointD(150, 75), 0).ShouldBeTrue();   // oben im gedrehten Rahmen
        emoji.HitTest(new PointD(105, 120), 0).ShouldBeFalse(); // im ungedrehten, nicht im gedrehten Rahmen
        emoji.HitTest(new PointD(150, 120), 0).ShouldBeTrue();
        Create().HitTest(new PointD(105, 120), 0).ShouldBeTrue();
    }

    [Fact]
    public void HitTest_Bei45Grad_EckeAusserhalb()
    {
        var emoji = Create(45);

        emoji.HitTest(new PointD(101, 101), 0).ShouldBeFalse(); // Ecke des ungedrehten Rahmens liegt nun außerhalb
        emoji.HitTest(new PointD(150, 120), 0).ShouldBeTrue();
    }

    [Fact]
    public void Spiegelung_AendertDieTrefferflaecheNicht()
    {
        var emoji = Create();
        emoji.FlipHorizontal = true;
        emoji.FlipVertical = true;

        emoji.HitTest(new PointD(105, 105), 0).ShouldBeTrue();
        emoji.Bounds.ShouldBe(new RectD(100, 100, 100, 40));
    }

    [Fact]
    public void MapPoints_SkaliertRahmenUndBehaeltDrehung()
    {
        var emoji = Create(30);

        emoji.MapPoints(p => new PointD(p.X * 2, p.Y * 2));

        emoji.Rect.ShouldBe(new RectD(200, 200, 200, 80));
        emoji.Rotation.ShouldBe(30);
    }

    [Fact]
    public void Clone_KopiertAlleEigenschaften()
    {
        var original = new EmojiAnnotation { Sequence = "\U0001F44D\U0001F3FD", Rect = new RectD(1, 2, 3, 4), Rotation = 12, FlipHorizontal = true, FlipVertical = true, Opacity = 0.5, Name = "x" };

        var copy = (EmojiAnnotation)original.Clone();

        copy.Id.ShouldNotBe(original.Id);
        copy.Sequence.ShouldBe(original.Sequence);
        copy.Rect.ShouldBe(original.Rect);
        copy.Rotation.ShouldBe(12);
        copy.FlipHorizontal.ShouldBeTrue();
        copy.FlipVertical.ShouldBeTrue();
        copy.Opacity.ShouldBe(0.5);
        copy.Name.ShouldBe("x");
    }

    [Fact]
    public void Json_Roundtrip()
    {
        var original = new EmojiAnnotation { Sequence = "\U0001F468‍\U0001F469‍\U0001F467", Rect = new RectD(10.5, 20, 64, 32), Rotation = 99.25, FlipHorizontal = true, Opacity = 0.75 };

        var json = AnnotationSerializer.Serialize([original]);
        var restored = AnnotationSerializer.Deserialize(json).ShouldHaveSingleItem().ShouldBeOfType<EmojiAnnotation>();

        json.ShouldContain("\"type\":\"emoji\"");
        restored.Sequence.ShouldBe(original.Sequence);
        restored.Rect.ShouldBe(original.Rect);
        restored.Rotation.ShouldBe(99.25);
        restored.FlipHorizontal.ShouldBeTrue();
        restored.FlipVertical.ShouldBeFalse();
        restored.Opacity.ShouldBe(0.75);
        restored.Id.ShouldBe(original.Id);
    }

    [Fact]
    public void RotationMath_DrehtImUhrzeigersinn()
    {
        var rotated = RotationMath.Rotate(new PointD(10, 0), new PointD(0, 0), 90);

        rotated.X.ShouldBe(0, 1e-9);
        rotated.Y.ShouldBe(10, 1e-9);
        RotationMath.Unrotate(rotated, new PointD(0, 0), 90).X.ShouldBe(10, 1e-9);
    }
}

public sealed class EmojiSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsts-emoji-" + Guid.NewGuid().ToString("N"));

    public EmojiSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private JsonSettingsService Create() =>
        new(new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local")), NullLogger<JsonSettingsService>.Instance);

    [Fact]
    public async Task Einstellungen_UeberstehenNeuladen()
    {
        var service = Create();
        service.Current = new AppSettings
        {
            EmojiDefaults = new EmojiStyle { Opacity = 0.4, DefaultSize = 128, SkinTone = SkinTone.MediumDark },
            EmojiRecent = new StringList(["\U0001F600", "\U0001F44D"]),
            EmojiFavorites = new StringList(["❤️"]),
        };
        await service.SaveAsync(TestContext.Current.CancellationToken);

        var reloaded = Create().Current;

        reloaded.EmojiDefaults.ShouldBe(new EmojiStyle { Opacity = 0.4, DefaultSize = 128, SkinTone = SkinTone.MediumDark });
        reloaded.EmojiRecent.ShouldBe(["\U0001F600", "\U0001F44D"]);
        reloaded.EmojiFavorites.ShouldBe(["❤️"]);
    }

    [Fact]
    public void AlteDatei_OhneEmojiFelder_LiefertStandardwerte()
    {
        var path = Path.Combine(_root, "roaming");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "settings.json"), "{ \"language\": \"de\" }");

        var current = Create().Current;

        current.EmojiDefaults.DefaultSize.ShouldBe(96);
        current.EmojiDefaults.Opacity.ShouldBe(1);
        current.EmojiRecent.ShouldBeEmpty();
        current.EmojiFavorites.ShouldBeEmpty();
    }
}

public sealed class EmojiGoldenFileTests
{
    private static string GoldenPath => Path.Combine(AppContext.BaseDirectory, "TestData", "golden-v1-emoji.gsts");

    internal static ProjectContent GoldenContent()
    {
        var content = ProjectSerializerTests.GoldenContent();
        var emoji = new EmojiAnnotation
        {
            Sequence = "\U0001F44D\U0001F3FD",
            Rect = new RectD(20, 30, 48, 40),
            Rotation = 15,
            FlipHorizontal = true,
            Opacity = 0.8,
            Name = "Emoji",
        };
        typeof(Annotation).GetProperty(nameof(Annotation.Id))!.SetValue(emoji, new Guid(9, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));
        return content with { Annotations = [.. content.Annotations, emoji] };
    }

    [Fact]
    public void GoldenFile_MitEmoji_LaesstSichLesen()
    {
        File.Exists(GoldenPath).ShouldBeTrue("TestData/golden-v1-emoji.gsts fehlt");

        var restored = ProjectSamples.Load(File.ReadAllBytes(GoldenPath));

        restored.Warnings.ShouldBeEmpty();
        restored.Annotations.Count.ShouldBe(5);
        var emoji = restored.Annotations[4].ShouldBeOfType<EmojiAnnotation>();
        emoji.Sequence.ShouldBe("\U0001F44D\U0001F3FD");
        emoji.Rect.ShouldBe(new RectD(20, 30, 48, 40));
        emoji.Rotation.ShouldBe(15);
        emoji.FlipHorizontal.ShouldBeTrue();
        emoji.Opacity.ShouldBe(0.8);
    }

    [Fact]
    public void GoldenFile_MitEmoji_AktuellerSerializerErzeugtDieselbenJsonDateien()
    {
        var golden = ReadEntries(File.ReadAllBytes(GoldenPath));
        var current = ReadEntries(ProjectSamples.Save(GoldenContent()));

        current["manifest.json"].ShouldBe(golden["manifest.json"]);
        current["document.json"].ShouldBe(golden["document.json"]);
    }

    [Fact]
    public void AelterePhaseDatei_OhneEmoji_BleibtLesbar()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "golden-v1.gsts");

        ProjectSamples.Load(File.ReadAllBytes(path)).Annotations.Count.ShouldBe(4);
    }

    private static Dictionary<string, string> ReadEntries(byte[] data)
    {
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        return zip.Entries
            .Where(e => e.FullName.EndsWith(".json", StringComparison.Ordinal))
            .ToDictionary(e => e.FullName, e => new StreamReader(e.Open()).ReadToEnd().ReplaceLineEndings("\n"));
    }

    /// <summary>Erzeugt die Golden-Datei neu, wenn <c>GSTS_GOLDEN_EMOJI_OUT</c> auf den Zielpfad zeigt (bewusste Formatänderung).</summary>
    [Fact]
    public void ErzeugeGoldenFile_NurMitUmgebungsvariable()
    {
        if (Environment.GetEnvironmentVariable("GSTS_GOLDEN_EMOJI_OUT") is { Length: > 0 } target)
        {
            File.WriteAllBytes(target, ProjectSamples.Save(GoldenContent()));
        }
    }
}
