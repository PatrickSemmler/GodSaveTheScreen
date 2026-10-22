#pragma warning disable xUnit1051 // Die Tests brauchen keine Abbruchbarkeit
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Xml.Linq;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Localization;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Durchsucht alle XAML-Dateien des App-Projekts.</summary>
internal static class XamlFiles
{
    public static IEnumerable<(string Path, XDocument Document)> All()
    {
        var root = Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App");
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.StartsWith("obj", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (relative, XDocument.Load(file));
        }
    }
}

public sealed class LocalizationCompletenessTests
{
    private static readonly HashSet<string> TextAttributes = new(StringComparer.Ordinal)
    {
        "Content", "Text", "Header", "ToolTip", "Title", "PlaceholderText", "InputGestureText", "Watermark",
        "AutomationProperties.Name", "AutomationProperties.HelpText",
    };

    /// <summary>Technische Literale, die in jeder Sprache gleich sind.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) { "X", "Y", "#AARRGGBB", "GodSaveTheScreen" };

    private static bool IsViolation(string value) =>
        !value.StartsWith('{')
        && value.Any(char.IsLetter)
        && !Allowed.Contains(value)
        && !Regex.IsMatch(value, @"^\w+Page$"); // Seitentitel der Einstellungen-Navigation (nicht sichtbar)

