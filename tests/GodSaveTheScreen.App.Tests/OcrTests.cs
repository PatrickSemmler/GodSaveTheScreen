#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Ocr;
using GodSaveTheScreen.Rendering;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Ein Wort einer erkannten Zeile als einfaches Beispiel.</summary>
internal static class OcrSamples
{
    public static OcrLine Line(string text, double x, double y, double width = 100, double height = 14)
    {
        var words = new List<OcrWord>();
        var cursor = x;
        foreach (var part in text.Split(' '))
        {
            var w = Math.Max(10, part.Length * 7.0);
            words.Add(new OcrWord(part, new RectD(cursor, y, w, height)));
            cursor += w + 6;
        }

        return new OcrLine(text, new RectD(x, y, Math.Max(width, cursor - x), height), words);
    }

    public static IOcrService Service(OcrResult? result = null, params string[] languages)
    {
        var service = Substitute.For<IOcrService>();
        service.AvailableLanguages.Returns([.. languages.Select(l => new OcrLanguage(l, l))]);
        service.RecognizeAsync(Arg.Any<PixelBuffer>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(result ?? new OcrResult("de-DE", [Line("Hallo Welt", 5, 5)])));
        return service;
    }
}

public sealed class OcrCaptureTests
{
    private static async Task<PixelBuffer?> ShownAfterCapture(TestCoordinator t)
    {
        PixelBuffer? shown = null;
        t.Ocr.When(o => o.Show(Arg.Any<PixelBuffer>())).Do(call => shown = call.Arg<PixelBuffer>());
        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));
        return shown;
    }

    private static void Select(TestCoordinator t, SelectionResult? result) =>
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

    [Fact]
    public async Task Text_ZeigtDasErgebnisfensterMitDemAusgewaehltenBereich()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        Select(t, new SelectionResult(new PixelRect(10, 10, 30, 20)));

        var shown = await ShownAfterCapture(t);

        shown.ShouldNotBeNull();
        shown.Width.ShouldBe(30);
        shown.Height.ShouldBe(20);
    }

    [Fact]
    public async Task Text_WaehltImmerEinRechteck_MitEigenemHinweis()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        Select(t, null);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));

        await t.Selection.Received(1).SelectAsync(Arg.Any<ScreenSnapshot>(), CaptureMode.Rectangle, GodSaveTheScreen.App.Resources.Strings.Overlay_HintText, Arg.Any<CancellationToken>());
        await t.Selection.DidNotReceive().SelectAsync(Arg.Any<ScreenSnapshot>(), CaptureMode.Text, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Text_NimmtOhneMauszeigerAuf_UnabhaengigVonDerEinstellung(bool captureCursor)
    {
        var t = TestCoordinator.Create(new FakeSettings { Current = new AppSettings { CaptureCursor = captureCursor } });
        Select(t, null);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));

        t.Capture.Received(1).CaptureVirtualScreen(false);
    }

    [Fact]
    public async Task Rechteck_NimmtMitMauszeigerAufWennEingestellt()
    {
        var t = TestCoordinator.Create(new FakeSettings { Current = new AppSettings { CaptureCursor = true } });
        t.Selection.SelectAsync(Arg.Any<ScreenSnapshot>(), Arg.Any<CaptureMode>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<SelectionResult?>(null));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Rectangle));

        t.Capture.Received(1).CaptureVirtualScreen(true);
    }

    [Fact]
    public async Task Text_UeberspringtDieNachaufnahmeKette()
    {
        var action = Substitute.For<IPostCaptureAction>();
        var t = TestCoordinator.Create(new FakeSettings(), action);
        Select(t, new SelectionResult(new PixelRect(0, 0, 20, 20)));

        var shown = await ShownAfterCapture(t);

        shown.ShouldNotBeNull();
        await action.DidNotReceive().ExecuteAsync(Arg.Any<CaptureResultContext>());
    }

    [Fact]
    public async Task Text_AbbruchInDerAuswahl_ZeigtKeinFenster()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        Select(t, null);

        var shown = await ShownAfterCapture(t);

        shown.ShouldBeNull();
        t.Ocr.DidNotReceive().Show(Arg.Any<PixelBuffer>());
        t.Coordinator.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public async Task Text_FensterWirdErstNachDemEinblendenGeoeffnet()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        Select(t, new SelectionResult(new PixelRect(0, 0, 20, 20)));
        var hiddenToken = Substitute.For<IDisposable>();
        t.Visibility.HideAll().Returns(hiddenToken);

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));

        Received.InOrder(() =>
        {
            hiddenToken.Dispose();
            t.Ocr.Show(Arg.Any<PixelBuffer>());
        });
    }

    [Fact]
    public async Task Text_FehlerImErgebnisfenster_WirdAlsFehlerAngezeigt()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        Select(t, new SelectionResult(new PixelRect(0, 0, 20, 20)));
        t.Ocr.When(o => o.Show(Arg.Any<PixelBuffer>())).Do(_ => throw new InvalidOperationException("kaputt"));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));

        t.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("kaputt", StringComparison.Ordinal)));
        t.Coordinator.IsCapturing.ShouldBeFalse();
    }

    [Fact]
    public async Task Text_FehlerBeimAufnehmen_LandetInDerFehlermeldung()
    {
        var t = TestCoordinator.Create(new FakeSettings());
        t.Capture.CaptureVirtualScreen(Arg.Any<bool>()).Throws(new InvalidOperationException("kein Bild"));

        await t.Coordinator.StartAsync(new CaptureRequest(CaptureMode.Text));

        t.Dialogs.Received(1).ShowError(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("kein Bild", StringComparison.Ordinal)));
        t.Ocr.DidNotReceive().Show(Arg.Any<PixelBuffer>());
    }

    [Fact]
    public void AufnahmeModus_Text_HatEigenenHotkeyUndStehtAmEnde()
    {
        ((int)CaptureMode.Text).ShouldBe(4);
        var settings = new AppSettings();
        settings.GetHotkey(CaptureMode.Text).ShouldBe("Ctrl+Shift+D5");
        settings.WithHotkey(CaptureMode.Text, "Ctrl+Alt+T").HotkeyText.ShouldBe("Ctrl+Alt+T");
        settings.WithHotkey(CaptureMode.Text, "Ctrl+Alt+T").HotkeyFullScreen.ShouldBe(settings.HotkeyFullScreen);
    }

    [Fact]
    public void Kommandozeile_VersteheCaptureText()
    {
        CommandLineParser.Parse(["--capture", "text"]).Capture.ShouldBe(CaptureMode.Text);
        CommandLineParser.Parse(["--capture", "TEXT"]).Errors.ShouldBeEmpty();
    }
}

