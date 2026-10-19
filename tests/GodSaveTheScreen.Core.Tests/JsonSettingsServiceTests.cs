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
