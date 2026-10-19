#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.IO;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-proj-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _service = new ProjectService(new DocumentExporter(), new ImageCodecService());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static ImageDocument SampleDocument()
    {
        var image = new PixelBuffer(120, 80);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                PixelTools.Set(image, x, y, (byte)(x * 2), (byte)(y * 3), (byte)(x + y));
            }
        }

        var document = new ImageDocument(image, 144, 120) { Metadata = new DocumentMetadata("Rectangle", "Titel") };
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(5, 6, 40, 30), Fill = ArgbColor.Parse("#80FF0000"), Name = "Kasten" });
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(50, 20, 30, 20), Strength = 9 });
        document.Annotations.Add(new MarkerAnnotation { Center = new PointD(90, 60), Number = 4 });
        document.CropRect = new PixelRect(10, 10, 100, 60);
        document.HighlightersBehind = false;
        return document;
    }

    [StaFact]
    public async Task SpeichernUndLaden_ErgibtIdentischesDokument()
    {
        var original = SampleDocument();
        var path = Path.Combine(_directory, "test.gsts");

        await _service.SaveAsync(original, path);
        var loaded = await _service.LoadAsync(path);

        var document = loaded.Document;
        loaded.Warnings.ShouldBeEmpty();
        document.BaseImage.Width.ShouldBe(120);
        document.BaseImage.Height.ShouldBe(80);
        document.BaseImage.Pixels.ShouldBe(original.BaseImage.Pixels);
        document.DpiX.ShouldBe(144);
        document.DpiY.ShouldBe(120);
        document.CropRect.ShouldBe(new PixelRect(10, 10, 100, 60));
        document.HighlightersBehind.ShouldBeFalse();
        document.Metadata.ShouldBe(new DocumentMetadata("Rectangle", "Titel"));
        document.CreatedAt.ShouldBe(original.CreatedAt);
        document.ProjectPath.ShouldBe(path);
        document.Annotations.Count.ShouldBe(3);
        document.Annotations.Select(a => a.Id).ShouldBe(original.Annotations.Select(a => a.Id));
        document.Annotations[0].ShouldBeOfType<RectangleAnnotation>().Name.ShouldBe("Kasten");
        document.Annotations[1].ShouldBeOfType<BlurAnnotation>().Strength.ShouldBe(9);
        document.Annotations[2].ShouldBeOfType<MarkerAnnotation>().Number.ShouldBe(4);
    }

    [StaFact]
    public async Task GeladenesDokument_HatLeerenUndoVerlaufUndIstNichtGeaendert()
    {
        var path = Path.Combine(_directory, "test.gsts");
        await _service.SaveAsync(SampleDocument(), path);

        var document = (await _service.LoadAsync(path)).Document;

        document.Undo.CanUndo.ShouldBeFalse();
        document.IsDirty.ShouldBeFalse();
    }

    [StaFact]
    public async Task GeladenesProjekt_RendertWieDasOriginal()
    {
        var original = SampleDocument();
        var path = Path.Combine(_directory, "test.gsts");
        await _service.SaveAsync(original, path);

        var loaded = (await _service.LoadAsync(path)).Document;

        var exporter = new DocumentExporter();
        var expected = exporter.Render(original);
        var actual = exporter.Render(loaded);
        actual.Width.ShouldBe(expected.Width);
        actual.Height.ShouldBe(expected.Height);
        actual.Pixels.ShouldBe(expected.Pixels);
    }

    [StaFact]
    public async Task Projektdatei_EnthaeltLesbaresBaseUndVorschauPng()
    {
        var path = Path.Combine(_directory, "test.gsts");
        await _service.SaveAsync(SampleDocument(), path);

        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        zip.Entries.Select(e => e.FullName).Order().ShouldBe(["base.png", "document.json", "manifest.json", "preview.png"]);

        var preview = await _service.ReadPreviewAsync(path);
        preview.ShouldNotBeNull();
        var size = ProjectSerializer.ReadPngSize(preview);
        size.ShouldNotBeNull();
        Math.Max(size.Value.Width, size.Value.Height).ShouldBeLessThanOrEqualTo(ProjectService.PreviewSize);
        // Die Vorschau zeigt das Ergebnis mit Zuschnitt (100 × 60), nicht das ganze Bild.
        size.Value.Width.ShouldBe(100);
        size.Value.Height.ShouldBe(60);
    }

    [StaFact]
    public async Task Vorschau_GrosseBilder_WerdenAuf512PixelVerkleinert()
    {
        var document = new ImageDocument(PixelTools.Solid(2000, 1000, 10, 20, 30));
        var path = Path.Combine(_directory, "gross.gsts");

        await _service.SaveAsync(document, path);

        var size = ProjectSerializer.ReadPngSize((await _service.ReadPreviewAsync(path))!);
        size.ShouldBe((512, 256));
    }

    [StaFact]
    public async Task Speichern_UeberschreibtAtomarUndHinterlaesstKeineTempDateien()
    {
        var path = Path.Combine(_directory, "test.gsts");
        await _service.SaveAsync(SampleDocument(), path);
        await _service.SaveAsync(SampleDocument(), path);

        Directory.GetFiles(_directory).Select(Path.GetFileName).ShouldBe(["test.gsts"]);
    }

    [StaFact]
    public async Task Laden_BildgroessePasstNichtZumDokument_WirdAbgelehnt()
    {
        var path = Path.Combine(_directory, "falsch.gsts");
        var content = new ProjectContent { Width = 50, Height = 50, BasePng = new ImageCodecService().Encode(new PixelBuffer(10, 10), 96, 96, new ImageSaveOptions(ImageFormatSetting.Png)) };
        await _service.WriteAsync(content, path);

        await Should.ThrowAsync<ProjectFormatException>(() => _service.LoadAsync(path));
    }

    [StaFact]
    public async Task Laden_KeineProjektdatei_WirdAlsFormatfehlerGemeldet()
    {
        var path = Path.Combine(_directory, "kaputt.gsts");
        await File.WriteAllTextAsync(path, "kein zip");

        await Should.ThrowAsync<ProjectFormatException>(() => _service.LoadAsync(path));
        (await _service.ReadPreviewAsync(path)).ShouldBeNull();
    }

    [StaFact]
    public async Task Pfade_MitSonderzeichen_FunktionierenWieInOneDrive()
    {
        // Leerzeichen, Umlaute, ß, CJK, #, [], & und Klammern – wie sie in OneDrive-Ordnern vorkommen.
        var folder = Path.Combine(_directory, "Ünïcode ü ß 日本 #1 [x] & (co)");
        var path = Path.Combine(folder, "Bericht – Q3 #2.gsts");

        await _service.SaveAsync(SampleDocument(), path);
        var loaded = await _service.LoadAsync(path);

        loaded.Document.Annotations.Count.ShouldBe(3);
        loaded.Document.ProjectPath.ShouldBe(path);
        (await _service.ReadPreviewAsync(path)).ShouldNotBeNull();
    }

    [StaFact]
    public async Task ReadPreview_FehlendeDatei_LiefertNull() =>
        (await _service.ReadPreviewAsync(Path.Combine(_directory, "gibtsnicht.gsts"))).ShouldBeNull();
}

