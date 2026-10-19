using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace GodSaveTheScreen.Core.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsts-tests-" + Guid.NewGuid().ToString("N"));

    public JsonSettingsServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private JsonSettingsService Create() =>
        new(new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local")), NullLogger<JsonSettingsService>.Instance);

    [Fact]
    public void Load_DateiFehlt_LiefertStandardwerte()
    {
        Create().Current.ShouldBe(new AppSettings());
    }

    [Fact]
    public async Task Save_Roundtrip_BehaeltWerte()
    {
        var service = Create();
        service.Current = new AppSettings { Language = "en", Theme = ThemeSetting.Dark, LogLevel = LogLevelSetting.Debug };
        await service.SaveAsync(TestContext.Current.CancellationToken);

        var reloaded = Create();
        reloaded.Current.Language.ShouldBe("en");
        reloaded.Current.Theme.ShouldBe(ThemeSetting.Dark);
        reloaded.Current.LogLevel.ShouldBe(LogLevelSetting.Debug);
    }

    [Fact]
    public async Task Save_Roundtrip_BehaeltEditorEinstellungen()
    {
        var service = Create();
        service.Current = new AppSettings
        {
            JpegQuality = 77,
            TiffCompression = TiffCompressionSetting.Zip,
            FlattenBackground = new GodSaveTheScreen.Core.Annotations.ArgbColor(255, 10, 20, 30),
            LastSaveFormat = ImageFormatSetting.JpegXr,
            ShapeDefaults = new ShapeDefaultsMap
            {
                ["rectangle"] = new ShapeStyle
                {
                    Stroke = new GodSaveTheScreen.Core.Annotations.StrokeStyle(GodSaveTheScreen.Core.Annotations.ArgbColor.FromRgb(1, 2, 3), 9, GodSaveTheScreen.Core.Annotations.DashKind.Dot),
                    CornerRadius = 4,
                },
            },
            RecentColors = new StringList(["#FF112233", "#80FFFFFF"]),
            RecentFonts = new StringList(["Consolas"]),
            TextDefaults = new TextStyle { FontFamilyName = "Arial", FontSize = 40, Decorations = GodSaveTheScreen.Core.Annotations.TextDecorationKind.Underline },
            MarkerDefaults = new MarkerStyle { Size = 50, Shape = GodSaveTheScreen.Core.Annotations.MarkerShape.Square },
            MarkerStartNumber = 3,
        };
        await service.SaveAsync(TestContext.Current.CancellationToken);

        var reloaded = Create().Current;

        reloaded.JpegQuality.ShouldBe(77);
        reloaded.TiffCompression.ShouldBe(TiffCompressionSetting.Zip);
        reloaded.FlattenBackground.ShouldBe(new GodSaveTheScreen.Core.Annotations.ArgbColor(255, 10, 20, 30));
        reloaded.LastSaveFormat.ShouldBe(ImageFormatSetting.JpegXr);
        reloaded.ShapeDefaults["rectangle"].Stroke.Thickness.ShouldBe(9);
        reloaded.ShapeDefaults["rectangle"].Stroke.Dash.ShouldBe(GodSaveTheScreen.Core.Annotations.DashKind.Dot);
        reloaded.ShapeDefaults["rectangle"].CornerRadius.ShouldBe(4);
        reloaded.RecentColors.ShouldBe(["#FF112233", "#80FFFFFF"]);
        reloaded.RecentFonts.ShouldBe(["Consolas"]);
        reloaded.TextDefaults.FontFamilyName.ShouldBe("Arial");
        reloaded.TextDefaults.Decorations.ShouldBe(GodSaveTheScreen.Core.Annotations.TextDecorationKind.Underline);
        reloaded.MarkerDefaults.Shape.ShouldBe(GodSaveTheScreen.Core.Annotations.MarkerShape.Square);
        reloaded.MarkerStartNumber.ShouldBe(3);
    }

    [Fact]
    public void Load_AelterDateiOhneEditorFelder_LiefertBenutzbareStandardwerte()
    {
        var dir = Path.Combine(_root, "roaming", "GodSaveTheScreen");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{ \"Language\": \"de\", \"AutoSave\": true }");

        var settings = Create().Current;

        settings.Language.ShouldBe("de");
        settings.AutoSave.ShouldBeTrue();
        settings.ShapeDefaults.ShouldNotBeNull();
        settings.RecentColors.ShouldNotBeNull();
        settings.RecentFonts.ShouldNotBeNull();
        settings.TextDefaults.Stroke.ShouldNotBeNull();
        settings.TextDefaults.FontFamilyName.ShouldNotBeNullOrEmpty();
        settings.MarkerDefaults.Stroke.ShouldNotBeNull();
        settings.FileNamePattern.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Save_ZweimalHintereinander_ErsetztDateiAtomisch()
    {
        var service = Create();
        await service.SaveAsync(TestContext.Current.CancellationToken);
        service.Current = service.Current with { Language = "de" };
        await service.SaveAsync(TestContext.Current.CancellationToken);

        Create().Current.Language.ShouldBe("de");
        File.Exists(Path.Combine(_root, "roaming", "GodSaveTheScreen", "settings.json.tmp")).ShouldBeFalse();
    }

    [Fact]
    public async Task Save_DateiGesperrt_WirftNichtUndBehaeltWerteImSpeicher()
    {
        var dir = Path.Combine(_root, "roaming", "GodSaveTheScreen");
        Directory.CreateDirectory(dir);
        var service = Create();
        service.Current = service.Current with { Language = "en" };

        // Die temporäre Zieldatei ist durch einen anderen Prozess gesperrt.
        using (new FileStream(Path.Combine(dir, "settings.json.tmp"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await service.SaveAsync(TestContext.Current.CancellationToken);
        }

        service.Current.Language.ShouldBe("en");
        File.Exists(Path.Combine(dir, "settings.json")).ShouldBeFalse();
    }

    [Fact]
    public void Load_BeschaedigteDatei_LiefertStandardwerte()
    {
        var path = Path.Combine(_root, "roaming", "GodSaveTheScreen");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "settings.json"), "{ das ist kein json");

        Create().Current.ShouldBe(new AppSettings());
    }

    [Fact]
    public void Load_UnbekannteUndFehlendeFelder_WerdenToleriert()
    {
        var path = Path.Combine(_root, "roaming", "GodSaveTheScreen");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "settings.json"), """{ "Language": "de", "Unbekannt": 42 }""");

        var settings = Create().Current;
        settings.Language.ShouldBe("de");
        settings.Theme.ShouldBe(ThemeSetting.System);
    }

    [Fact]
    public void AppPaths_MitPortableTxt_NutztDataVerzeichnisNebenExe()
    {
        File.WriteAllText(Path.Combine(_root, "portable.txt"), string.Empty);

        var paths = new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));

        paths.IsPortable.ShouldBeTrue();
        paths.DataDirectory.ShouldBe(Path.Combine(_root, "data"));
        paths.SettingsFilePath.ShouldBe(Path.Combine(_root, "data", "settings.json"));
    }

    [Fact]
    public void AppPaths_OhnePortableTxt_NutztAppData()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));

        paths.IsPortable.ShouldBeFalse();
        paths.SettingsFilePath.ShouldBe(Path.Combine(_root, "roaming", "GodSaveTheScreen", "settings.json"));
        paths.LogDirectory.ShouldBe(Path.Combine(_root, "local", "GodSaveTheScreen", "logs"));
    }
}