    [Fact]
    public void Xaml_EnthaeltKeineHartkodiertenAnzeigetexte()
    {
        var violations = new List<string>();
        foreach (var (path, document) in XamlFiles.All())
        {
            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes().Where(a => TextAttributes.Contains(a.Name.LocalName)))
                {
                    if (IsViolation(attribute.Value))
                    {
                        violations.Add($"{path}: <{element.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\">");
                    }
                }

                var inner = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
                if (inner.Length > 0 && IsViolation(inner))
                {
                    violations.Add($"{path}: <{element.Name.LocalName}>{inner}</{element.Name.LocalName}>");
                }
            }
        }

        violations.ShouldBeEmpty("Anzeigetexte gehören in Strings.resx/Strings.de.resx ({l:Loc …} bzw. {l:Shortcut …}):\n" + string.Join('\n', violations));
    }

    [Fact]
    public void Xaml_LocSchluessel_ExistierenInBeidenSprachen()
    {
        var resources = Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Resources");
        HashSet<string> Keys(string file) =>
            XDocument.Load(Path.Combine(resources, file)).Root!.Elements("data").Select(e => (string)e.Attribute("name")!).ToHashSet();
        var english = Keys("Strings.resx");
        var german = Keys("Strings.de.resx");

        var missing = new List<string>();
        foreach (var (path, document) in XamlFiles.All())
        {
            foreach (var attribute in document.Descendants().SelectMany(e => e.Attributes()))
            {
                foreach (Match match in Regex.Matches(attribute.Value, @"\{l:Loc\s+(\w+)\}"))
                {
                    var key = match.Groups[1].Value;
                    if (!english.Contains(key) || !german.Contains(key))
                    {
                        missing.Add($"{path}: {key}");
                    }
                }
            }
        }

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Resx_KeinWertIstLeerOderNurEinSchluesselname()
    {
        var resources = Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Resources");
        foreach (var file in new[] { "Strings.resx", "Strings.de.resx" })
        {
            var empty = XDocument.Load(Path.Combine(resources, file)).Root!.Elements("data")
                .Where(e => string.IsNullOrWhiteSpace((string?)e.Element("value")) || (string?)e.Element("value") == (string)e.Attribute("name")!)
                .Select(e => (string)e.Attribute("name")!)
                .ToList();
            empty.ShouldBeEmpty($"leere oder unübersetzte Werte in {file}");
        }
    }

    [Fact]
    public void Strings_DesignerEnthaeltJedenSchluessel()
    {
        var resources = Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Resources");
        var keys = XDocument.Load(Path.Combine(resources, "Strings.resx")).Root!.Elements("data").Select(e => (string)e.Attribute("name")!).ToList();
        var designer = File.ReadAllText(Path.Combine(resources, "Strings.Designer.cs"));
        keys.Where(k => !designer.Contains($"public static string {k} =>", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void Resx_FormatPlatzhalter_SindInBeidenSprachenGleich()
    {
        var resources = Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Resources");
        Dictionary<string, string> Values(string file) =>
            XDocument.Load(Path.Combine(resources, file)).Root!.Elements("data").ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty);
        var english = Values("Strings.resx");
        var german = Values("Strings.de.resx");
        static string Placeholders(string text) => string.Join(',', Regex.Matches(text, @"\{(\d+)[^}]*\}").Select(m => m.Groups[1].Value).Distinct().Order());

        var differing = english.Where(kv => german.TryGetValue(kv.Key, out var de) && Placeholders(kv.Value) != Placeholders(de)).Select(kv => kv.Key).ToList();
        differing.ShouldBeEmpty("unterschiedliche Platzhalter in DE/EN");
    }
}

public sealed class AccessibilityTests
{
    [Fact]
    public void Icon_Schaltflaechen_HabenEinenAutomationsnamen()
    {
        var buttons = new HashSet<string>(StringComparer.Ordinal) { "Button", "ToggleButton", "RepeatButton" };
        var violations = new List<string>();
        foreach (var (path, document) in XamlFiles.All())
        {
            foreach (var element in document.Descendants().Where(e => buttons.Contains(e.Name.LocalName)))
            {
                var hasLabel = element.Attribute("Content") is not null
                    || element.Attribute("AutomationProperties.Name") is not null
                    || element.Elements().Any(c => c.Name.LocalName.EndsWith($"{element.Name.LocalName}.Content", StringComparison.Ordinal))
                    || element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value));
                if (!hasLabel)
                {
                    violations.Add($"{path}: <{element.Name.LocalName}> ohne Content/AutomationProperties.Name");
                }
            }
        }

        violations.ShouldBeEmpty("Symbolschaltflächen brauchen AutomationProperties.Name (Screenreader):\n" + string.Join('\n', violations));
    }

    [Fact]
    public void Eingabefelder_HabenEinenAutomationsnamen()
    {
        var inputs = new HashSet<string>(StringComparer.Ordinal) { "NumberBox", "Slider", "ComboBox", "TextBox", "PasswordBox" };
        var violations = new List<string>();
        foreach (var (path, document) in XamlFiles.All())
        {
            foreach (var element in document.Descendants().Where(e => inputs.Contains(e.Name.LocalName)))
            {
                // Felder in Vorlagen oder mit vorangestellter Beschriftung (Header/Placeholder) gelten als benannt.
                if (element.Attribute("AutomationProperties.Name") is null
                    && element.Attribute("Header") is null
                    && element.Attribute("PlaceholderText") is null
                    && element.Attribute("Visibility")?.Value != "Collapsed"
                    && element.Ancestors().All(a => a.Name.LocalName != "ControlTemplate"))
                {
                    violations.Add($"{path}: <{element.Name.LocalName} x:Name=\"{element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value}\">");
                }
            }
        }

        // Einstellungsseiten benennen ihre Felder über die Beschriftung daneben (Label-Fokus); hier zählen nur die Editor-Controls.
        violations.Where(v => v.StartsWith("Controls", StringComparison.Ordinal) || v.StartsWith("Views\\EditorWindow", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }
}

public sealed class ShortcutTextTests
{
    [Theory]
    [InlineData("Ctrl+Shift+S", "Strg+Umschalt+S")]
    [InlineData("Ctrl++", "Strg++")]
    [InlineData("Ctrl+-", "Strg+-")]
    [InlineData("Ctrl+Shift+]", "Strg+Umschalt+]")]
    [InlineData("Del", "Entf")]
    [InlineData("Enter", "Eingabe")]
    [InlineData("Alt+Arrow", "Alt+Arrow")]
    [InlineData("F1", "F1")]
    public void Deutsch_UebersetztModifikatorenUndTasten(string input, string expected) =>
        ShortcutText.Localize(input, new CultureInfo("de-DE")).ShouldBe(expected);

    [Theory]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("Ctrl++")]
    [InlineData("Del")]
    public void Englisch_BleibtUnveraendert(string input) =>
        ShortcutText.Localize(input, new CultureInfo("en-US")).ShouldBe(input);
}

public sealed class ShortcutCatalogTests
{
    [StaFact]
    public void Katalog_EnthaeltJedesWerkzeugkuerzel()
    {
        var harness = new EditorHarness();

        var entries = ShortcutCatalog.Build(harness.Tools, new AppSettings());

        foreach (var tool in harness.Tools.PaletteTools.Where(t => t.ShortcutKey != Key.None))
        {
            entries.ShouldContain(e => e.Keys == tool.ShortcutKey.ToString(), $"Werkzeug {tool.Id}");
        }

        entries.Count(e => e.Category == Strings.Shortcuts_Tools).ShouldBe(harness.Tools.PaletteTools.Count(t => t.ShortcutKey != Key.None));
    }

    [StaFact]
    public void Katalog_EnthaeltGlobaleHotkeysAusDenEinstellungen()
    {
        var settings = new AppSettings { HotkeyRectangle = "Ctrl+Alt+R", HotkeyWindow = string.Empty };

        var entries = ShortcutCatalog.Build(null, settings);

        entries.Where(e => e.Category == Strings.Shortcuts_Capture).Select(e => e.Name).ShouldBe([Strings.Mode_Rectangle, Strings.Mode_Freeform, Strings.Mode_Fullscreen]);
        entries.First(e => e.Category == Strings.Shortcuts_Capture).Keys.ShouldContain("Alt");
    }

    [Fact]
    public void Katalog_AlleTexteSindUebersetzt()
    {
        foreach (var (category, name, keys) in ShortcutCatalog.EditorCommands)
        {
            Strings.ResourceManager.GetString(category).ShouldNotBeNull(category);
            Strings.ResourceManager.GetString(name).ShouldNotBeNull(name);
            keys.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Katalog_KuerzelDerKernbefehleStimmenMitDemFensterUeberein()
    {
        // Die Übersicht darf nicht von den tatsächlichen Kürzeln abweichen (EditorWindow.TryGlobalShortcut).
        var window = File.ReadAllText(Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Views", "EditorWindow.xaml.cs"));
        foreach (var key in new[] { "Key.S", "Key.O", "Key.Z", "Key.Y", "Key.C", "Key.V", "Key.X", "Key.D", "Key.R" })
        {
            window.ShouldContain($"case {key}:");
        }

        var shortcuts = ShortcutCatalog.EditorCommands.Select(c => c.Keys).ToList();
        shortcuts.ShouldContain("Ctrl+S");
        shortcuts.ShouldContain("Ctrl+O");
        shortcuts.ShouldContain("Ctrl+Z");
        shortcuts.ShouldContain("Ctrl+D");
        shortcuts.ShouldContain("F1");
    }

    [Fact]
    public void ViewModel_GruppiertUndFiltert()
    {
        var entries = new List<ShortcutEntry>
        {
            new("A", "Speichern", "Strg+S"),
            new("A", "Öffnen", "Strg+O"),
            new("B", "Rechteck", "R"),
        };
        var viewModel = new ShortcutsViewModel(entries);

        viewModel.View.Groups!.Count.ShouldBe(2);

        viewModel.Filter = "strg";
        viewModel.View.Cast<ShortcutRow>().Select(r => r.Name).ShouldBe(["Speichern", "Öffnen"]);

        viewModel.Filter = "rechteck";
        viewModel.View.Cast<ShortcutRow>().Single().Keys.ShouldBe("R");

        viewModel.Filter = string.Empty;
        viewModel.View.Cast<ShortcutRow>().Count().ShouldBe(3);
        viewModel.Rows[0].Automation.ShouldBe("Speichern: Strg+S");
    }
}

public sealed class KeyboardObjectTests
{
    private static (EditorHarness Harness, SelectTool Tool) Create()
    {
        var harness = new EditorHarness();
        return (harness, harness.Tools.Get<SelectTool>());
    }

    [StaFact]
    public void Tab_WaehltObjekteNacheinanderUndVerlaesstDieZeichenflaeche()
    {
        var (h, tool) = Create();
        var a = h.AddRectangle(10, 10, 20, 20);
        var b = h.AddRectangle(50, 50, 20, 20);

        tool.OnKeyDown(Key.Tab, ModifierKeys.None).ShouldBeTrue();
        h.Context.Selection.Items.ShouldBe([a]);
        tool.OnKeyDown(Key.Tab, ModifierKeys.None).ShouldBeTrue();
        h.Context.Selection.Items.ShouldBe([b]);

        // Nach dem letzten Objekt darf der Fokus weiterwandern.
        tool.OnKeyDown(Key.Tab, ModifierKeys.None).ShouldBeFalse();
        h.Context.Selection.IsEmpty.ShouldBeTrue();
    }

    [StaFact]
    public void UmschaltTab_GehtRueckwaerts()
    {
        var (h, tool) = Create();
        var a = h.AddRectangle(10, 10, 20, 20);
        var b = h.AddRectangle(50, 50, 20, 20);

        tool.OnKeyDown(Key.Tab, ModifierKeys.Shift).ShouldBeTrue();
        h.Context.Selection.Items.ShouldBe([b]);
        tool.OnKeyDown(Key.Tab, ModifierKeys.Shift).ShouldBeTrue();
        h.Context.Selection.Items.ShouldBe([a]);
        tool.OnKeyDown(Key.Tab, ModifierKeys.Shift).ShouldBeFalse();
    }

    [StaFact]
    public void Tab_UeberspringtUnsichtbareObjekteUndBleibtOhneObjekteUntaetig()
    {
        var (h, tool) = Create();
        tool.OnKeyDown(Key.Tab, ModifierKeys.None).ShouldBeFalse();

        var hidden = h.AddRectangle(10, 10, 20, 20);
        hidden.IsVisible = false;
        var visible = h.AddRectangle(50, 50, 20, 20);

        tool.OnKeyDown(Key.Tab, ModifierKeys.None).ShouldBeTrue();
        h.Context.Selection.Items.ShouldBe([visible]);
    }

    [StaFact]
    public void Pfeiltasten_VerschiebenUmEinsUndMitUmschaltUmZehn()
    {
        var (h, tool) = Create();
        var rect = h.AddRectangle(10, 10, 20, 20);
        h.Context.Selection.Set(rect);

        tool.OnKeyDown(Key.Right, ModifierKeys.None).ShouldBeTrue();
        rect.Rect.X.ShouldBe(11);
        tool.OnKeyDown(Key.Down, ModifierKeys.Shift).ShouldBeTrue();
        rect.Rect.Y.ShouldBe(20);
    }

    [StaFact]
    public void AltPfeil_AendertDieGroesse()
    {
        var (h, tool) = Create();
        var rect = h.AddRectangle(10, 10, 20, 20);
        h.Context.Selection.Set(rect);

        tool.OnKeyDown(Key.Right, ModifierKeys.Alt).ShouldBeTrue();
        rect.Rect.Width.ShouldBe(21, 0.001);
        rect.Rect.X.ShouldBe(10, 0.001);

        tool.OnKeyDown(Key.Down, ModifierKeys.Alt | ModifierKeys.Shift).ShouldBeTrue();
        rect.Rect.Height.ShouldBe(30, 0.001);

        tool.OnKeyDown(Key.Left, ModifierKeys.Alt | ModifierKeys.Shift).ShouldBeTrue();
        rect.Rect.Width.ShouldBe(11, 0.001);

        h.Context.Undo.Undo();
        rect.Rect.Width.ShouldBe(21, 0.001);
    }

    [StaFact]
    public void AltPfeil_VerkleinertNieUnterEinPixel()
    {
        var (h, tool) = Create();
        var rect = h.AddRectangle(10, 10, 3, 3);
        h.Context.Selection.Set(rect);

        tool.OnKeyDown(Key.Left, ModifierKeys.Alt | ModifierKeys.Shift);

        rect.Rect.Width.ShouldBe(1, 0.001);
    }

    [StaFact]
    public void AltPfeil_GesperrteObjekteBleibenUnveraendert()
    {
        var (h, tool) = Create();
        var rect = h.AddRectangle(10, 10, 20, 20);
        rect.IsLocked = true;
        h.Context.Selection.Set(rect);

        tool.OnKeyDown(Key.Right, ModifierKeys.Alt);

        rect.Rect.Width.ShouldBe(20);
    }

    [StaFact]
    public void AltPfeil_TextWirdProportionalSkaliert()
    {
        var (h, tool) = Create();
        var text = new TextAnnotation { Text = "Hallo", Position = new PointD(10, 10), FontSize = 20 };
        h.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(h.Document.Annotations, text));
        h.Context.Selection.Set(text);
        var before = text.FontSize;

        tool.OnKeyDown(Key.Right, ModifierKeys.Alt | ModifierKeys.Shift);

        text.FontSize.ShouldBeGreaterThan(before);
    }
}

public sealed class FirstRunHintTests
{
    [Fact]
    public void Hinweis_WirdNurEinmalAngezeigt()
    {
        var settings = new FakeSettings();
        var dialogs = Substitute.For<IDialogService>();
        var hint = new FirstRunHint(settings, dialogs);

        hint.ShowIfNeeded().ShouldBeTrue();
        hint.ShowIfNeeded().ShouldBeFalse();

        dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        settings.Current.FirstRunHintShown.ShouldBeTrue();
    }

    [Fact]
    public void Nachricht_NenntDieHotkeysUndDieDruckTaste()
    {
        var message = FirstRunHint.BuildMessage(new AppSettings { HotkeyRectangle = "Ctrl+Shift+1", HotkeyFreeform = string.Empty });

        message.ShouldContain(HotkeyDisplay("Ctrl+Shift+1"));
        message.ShouldContain(Strings.FirstRun_NotAssigned);
        message.ShouldContain("F1");
        (message.Contains("Print", StringComparison.Ordinal) || message.Contains("Druck", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private static string HotkeyDisplay(string gesture) =>
        GodSaveTheScreen.Core.Hotkeys.HotkeyGesture.TryParse(gesture)!.ToDisplayString(CultureInfo.CurrentUICulture);
}

public sealed class LargeImageTests
{
    [Theory]
    [InlineData(1920, 1080, false)]
    [InlineData(7680, 4320, false)]
    [InlineData(10000, 10000, false)]
    [InlineData(10001, 10000, true)]
    [InlineData(16384, 16384, true)]
    public void Warnschwelle(int width, int height, bool expected) =>
        LargeImageGuard.IsLarge(width, height).ShouldBe(expected);

    [Fact]
    public void Speicherbedarf_IstVierBytesProPixel() =>
        LargeImageGuard.BytesFor(16384, 16384).ShouldBe(1L << 30);

    [Fact]
    public async Task Oeffnen_SehrGrossesBild_FragtNachUndLaedtBeiNeinNicht()
    {
        var codec = Substitute.For<IImageCodecService>();
        codec.PeekSize(Arg.Any<string>()).Returns((16384, 16384));
        var dialogs = Substitute.For<IDialogService>();
        dialogs.Confirm(default!, default!).ReturnsForAnyArgs(false);
        var service = new EditorService(
            Substitute.For<IServiceProvider>(),
            Substitute.For<IWindowExclusionService>(),
            codec,
            Substitute.For<IProjectService>(),
            TestDoubles.EmptyRecents(),
            Substitute.For<IClipboardService>(),
            dialogs,
            Substitute.For<GodSaveTheScreen.Capture.IMonitorService>(),
            NullLogger<EditorService>.Instance);

        await service.OpenFilesAsync([@"C:\temp\riesig.png"]);

        dialogs.Received(1).Confirm(Strings.LargeImage_Title, Arg.Is<string>(m => m.Contains("16384", StringComparison.Ordinal)));
        await codec.DidNotReceiveWithAnyArgs().LoadAsync(default!, default);
    }
}

public sealed class EffectCacheCleanupTests
{
    [StaFact]
    public void RemoveImage_EntferntNurDieEffekteDesBilds()
    {
        var cache = new EffectCache();
        var bitmap = new PixelBuffer(4, 4).ToBitmapSource();
        EffectKey Key(long id) => new(id, new PixelRect(0, 0, 4, 4), new RectD(0, 0, 4, 4), BlurMode.Pixelate, 4, RegionShape.Rectangle, 0, ArgbColor.Black);
        cache.GetOrCreate(Key(1), () => bitmap);
        cache.GetOrCreate(Key(2), () => bitmap);

        cache.RemoveImage(1);

        cache.Contains(Key(1)).ShouldBeFalse();
        cache.Contains(Key(2)).ShouldBeTrue();
        cache.Count.ShouldBe(1);
    }

    [StaFact]
    public void SchliessenDesEditors_LeertDenEffektCache()
    {
        var image = PixelTools.Solid(40, 40, 10, 20, 30);
        var harness = new EditorHarness(new ImageDocument(image));
        harness.Document.Undo.Execute(new GodSaveTheScreen.Core.Undo.AddAnnotationAction(harness.Document.Annotations, new BlurAnnotation { Rect = new RectD(5, 5, 20, 20) }));
        harness.Exporter.Render(harness.Document);
        var id = BaseImageIdentity.Get(image);
        // Der Cache ist prozessweit: nur die Einträge des eigenen Bildes zählen (andere Tests laufen parallel).
        EffectCache.Shared.CountFor(id).ShouldBeGreaterThan(0);

        harness.ViewModel.Dispose();

        EffectCache.Shared.CountFor(id).ShouldBe(0);
    }
}

public sealed class ReleaseConfigurationTests
{
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([RepoPaths.Root(), .. parts]));

    [Fact]
    public void Version_IstVierstellig()
    {
        var props = XDocument.Parse(Read("Directory.Build.props"));
        var version = props.Descendants("Version").Single().Value;

        Regex.IsMatch(version, @"^\d+\.\d+\.\d+\.\d+$").ShouldBeTrue(version);
    }

    [Fact]
    public void InfoSeite_KenntEntwicklerUndProjektseite()
    {
        var metadata = typeof(GodSaveTheScreen.App.ViewModels.SettingsViewModel).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .OfType<System.Reflection.AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);

        metadata["Authors"].ShouldBe("Patrick Semmler");
        metadata["RepositoryUrl"].ShouldBe("https://github.com/PatrickSemmler/GodSaveTheScreen");
    }

    [Fact]
    public void PublishProfil_EntsprichtDerVorgabe()
    {
        var profile = XDocument.Parse(Read("src", "GodSaveTheScreen.App", "Properties", "PublishProfiles", "win-x64.pubxml"));
        string Value(string name) => profile.Descendants(name).Single().Value;

        Value("RuntimeIdentifier").ShouldBe("win-x64");
        Value("SelfContained").ShouldBe("true");
        Value("PublishSingleFile").ShouldBe("true");
        Value("IncludeNativeLibrariesForSelfExtract").ShouldBe("true");
        Value("EnableCompressionInSingleFile").ShouldBe("true");
        Value("PublishReadyToRun").ShouldBe("true");

        // WPF ist nicht trimmbar.
        profile.Descendants("PublishTrimmed").ShouldBeEmpty();
    }

    [Fact]
    public void ArmProfil_Existiert() =>
        File.Exists(Path.Combine(RepoPaths.Root(), "src", "GodSaveTheScreen.App", "Properties", "PublishProfiles", "win-arm64.pubxml")).ShouldBeTrue();

    [Fact]
    public void ReleaseSkriptUndLizenzhinweise_Existieren()
    {
        File.Exists(Path.Combine(RepoPaths.Root(), "build", "release.ps1")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(RepoPaths.Root(), "THIRD-PARTY-NOTICES.txt")).ShouldContain("WPF-UI");
    }

    [Fact]
    public void Versionsinformation_EnthaeltDenGitHash()
    {
        var targets = Read("Directory.Build.targets");
        targets.ShouldContain("SourceRevisionId");
    }
}
