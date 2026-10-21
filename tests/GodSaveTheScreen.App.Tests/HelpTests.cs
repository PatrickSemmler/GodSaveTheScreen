using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Tests;

public sealed class HelpFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-help-" + Guid.NewGuid().ToString("N"));

    public HelpFilesTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Resolve_BeideVorhanden_WaehltSpracheDerOberflaeche()
    {
        File.WriteAllText(Path.Combine(_directory, HelpFiles.German), "de");
        File.WriteAllText(Path.Combine(_directory, HelpFiles.English), "en");

        Path.GetFileName(HelpFiles.Resolve(_directory, CultureInfo.GetCultureInfo("de-DE"))).ShouldBe(HelpFiles.German);
        Path.GetFileName(HelpFiles.Resolve(_directory, CultureInfo.GetCultureInfo("en-US"))).ShouldBe(HelpFiles.English);
        Path.GetFileName(HelpFiles.Resolve(_directory, CultureInfo.GetCultureInfo("fr-FR"))).ShouldBe(HelpFiles.English);
    }

    [Fact]
    public void Resolve_SpracheFehlt_NimmtAndereFassung()
    {
        File.WriteAllText(Path.Combine(_directory, HelpFiles.English), "en");

        Path.GetFileName(HelpFiles.Resolve(_directory, CultureInfo.GetCultureInfo("de-DE"))).ShouldBe(HelpFiles.English);
    }

    [Fact]
    public void Resolve_KeineDatei_LiefertNull() =>
        HelpFiles.Resolve(_directory, CultureInfo.GetCultureInfo("de-DE")).ShouldBeNull();

    [Theory]
    [InlineData("tool-rectangle", true)]
    [InlineData("settings-general", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Tool", false)]
    [InlineData("a'b", false)]
    [InlineData("x\"><script>", false)]
    public void IsValidTopic_NurKleinbuchstabenZiffernBindestrich(string? topic, bool expected) =>
        HelpFiles.IsValidTopic(topic).ShouldBe(expected);

    [Fact]
    public void BuildRedirect_LeitetAufDateiMitAnkerWeiter()
    {
        var file = Path.Combine(_directory, "Ordner mit Leerzeichen", HelpFiles.German);

        var html = HelpFiles.BuildRedirect(file, "tool-text");

        var url = new Uri(file).AbsoluteUri + "#tool-text";
        url.ShouldContain("%20");
        html.ShouldContain($"content=\"0;url={url}\"");
        html.ShouldContain($"location.replace('{url}')");
    }

    [Fact]
    public void ForTool_PipetteZeigtAufFarben()
    {
        HelpTopics.ForTool(PipetteTool.ToolId).ShouldBe(HelpTopics.Colors);
        HelpTopics.ForTool(RectangleTool.ToolId).ShouldBe("tool-rectangle");
    }

    [Fact]
    public void ForSelection_GleicherTyp_AbschnittDesWerkzeugs()
    {
        var a = new EllipseAnnotation { Rect = new RectD(0, 0, 10, 10) };
        var b = new RectangleAnnotation { Rect = new RectD(0, 0, 10, 10) };

        HelpTopics.ForSelection([a, b]).ShouldBe("tool-rectangle");
        HelpTopics.ForSelection([new CalloutAnnotation()]).ShouldBe("tool-callout");
    }

    [Fact]
    public void ForSelection_GemischteTypen_KeinAbschnitt() =>
        HelpTopics.ForSelection([new CalloutAnnotation(), new RectangleAnnotation()]).ShouldBeNull();
}

/// <summary>Die Abschnitte, die F1/Umschalt+F1 anspringen, müssen in beiden Sprachfassungen der Hilfe existieren.</summary>
public sealed class HelpContentTests
{
    private static readonly Regex Ids = new(" id=\"([a-z0-9-]+)\"",RegexOptions.Compiled);
    private static readonly Regex Images = new("src=\"(images/[^\"]+)\"", RegexOptions.Compiled);

    public static TheoryData<string> Languages => ["de", "en"];

    private static string Template(string language) => Path.Combine(RepoPaths.Root(), "docs", "help", language, "index.html");

    private static HashSet<string> IdsOf(string language) =>
        [.. Ids.Matches(File.ReadAllText(Template(language))).Select(m => m.Groups[1].Value)];

    public static IEnumerable<string> RequiredTopics()
    {
        foreach (var field in typeof(HelpTopics).GetFields().Where(f => f.IsLiteral))
        {
            yield return (string)field.GetRawConstantValue()!;
        }

        var tools = ToolRegistry.Create(new EditorHarness().Context);
        foreach (var tool in tools.Tools)
        {
            yield return HelpTopics.ForTool(tool.Id);
        }
    }

    [StaFact]
    public void Hilfe_EnthaeltAlleSprungziele()
    {
        var required = RequiredTopics().ToList();
        foreach (var language in new[] { "de", "en" })
        {
            var ids = IdsOf(language);
            required.Where(t => !ids.Contains(t)).ShouldBeEmpty(language);
        }
    }

    [Fact]
    public void Hilfe_BeideSprachenHabenDieselbenAbschnitte() =>
        IdsOf("de").SetEquals(IdsOf("en")).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(Languages))]
    public void Hilfe_AlleBilderVorhanden(string language)
    {
        var directory = Path.GetDirectoryName(Template(language))!;
        Images.Matches(File.ReadAllText(Template(language)))
            .Select(m => m.Groups[1].Value)
            .Where(src => !File.Exists(Path.Combine(directory, src)))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Hilfe_LiegtGebuendeltNebenDerAnwendung()
    {
        foreach (var name in new[] { HelpFiles.German, HelpFiles.English })
        {
            var path = Path.Combine(AppContext.BaseDirectory, name);
            File.Exists(path).ShouldBeTrue(path);
            var html = File.ReadAllText(path);
            html.ShouldNotContain("src=\"images/");
            html.ShouldNotContain("{{VERSION}}");
            html.ShouldContain("data:image/png;base64,");
        }
    }
}
