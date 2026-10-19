using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace GodSaveTheScreen.Core.Tests;

public sealed class PresetNamesTests
{
    [Theory]
    [InlineData("Rot", "rot")]
    [InlineData("Rot", "ROT")]
    [InlineData("  Rot ", "rot")]
    [InlineData("Größe", "GRÖSSE", false)]
    [InlineData("Größe", "GRÖßE")]
    [InlineData("Ärger", "ärger")]
    public void AreSame_IgnoriertGrossKleinschreibungUndRandleerzeichen(string a, string b, bool expected = true) =>
        PresetNames.AreSame(a, b).ShouldBe(expected);

    [Fact]
    public void AreSame_VerschiedeneNamen_SindNichtGleich() => PresetNames.AreSame("Rot", "Rot2").ShouldBeFalse();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("a", true)]
    [InlineData("  Mein Stil  ", true)]
    public void IsValid_PrueftLeereUndLaenge(string? name, bool expected) => PresetNames.IsValid(name).ShouldBe(expected);

    [Fact]
    public void IsValid_ZuLangerName_IstUngueltig() => PresetNames.IsValid(new string('x', PresetNames.MaxLength + 1)).ShouldBeFalse();

    [Fact]
    public void Clean_EntferntRandleerzeichen() => PresetNames.Clean("  Rot \t").ShouldBe("Rot");
}

public sealed class ToolPresetSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gsts-m12-" + Guid.NewGuid().ToString("N"));

    public ToolPresetSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private JsonSettingsService Create() =>
        new(new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local")), NullLogger<JsonSettingsService>.Instance);

    [Fact]
    public async Task Roundtrip_BehaeltVorlagenUndLetzteKonfiguration()
    {
        var service = Create();
        service.Current = new AppSettings
        {
            PresetsSeeded = true,
            FreehandDefaults = new FreehandStyle { Smoothing = 0.8, UsePressure = false },
            BlurDefaults = new BlurStyle { Mode = BlurMode.Redact, Strength = 33, Shape = RegionShape.Ellipse, FeatherEdge = 5, RedactColor = ArgbColor.FromRgb(1, 2, 3) },
            MagnifierDefaults = new MagnifierStyle { Zoom = 4, Shape = MagnifierShape.Rectangle, ShowConnector = false, Border = new StrokeStyle(ArgbColor.FromRgb(9, 9, 9), 7) },
            ToolPresets = new ToolPresetMap
            {
                ["rectangle"] = new ToolPresetList([new ToolPreset { Name = "Rot", Shape = new ShapeStyle { CornerRadius = 8, Opacity = 0.5 } }]),
                ["pen"] = new ToolPresetList([new ToolPreset { Name = "Fein", Shape = new ShapeStyle(), Smoothing = 0.2, UsePressure = false }]),
                ["text"] = new ToolPresetList([new ToolPreset { Name = "Groß", Text = new TextStyle { FontSize = 64, FontFamilyName = "Consolas" } }]),
                ["marker"] = new ToolPresetList([new ToolPreset { Name = "Schritt", Marker = new MarkerStyle { Size = 50 } }]),
                ["blur"] = new ToolPresetList([new ToolPreset { Name = "Schwärzen", Blur = new BlurStyle { Mode = BlurMode.Redact } }]),
                ["magnifier"] = new ToolPresetList([new ToolPreset { Name = "Stark", Magnifier = new MagnifierStyle { Zoom = 6 } }]),
            },
        };
        await service.SaveAsync(TestContext.Current.CancellationToken);

        var loaded = Create().Current;

        loaded.PresetsSeeded.ShouldBeTrue();
        loaded.FreehandDefaults.Smoothing.ShouldBe(0.8);
        loaded.FreehandDefaults.UsePressure.ShouldBeFalse();
        loaded.BlurDefaults.Mode.ShouldBe(BlurMode.Redact);
        loaded.BlurDefaults.Strength.ShouldBe(33);
        loaded.BlurDefaults.Shape.ShouldBe(RegionShape.Ellipse);
        loaded.BlurDefaults.RedactColor.ShouldBe(ArgbColor.FromRgb(1, 2, 3));
        loaded.MagnifierDefaults.Zoom.ShouldBe(4);
        loaded.MagnifierDefaults.ShowConnector.ShouldBeFalse();
        loaded.MagnifierDefaults.Border.Thickness.ShouldBe(7);
        loaded.ToolPresets["rectangle"].Single().Shape!.CornerRadius.ShouldBe(8);
        loaded.ToolPresets["pen"].Single().Smoothing.ShouldBe(0.2);
        loaded.ToolPresets["pen"].Single().UsePressure.ShouldBe(false);
        loaded.ToolPresets["text"].Single().Name.ShouldBe("Groß");
        loaded.ToolPresets["text"].Single().Text!.FontFamilyName.ShouldBe("Consolas");
        loaded.ToolPresets["marker"].Single().Marker!.Size.ShouldBe(50);
        loaded.ToolPresets["blur"].Single().Blur!.Mode.ShouldBe(BlurMode.Redact);
        loaded.ToolPresets["magnifier"].Single().Magnifier!.Zoom.ShouldBe(6);
    }

    [Fact]
    public void AlteEinstellungenOhneVorlagen_LadenMitStandardwerten()
    {
        var paths = new AppPaths(_root, Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFilePath)!);
        File.WriteAllText(paths.SettingsFilePath, "{ \"Language\": \"de\" }");
        var loaded = Create().Current.Normalized();

        loaded.ToolPresets.ShouldBeEmpty();
        loaded.PresetsSeeded.ShouldBeFalse();
        loaded.BlurDefaults.ShouldBe(new BlurStyle());
        loaded.MagnifierDefaults.ShouldBe(new MagnifierStyle());
        loaded.FreehandDefaults.ShouldBe(new FreehandStyle());
    }

    [Fact]
    public void Gleichheit_VergleichtVorlagenInhalte()
    {
        var a = new AppSettings { ToolPresets = new ToolPresetMap { ["x"] = new ToolPresetList([new ToolPreset { Name = "A" }]) } };
        var b = new AppSettings { ToolPresets = new ToolPresetMap { ["x"] = new ToolPresetList([new ToolPreset { Name = "A" }]) } };
        var c = new AppSettings { ToolPresets = new ToolPresetMap { ["x"] = new ToolPresetList([new ToolPreset { Name = "B" }]) } };

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }
}