public sealed class EditorProjectFlowTests
{
    [StaFact]
    public async Task Speichern_Unbenannt_BietetProjektAnUndSchreibtDasProjekt()
    {
        var h = new EditorHarness();
        h.AddRectangle(1, 1, 20, 20);
        h.Dialogs.PickDocumentSavePath(default!, default, default, default).ReturnsForAnyArgs((@"C:\temp\arbeit.gsts", (ImageFormatSetting?)null));

        (await h.ViewModel.TrySaveAsync(forceDialog: false)).ShouldBeTrue();

        h.Dialogs.Received(1).PickDocumentSavePath(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ImageFormatSetting>(), true);
        await h.Projects.Received(1).SaveAsync(h.Document, @"C:\temp\arbeit.gsts", Arg.Any<CancellationToken>());
        await h.Codec.DidNotReceiveWithAnyArgs().SaveAsync(default!, 0, 0, default!, default!);
        h.Document.ProjectPath.ShouldBe(@"C:\temp\arbeit.gsts");
        h.Document.IsDirty.ShouldBeFalse();
        h.Recents.Received(1).Add(@"C:\temp\arbeit.gsts");
        h.ViewModel.WindowTitle.ShouldContain("arbeit.gsts");
    }

    [StaFact]
    public async Task Speichern_OhneObjekte_SchlaegtBildformatVor()
    {
        var h = new EditorHarness();
        h.Dialogs.PickDocumentSavePath(default!, default, default, default).ReturnsForAnyArgs((ValueTuple<string, ImageFormatSetting?>?)null);

        await h.ViewModel.TrySaveAsync(forceDialog: false);

        h.Dialogs.Received(1).PickDocumentSavePath(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ImageFormatSetting>(), false);
    }