public sealed class OcrResultViewModelTests
{
    private static OcrResultViewModel Create(IOcrService ocr, out FakeSettings settings, out IClipboardService clipboard, Action<PixelBuffer>? open = null, string language = "")
    {
        settings = new FakeSettings { Current = new AppSettings { OcrLanguage = language } };
        clipboard = Substitute.For<IClipboardService>();
        return new OcrResultViewModel(new PixelBuffer(40, 30), ocr, clipboard, settings, open);
    }

    [Fact]
    public async Task Erkennen_ZeigtDenTextZeilenweise()
    {
        var ocr = OcrSamples.Service(new OcrResult("de-DE", [OcrSamples.Line("Erste Zeile", 5, 5), OcrSamples.Line("Zweite Zeile", 5, 25)]), "de-DE");
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        vm.Text.ShouldBe("Erste Zeile\nZweite Zeile");
        vm.IsBusy.ShouldBeFalse();
        vm.IsEmptyResult.ShouldBeFalse();
        vm.IsNoLanguage.ShouldBeFalse();
        vm.Statistics.ShouldContain("21");
    }

    [Fact]
    public async Task Kopieren_NutztDenBearbeitetenTextMitWindowsZeilenenden()
    {
        var vm = Create(OcrSamples.Service(null, "de-DE"), out _, out var clipboard);
        await vm.RecognizeAsync();
        vm.Text = "Korrigiert\nZwei";

        vm.CopyCommand.Execute(null);

        clipboard.Received(1).SetText("Korrigiert\r\nZwei");
        vm.Notice.ShouldNotBeNull();
    }

    [Fact]
    public void Kopieren_OhneText_TutNichts()
    {
        var vm = Create(OcrSamples.Service(null, "de-DE"), out _, out var clipboard);

        vm.CopyCommand.Execute(null);

        clipboard.DidNotReceive().SetText(Arg.Any<string>());
    }

