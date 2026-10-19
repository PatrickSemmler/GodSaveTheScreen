using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.Core.Tests;

public sealed class RecentFilesServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-recent-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    public RecentFilesServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string RecentPath => Path.Combine(_directory, "recent.json");

    private RecentFilesService Create()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.RecentFilesPath.Returns(RecentPath);
        return new RecentFilesService(paths, NullLogger<RecentFilesService>.Instance, () => _now += TimeSpan.FromMinutes(1));
    }

    private string File(string name)
    {
        var path = Path.Combine(_directory, name);
        System.IO.File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Add_NeuesteDateiStehtOben()
    {
        var service = Create();
        service.Add(File("a.png"));
        service.Add(File("b.gsts"));

        service.Items.Select(i => i.Name).ShouldBe(["b.gsts", "a.png"]);
        service.Items[0].Kind.ShouldBe(RecentFileKind.Project);
        service.Items[1].Kind.ShouldBe(RecentFileKind.Image);
    }

    [Fact]
    public void Add_BereitsVorhandene_WandertNachObenOhneDuplikat()
    {
        var service = Create();
        var a = File("a.png");
        service.Add(a);
        service.Add(File("b.png"));
        service.Add(a.ToUpperInvariant());

        service.Items.Count.ShouldBe(2);
        service.Items[0].Name.ShouldBe("a.png", StringCompareShould.IgnoreCase);
    }

    [Fact]
    public void Add_BegrenztAuf15Eintraege()
    {
        var service = Create();
        for (var i = 0; i < 20; i++)
        {
            service.Add(File($"bild{i}.png"));
        }

        service.Items.Count.ShouldBe(15);
        service.Items[0].Name.ShouldBe("bild19.png");
        service.Items[^1].Name.ShouldBe("bild5.png");
    }

    [Fact]
    public void Liste_BleibtNachNeustartErhalten()
    {
        var first = Create();
        first.Add(File("a.png"));
        first.Add(File("b.gsts"));

        var second = Create();

        second.Items.Select(i => i.Name).ShouldBe(["b.gsts", "a.png"]);
    }

    [Fact]
    public void Remove_EntferntNurDenEintrag_UndMeldetAenderung()
    {
        var service = Create();
        var a = File("a.png");
        service.Add(a);
        service.Add(File("b.png"));
        var changes = 0;
        service.Changed += (_, _) => changes++;

        service.Remove(a);
        service.Remove(a);

        service.Items.Select(i => i.Name).ShouldBe(["b.png"]);
        changes.ShouldBe(1);
    }

    [Fact]
    public void Clear_LeertDieListe()
    {
        var service = Create();
        service.Add(File("a.png"));

        service.Clear();

        service.Items.ShouldBeEmpty();
        Create().Items.ShouldBeEmpty();
    }

    [Fact]
    public void FehlendeDatei_IstMarkiertAberImmerNochEntfernbar()
    {
        var service = Create();
        var path = File("weg.png");
        service.Add(path);
        service.Items[0].IsMissing.ShouldBeFalse();

        System.IO.File.Delete(path);

        service.Items[0].IsMissing.ShouldBeTrue();
        service.Remove(path);
        service.Items.ShouldBeEmpty();
    }

    [Fact]
    public void DefekteDatei_ErgibtLeereListe()
    {
        System.IO.File.WriteAllText(RecentPath, "{kein json");

        Create().Items.ShouldBeEmpty();
    }
}