    [StaFact]
    public async Task Speichern_BekanntesProjekt_SchreibtOhneDialogWiederInsProjekt()
    {
        var h = new EditorHarness();
        h.Document.ProjectPath = @"C:\temp\arbeit.gsts";
        h.AddRectangle(1, 1, 20, 20);

        (await h.ViewModel.TrySaveAsync(forceDialog: false)).ShouldBeTrue();

        h.Dialogs.DidNotReceiveWithAnyArgs().PickDocumentSavePath(default!, default, default, default);
        await h.Projects.Received(1).SaveAsync(h.Document, @"C:\temp\arbeit.gsts", Arg.Any<CancellationToken>());
        h.Document.IsDirty.ShouldBeFalse();
    }

    [StaFact]
    public async Task Speichern_ProjektOhneEndung_BekommtGstsAngehaengt()
    {
        var h = new EditorHarness();

        await h.ViewModel.SaveProjectAsync(@"C:\temp\arbeit");

        h.Document.ProjectPath.ShouldBe(@"C:\temp\arbeit.gsts");
    }

    [StaFact]
    public async Task Export_EinesProjekts_LaesstDenProjektzustandUnveraendert()
    {
        var h = new EditorHarness();
        h.Document.ProjectPath = @"C:\temp\arbeit.gsts";
        h.AddRectangle(1, 1, 20, 20);
        h.Dialogs.PickImageSavePath(default!, default, default).ReturnsForAnyArgs((@"C:\temp\ergebnis.png", ImageFormatSetting.Png));

        h.ViewModel.ExportCommand.Execute(null);
        await Task.Delay(100);

        await h.Codec.Received(1).SaveAsync(Arg.Any<PixelBuffer>(), Arg.Any<double>(), Arg.Any<double>(), @"C:\temp\ergebnis.png", Arg.Any<ImageSaveOptions>());
        h.Document.SourcePath.ShouldBe(@"C:\temp\ergebnis.png");
        h.Document.ProjectPath.ShouldBe(@"C:\temp\arbeit.gsts");
        h.Document.IsDirty.ShouldBeTrue();
        await h.Projects.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default);
    }

    [StaFact]
    public async Task Speichern_MitBekanntemBildUndProjekt_SchreibtInsProjektNichtInsBild()
    {
        var h = new EditorHarness();
        h.Document.SourcePath = @"C:\temp\ergebnis.png";
        h.Document.ProjectPath = @"C:\temp\arbeit.gsts";
        h.AddRectangle(1, 1, 20, 20);

        await h.ViewModel.TrySaveAsync(forceDialog: false);

        await h.Projects.Received(1).SaveAsync(h.Document, @"C:\temp\arbeit.gsts", Arg.Any<CancellationToken>());
        await h.Codec.DidNotReceiveWithAnyArgs().SaveAsync(default!, 0, 0, default!, default!);
    }

    [StaFact]
    public async Task Schliessen_MitObjekten_BietetProjektAn_UndSpeichertAlsProjekt()
    {
        var h = new EditorHarness();
        h.AddRectangle(1, 1, 20, 20);
        h.Dialogs.AskSaveChanges(default!, default!).ReturnsForAnyArgs(SaveChangesAnswer.SaveAsProject);
        h.Dialogs.PickDocumentSavePath(default!, default, default, default).ReturnsForAnyArgs((@"C:\temp\arbeit.gsts", (ImageFormatSetting?)null));

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeTrue();

        h.Dialogs.Received(1).AskSaveChanges(Arg.Any<string>(), Arg.Any<string>(), true);
        await h.Projects.Received(1).SaveAsync(h.Document, @"C:\temp\arbeit.gsts", Arg.Any<CancellationToken>());
    }

    [StaFact]
    public async Task Schliessen_BereitsProjekt_BietetKeinProjektAn()
    {
        var h = new EditorHarness();
        h.Document.ProjectPath = @"C:\temp\arbeit.gsts";
        h.AddRectangle(1, 1, 20, 20);
        h.Dialogs.AskSaveChanges(default!, default!).ReturnsForAnyArgs(SaveChangesAnswer.Discard);

        (await h.ViewModel.ConfirmCloseAsync()).ShouldBeTrue();

        h.Dialogs.Received(1).AskSaveChanges(Arg.Any<string>(), Arg.Any<string>(), false);
    }

    [StaFact]
    public async Task Speichern_MitUnschaerfe_ZeigtHinweisAufLesbaresOriginal()
    {
        var h = new EditorHarness();
        h.Document.Undo.Execute(new AddAnnotationAction(h.Document.Annotations, new BlurAnnotation { Rect = new RectD(5, 5, 30, 30) }));

        await h.ViewModel.SaveProjectAsync(@"C:\temp\arbeit.gsts");

        h.ViewModel.InfoMessage.ShouldBe(Strings.Project_BlurHint);
    }

    [StaFact]
    public async Task Speichern_OhneUnschaerfe_ZeigtKeinenHinweis()
    {
        var h = new EditorHarness();
        h.AddRectangle(1, 1, 20, 20);

        await h.ViewModel.SaveProjectAsync(@"C:\temp\arbeit.gsts");

        h.ViewModel.InfoMessage.ShouldBeNull();
    }

    [StaFact]
    public async Task Speichern_ProjektFehler_ZeigtMeldungUndBleibtDirty()
    {
        var h = new EditorHarness();
        h.AddRectangle(1, 1, 20, 20);
        h.Projects.SaveAsync(default!, default!, default).ReturnsForAnyArgs(Task.FromException(new IOException("voll")));

        (await h.ViewModel.SaveProjectAsync(@"C:\temp\arbeit.gsts")).ShouldBeFalse();

        h.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("voll")));
        h.Document.IsDirty.ShouldBeTrue();
        h.Document.ProjectPath.ShouldBeNull();
    }

    [StaFact]
    public void ZuletztVerwendet_OeffnenUeberDenEditorService()
    {
        var h = new EditorHarness();
        var file = new RecentFile(@"C:\temp\a.gsts", RecentFileKind.Project, DateTimeOffset.Now);

        h.ViewModel.OpenRecentCommand.Execute(file);

        h.Editors.Received(1).OpenFilesAsync(Arg.Is<IEnumerable<string>>(p => p.Single() == @"C:\temp\a.gsts"));
    }
}