    [Fact]
    public async Task Sprachwechsel_StartetNeuUndWirdGespeichert()
    {
        var ocr = OcrSamples.Service(null, "de-DE", "en-US");
        var vm = Create(ocr, out var settings, out _);
        await vm.RecognizeAsync();
        ocr.ClearReceivedCalls();

        vm.LanguageTag = "en-US";
        await WaitFor(() => ocr.ReceivedCalls().Any());

        await ocr.Received().RecognizeAsync(Arg.Any<PixelBuffer>(), "en-US", Arg.Any<CancellationToken>());
        settings.Current.OcrLanguage.ShouldBe("en-US");
        settings.SaveCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Automatik_UebergibtKeineSprache()
    {
        var ocr = OcrSamples.Service(null, "de-DE");
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        await ocr.Received().RecognizeAsync(Arg.Any<PixelBuffer>(), null, Arg.Any<CancellationToken>());
        vm.Languages[0].Value.ShouldBe(string.Empty);
        vm.Languages.Select(l => l.Value).ShouldBe(["", "de-DE"]);
    }

    [Fact]
    public async Task NichtMehrInstallierteGespeicherteSprache_FaelltAufAutomatikZurueck()
    {
        var ocr = OcrSamples.Service(null, "de-DE");
        var vm = Create(ocr, out _, out _, language: "fr-FR");

        vm.LanguageTag.ShouldBe(string.Empty);
        await vm.RecognizeAsync();

        await ocr.Received().RecognizeAsync(Arg.Any<PixelBuffer>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeeresErgebnis_ZeigtEigeneMeldung()
    {
        var ocr = OcrSamples.Service(new OcrResult("de-DE", []), "de-DE");
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        vm.IsEmptyResult.ShouldBeTrue();
        vm.Text.ShouldBeEmpty();
        vm.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task KeineSprache_ZeigtDieHinweisMeldung_OhneAbsturz()
    {
        var ocr = OcrSamples.Service(null);
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        vm.IsNoLanguage.ShouldBeTrue();
        vm.IsEmptyResult.ShouldBeFalse();
        vm.IsBusy.ShouldBeFalse();
        await ocr.DidNotReceive().RecognizeAsync(Arg.Any<PixelBuffer>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpracheWirdWaehrendDesLaufsDeinstalliert_ZeigtDieHinweisMeldung()
    {
        var ocr = OcrSamples.Service(null, "de-DE");
        ocr.RecognizeAsync(Arg.Any<PixelBuffer>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Throws(new OcrUnavailableException());
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        vm.IsNoLanguage.ShouldBeTrue();
    }

    [Fact]
    public async Task FehlerDerErkennung_WirdAngezeigt()
    {
        var ocr = OcrSamples.Service(null, "de-DE");
        ocr.RecognizeAsync(Arg.Any<PixelBuffer>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("kaputt"));
        var vm = Create(ocr, out _, out _);

        await vm.RecognizeAsync();

        vm.HasError.ShouldBeTrue();
        vm.ErrorMessage!.ShouldContain("kaputt");
        vm.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Abbrechen_BeendetDenLaufOhneFehler()
    {
        var gate = new TaskCompletionSource<OcrResult>();
        var ocr = OcrSamples.Service(null, "de-DE");
        ocr.RecognizeAsync(Arg.Any<PixelBuffer>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var token = call.Arg<CancellationToken>();
            token.Register(() => gate.TrySetCanceled(token));
            return gate.Task;
        });
        var vm = Create(ocr, out _, out _);

        var run = vm.RecognizeAsync();
        vm.IsBusy.ShouldBeTrue();
        vm.CancelCommand.CanExecute(null).ShouldBeTrue();
        vm.CancelCommand.Execute(null);
        await run;

        vm.IsBusy.ShouldBeFalse();
        vm.HasError.ShouldBeFalse();
        vm.Notice.ShouldNotBeNull();
        vm.CancelCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task SpaltenMitTabulator_BautDenTextNeuAusDemLetztenErgebnis()
    {
        var line = new OcrLine(
            "A B",
            new RectD(0, 0, 400, 14),
            [new OcrWord("Name", new RectD(0, 0, 40, 14)), new OcrWord("Größe", new RectD(300, 0, 40, 14))]);
        var ocr = OcrSamples.Service(new OcrResult("de-DE", [line]), "de-DE");
        var vm = Create(ocr, out _, out _);
        await vm.RecognizeAsync();
        vm.Text.ShouldBe("A B");
        ocr.ClearReceivedCalls();

        vm.SeparateColumns = true;

        vm.Text.ShouldBe("Name\tGröße");
        ocr.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ImEditorOeffnen_GibtDasAufgenommeneBildWeiter()
    {
        PixelBuffer? opened = null;
        var vm = Create(OcrSamples.Service(null, "de-DE"), out _, out _, open: image => opened = image);
        await vm.RecognizeAsync();

        vm.OpenInEditorCommand.Execute(null);

        opened.ShouldBeSameAs(vm.Image);
    }

    [StaFact]
    public void Vorschau_IstEingefroren()
    {
        var vm = Create(OcrSamples.Service(null, "de-DE"), out _, out _);

        vm.Preview.IsFrozen.ShouldBeTrue();
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }
    }
}

public sealed class OcrEditorSourceTests
{
    private static ImageDocument Document() => new(PixelTools.Solid(100, 80, 255, 255, 255));

    private static void Paint(ImageDocument document, int x, int y, int width, int height)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                PixelTools.Set(document.BaseImage, column, row, 0, 0, 0);
            }
        }
    }

    [StaFact]
    public void OhneAuswahl_IstDasGanzeSichtbareBild()
    {
        var document = Document();
        Paint(document, 10, 10, 20, 10);

        var source = OcrSource.Prepare(document, new DocumentExporter());

        source.Width.ShouldBe(100);
        source.Height.ShouldBe(80);
        PixelTools.Get(source, 15, 15).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void MitAuswahl_WirdAusgeschnittenUndAusserhalbDerDeckungWeissGefuellt()
    {
        var document = Document();
        Paint(document, 10, 10, 40, 40);
        var selection = new AreaSelection(100, 80);
        var (box, coverage) = AreaSelection.Ellipse(new RectD(10, 10, 40, 40), document.BaseImage.Bounds);
        selection.Combine(box, coverage, SelectionCombine.Replace);

        var source = OcrSource.Prepare(document, new DocumentExporter(), selection);

        source.Width.ShouldBe(40);
        source.Height.ShouldBe(40);
        PixelTools.Get(source, 20, 20).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255)); // Mitte der Ellipse: schwarz
        PixelTools.Get(source, 0, 0).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255)); // Ecke außerhalb: weiß
        PixelTools.Get(source, 39, 39).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void LeereAuswahl_BedeutetDasGanzeBild()
    {
        var document = Document();

        OcrSource.Prepare(document, new DocumentExporter(), new AreaSelection(100, 80)).Width.ShouldBe(100);
    }

    [StaFact]
    public void Unschaerfe_VerdecktDenText()
    {
        var document = Document();
        Paint(document, 10, 10, 30, 20);
        document.Annotations.Add(new BlurAnnotation { Rect = new RectD(5, 5, 50, 30), Mode = BlurMode.Redact, RedactColor = ArgbColor.White });

        var source = OcrSource.Prepare(document, new DocumentExporter());

        PixelTools.Get(source, 20, 20).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Textobjekte_BleibenImBild()
    {
        var document = Document();
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(10, 10, 30, 20), Fill = ArgbColor.Black, Stroke = new StrokeStyle(ArgbColor.Black, 0) });

        var source = OcrSource.Prepare(document, new DocumentExporter());

        PixelTools.Get(source, 20, 20).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
    }

    [StaFact]
    public void ErweiterteFlaeche_TransparentWirdWeiss()
    {
        var document = Document();
        document.Annotations.Add(new RectangleAnnotation { Rect = new RectD(90, 60, 60, 60), Fill = ArgbColor.Black, Stroke = new StrokeStyle(ArgbColor.Black, 0) });
        document.UpdateCanvasBounds();

        var source = OcrSource.Prepare(document, new DocumentExporter());

        source.Width.ShouldBeGreaterThan(100);
        PixelTools.Get(source, source.Width - 1, 0).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Editor_BefehlTextErkennen_ZeigtDasErgebnisfenster()
    {
        var presenter = Substitute.For<IOcrResultPresenter>();
        var harness = new EditorHarness(new ImageDocument(PixelTools.Solid(60, 40, 255, 255, 255)), presenter);

        harness.ViewModel.RecognizeTextCommand.Execute(null);

        presenter.Received(1).Show(Arg.Is<PixelBuffer>(p => p.Width == 60 && p.Height == 40));
    }
}

public sealed class OcrRealRecognitionTests
{
    private static OcrLanguage? Installed() => new WindowsOcrService().AvailableLanguages.FirstOrDefault(l => l.Tag.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        ?? new WindowsOcrService().AvailableLanguages.FirstOrDefault(l => l.Tag.StartsWith("en", StringComparison.OrdinalIgnoreCase));

    private static PixelBuffer Render(string[] lines, double size, bool dark = false, string font = "Segoe UI", double dpi = 96)
    {
        var visual = new DrawingVisual();
        var width = 900;
        var height = (int)Math.Ceiling((lines.Length * size * 1.7) + 24);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(dark ? Brushes.Black : Brushes.White, null, new Rect(0, 0, width, height));
            for (var i = 0; i < lines.Length; i++)
            {
                var text = new FormattedText(lines[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(font), size, dark ? Brushes.White : Brushes.Black, 1.0);
                dc.DrawText(text, new Point(12, 12 + (i * size * 1.7)));
            }
        }

        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var buffer = new PixelBuffer(width, height);
        bitmap.CopyPixels(buffer.Pixels, buffer.Stride, 0);
        return buffer;
    }

    private static string Normalize(string text) => new([.. text.Where(char.IsLetterOrDigit)]);

    [StaFact]
    public async Task Erkennt_NormaleSchrift_AbDreizehnPixeln()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        string[] lines = ["Das ist ein einfacher Beispieltext", "Zweite Zeile mit Zahlen 1234567890", "Dritte Zeile ohne Besonderheiten"];
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(Render(lines, 16), language!.Tag);

        var recognized = Normalize(OcrTextBuilder.Build(result.Lines));
        var expected = Normalize(string.Concat(lines));
        var correct = expected.Count(c => recognized.Contains(c, StringComparison.Ordinal));
        result.Lines.Count.ShouldBeGreaterThanOrEqualTo(3);
        (correct / (double)expected.Length).ShouldBeGreaterThan(0.98);
        recognized.ShouldContain("Beispieltext");
    }

    /// <summary>Mustersammlung aus selbst gerenderter Schrift (Fenster, Datei- und Menütexte): Genauigkeit je Schriftgröße, hell und dunkel.</summary>
    [StaTheory]
    [InlineData("Segoe UI", false, 13, 0.98)]
    [InlineData("Segoe UI", true, 13, 0.98)]
    [InlineData("Segoe UI", false, 16, 0.98)]
    [InlineData("Consolas", false, 13, 0.98)]
    [InlineData("Consolas", true, 16, 0.98)]
    [InlineData("Arial", false, 14, 0.97)]
    [InlineData("Arial", true, 16, 0.98)]
    [InlineData("Segoe UI", false, 12, 0.95)]
    [InlineData("Consolas", false, 12, 0.95)]
    [InlineData("Arial", false, 11, 0.93)]
    [InlineData("Segoe UI", true, 10, 0.93)]
    [InlineData("Arial", false, 10, 0.93)]
    [InlineData("Segoe UI", false, 9, 0.9)]
    [InlineData("Arial", true, 9, 0.9)]
    public async Task Genauigkeit_JeSchriftgroesse(string font, bool dark, double size, double minimum)
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        Assert.SkipUnless(language!.Tag.StartsWith("de", StringComparison.OrdinalIgnoreCase), "Die Mustertexte sind deutsch.");
        string[] lines = ["Der Explorer zeigt Dateien und Ordner an", "Einstellungen Netzwerk und Internet Bluetooth", "Datei Bearbeiten Ansicht Hilfe 2026 Version 1234"];
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(Render(lines, size, dark, font), language.Tag);

        Similarity(string.Concat(lines), OcrTextBuilder.Build(result.Lines)).ShouldBeGreaterThanOrEqualTo(minimum);
    }

    /// <summary>1 − Levenshtein-Abstand / Länge der Vorlage, nur Buchstaben und Ziffern (O und 0 zählen als Verwechslung).</summary>
    private static double Similarity(string expected, string actual)
    {
        var a = Normalize(expected);
        var b = Normalize(actual);
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }
        }

        return Math.Max(0, 1 - ((double)d[a.Length, b.Length] / Math.Max(1, a.Length)));
    }