public sealed class AutoBackupServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gsts-backup-" + Guid.NewGuid().ToString("N"));
    private readonly IEditorService _editors = Substitute.For<IEditorService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ProjectService _projects = new(new DocumentExporter(), new ImageCodecService());
    private readonly List<ImageDocument> _documents = [];
    private readonly AutoBackupService _service;

    public AutoBackupServiceTests()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.AutosaveDirectory.Returns(_directory);
        _editors.OpenDocuments.Returns(_ => _documents.ToList());
        _service = new AutoBackupService(_editors, _projects, paths, _dialogs, NullLogger<AutoBackupService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ImageDocument Dirty()
    {
        var document = new ImageDocument(PixelTools.Solid(40, 30, 200, 100, 50)) { IsDirty = true };
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(2, 2, 10, 10) });
        _documents.Add(document);
        return document;
    }

    [StaFact]
    public async Task UngespeichertesDokument_WirdAlsGstsGesichert()
    {
        var document = Dirty();

        await _service.BackupAllAsync();

        var path = Path.Combine(_directory, $"{document.DocumentId}.gsts");
        File.Exists(path).ShouldBeTrue();
        var loaded = await _projects.LoadAsync(path);
        loaded.Document.Annotations.Count.ShouldBe(1);
        loaded.Document.BaseImage.Pixels.ShouldBe(document.BaseImage.Pixels);
    }

    [StaFact]
    public async Task UnveraendertesDokument_WirdNichtSofortErneutGeschrieben()
    {
        var document = Dirty();
        await _service.BackupAllAsync();
        var path = Path.Combine(_directory, $"{document.DocumentId}.gsts");
        var first = File.GetLastWriteTimeUtc(path);
        await Task.Delay(50);

        await _service.BackupAllAsync();

        File.GetLastWriteTimeUtc(path).ShouldBe(first);
    }

    [StaFact]
    public async Task GeaendertesDokument_WirdErneutGesichert()
    {
        var document = Dirty();
        await _service.BackupAllAsync();
        document.Undo.Execute(new AddAnnotationAction(document.Annotations, new EllipseAnnotation { Rect = new RectD(1, 1, 5, 5) }));

        await _service.BackupAllAsync();

        var loaded = await _projects.LoadAsync(Path.Combine(_directory, $"{document.DocumentId}.gsts"));
        loaded.Document.Annotations.Count.ShouldBe(2);
    }

    [StaFact]
    public async Task GespeichertesDokument_LoeschtDieSicherung()
    {
        var document = Dirty();
        await _service.BackupAllAsync();
        var path = Path.Combine(_directory, $"{document.DocumentId}.gsts");
        File.Exists(path).ShouldBeTrue();

        document.MarkSaved();
        await _service.BackupAllAsync();

        File.Exists(path).ShouldBeFalse();
    }

    [StaFact]
    public async Task Wiederherstellen_OeffnetDokumenteUndLoeschtDieSicherung()
    {
        var document = Dirty();
        await _service.BackupAllAsync();
        _documents.Clear();
        _dialogs.AskRecovery(default!, default!).ReturnsForAnyArgs(true);
        ImageDocument? opened = null;
        _editors.Open(Arg.Do<ImageDocument>(d => opened = d), Arg.Any<string?>()).Returns((GodSaveTheScreen.App.Views.EditorWindow)null!);

        var count = await _service.RecoverAsync();

        count.ShouldBe(1);
        opened.ShouldNotBeNull();
        opened.IsDirty.ShouldBeTrue();
        opened.ProjectPath.ShouldBeNull();
        opened.Annotations.Count.ShouldBe(1);
        Directory.GetFiles(_directory).ShouldBeEmpty();
        _ = document;
    }

    [StaFact]
    public async Task Verwerfen_LoeschtDieSicherungenOhneZuOeffnen()
    {
        Dirty();
        await _service.BackupAllAsync();
        _dialogs.AskRecovery(default!, default!).ReturnsForAnyArgs(false);

        var count = await _service.RecoverAsync();

        count.ShouldBe(0);
        Directory.GetFiles(_directory).ShouldBeEmpty();
        _editors.DidNotReceiveWithAnyArgs().Open(default!, default);
    }

    [StaFact]
    public async Task Wiederherstellen_OhneSicherungen_FragtNicht()
    {
        (await _service.RecoverAsync()).ShouldBe(0);

        _dialogs.DidNotReceiveWithAnyArgs().AskRecovery(default!, default!);
    }

    [StaFact]
    public async Task Wiederherstellen_DefekteSicherung_WirdUebersprungenUndGeloescht()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "kaputt.gsts"), "kein zip");
        _dialogs.AskRecovery(default!, default!).ReturnsForAnyArgs(true);

        (await _service.RecoverAsync()).ShouldBe(0);

        Directory.GetFiles(_directory).ShouldBeEmpty();
    }
}

public sealed class JumpListTests
{
    [StaFact]
    public void Sprungliste_EnthaeltAufnahmeaufgabenUndVorhandeneDateien()
    {
        var existing = Path.GetTempFileName();
        try
        {
            var recents = new[]
            {
                new RecentFile(existing, RecentFileKind.Image, DateTimeOffset.Now),
                new RecentFile(@"C:\gibt\es\nicht.gsts", RecentFileKind.Project, DateTimeOffset.Now),
            };

            var list = JumpListService.Create(@"C:\app\GodSaveTheScreen.exe", recents);

            var tasks = list.JumpItems.OfType<System.Windows.Shell.JumpTask>().ToList();
            tasks.Count(t => t.Arguments.StartsWith("--capture", StringComparison.Ordinal)).ShouldBe(4);
            tasks.Select(t => t.Arguments).ShouldContain("--capture rect");
            var recent = tasks.Where(t => t.CustomCategory == Strings.Recent_Title).ToList();
            recent.Count.ShouldBe(1);
            recent[0].Arguments.ShouldBe($"\"{existing}\"");
        }
        finally
        {
            File.Delete(existing);
        }
    }
}

public sealed class ProjectCommandLineTests
{
    [Fact]
    public void Kommandozeile_GstsDatei_WirdAlsDateiErkannt()
    {
        var options = GodSaveTheScreen.App.Hosting.CommandLineParser.Parse([@"C:\temp\arbeit.gsts"]);

        options.Files.ShouldBe([@"C:\temp\arbeit.gsts"]);
        options.Errors.ShouldBeEmpty();
    }
}