    [StaFact]
    public async Task Erkennt_HellenTextAufDunklemGrund()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(Render(["Dunkler Hintergrund mit hellem Text"], 18, dark: true), language!.Tag);

        Normalize(OcrTextBuilder.Build(result.Lines)).ShouldContain("Hintergrund");
    }

    [StaFact]
    public async Task Wortrechtecke_LiegenImBild_UndInDerRichtigenReihenfolge()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        var image = Render(["Erste Zeile hier", "Zweite Zeile dort"], 18);
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(image, language!.Tag);

        result.Lines.Count.ShouldBe(2);
        foreach (var word in result.Lines.SelectMany(l => l.Words))
        {
            word.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
            word.Bounds.Right.ShouldBeLessThanOrEqualTo(image.Width);
            word.Bounds.Bottom.ShouldBeLessThanOrEqualTo(image.Height);
        }

        result.Lines[0].Bounds.Y.ShouldBeLessThan(result.Lines[1].Bounds.Y);
    }

    [StaFact]
    public async Task KleineSchrift_WirdVergroessertNachErkannt()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        string[] lines = ["Kleine Schrift braucht Hilfe", "Zweite kleine Zeile mit Text"];
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(Render(lines, 11), language!.Tag);

        var recognized = Normalize(OcrTextBuilder.Build(result.Lines));
        var expected = Normalize(string.Concat(lines));
        var correct = expected.Count(c => recognized.Contains(c, StringComparison.Ordinal));
        (correct / (double)expected.Length).ShouldBeGreaterThan(0.85);
    }

    [StaFact]
    public async Task SehrLangesBild_WirdInStreifenVollstaendigUndOhneDoppelteZeilenErkannt()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        var lines = Enumerable.Range(1, 400).Select(i => $"Zeile Nummer {i:000} mit etwas Text").ToArray();
        var image = Render(lines, 16);
        image.Height.ShouldBeGreaterThan(OcrTiling.MaxSide);
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(image, language!.Tag);

        var texts = result.Lines.Select(l => l.Text).ToList();
        var numbers = texts.Select(t => System.Text.RegularExpressions.Regex.Match(t, @"\d{3}")).Where(m => m.Success).Select(m => m.Value).ToList();
        numbers.Count.ShouldBeGreaterThan(360);
        numbers.Distinct().Count().ShouldBe(numbers.Count);
        numbers.ShouldContain("001");
        numbers.ShouldContain("400");
        result.Lines.Select(l => l.Bounds.Y).ShouldBeInOrder();
    }

    [Fact]
    public void OcrProjekt_VerweistAufKeineNetzwerkAssembly()
    {
        var references = typeof(WindowsOcrService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

        references.ShouldNotContain(name => name.StartsWith("System.Net", StringComparison.Ordinal));
        references.ShouldNotContain(name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
        typeof(OcrUnavailableException).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)
            .ShouldNotContain(name => name.StartsWith("System.Net", StringComparison.Ordinal));
    }

    [StaFact]
    public async Task OhneInhalt_LiefertEinLeeresErgebnis()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(PixelTools.Solid(300, 100, 255, 255, 255), language!.Tag);

        result.IsEmpty.ShouldBeTrue();
    }

    [StaFact]
    public async Task Abbruch_WirdAlsOperationCanceledGemeldet()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        using var service = new WindowsOcrService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.RecognizeAsync(Render(["Text"], 16), language!.Tag, cancellation.Token));
    }

    [StaFact]
    public async Task UnbekannteSprache_FaelltAufAutomatikZurueck()
    {
        var language = Installed();
        Assert.SkipWhen(language is null, "Keine Erkennungssprache (de/en) installiert.");
        using var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(Render(["Automatik Test"], 18), "xx-YY");

        Normalize(OcrTextBuilder.Build(result.Lines)).ShouldContain("Automatik");
        result.LanguageTag.ShouldNotBeNull();
    }
}
