#pragma warning disable xUnit1051
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GodSaveTheScreen.App.Controls;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.App.Views.Settings;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;
using NSubstitute;
using NavigationView = Wpf.Ui.Controls.NavigationView;

namespace GodSaveTheScreen.App.Tests;

/// <summary>
/// Erzeugt die Bildschirmfotos der Benutzeranleitung (<c>docs/help/{de,en}/images</c>) mit den echten Fenstern.
/// Läuft nur mit Umgebungsvariable <c>GSTS_HELP_OUT</c> (Pfad zu <c>docs/help</c>) und allein im Prozess
/// (eine WPF-<c>Application</c> je Prozess): <c>build/generate-help-screenshots.ps1</c>.
/// </summary>
[Collection(WpfApplicationTests.Name)]
public sealed class HelpScreenshotGenerator
{
    /// <summary>Auflösung der Bilder (1,5 = 144 dpi, scharf auf hochauflösenden Bildschirmen); die Hilfe zeigt sie in Originalgröße (<c>help.js</c>).</summary>
    private const double Scale = 1.5;

    private static readonly Color Backdrop = Color.FromRgb(0xF3, 0xF3, 0xF3);
    private static readonly Color Accent = Color.FromRgb(0xC4, 0x2B, 0x1C);

    /// <summary>Werkzeuge mit eigenem Panel-Bild.</summary>
    private static readonly string[] PanelTools =
    [
        RectangleTool.ToolId, ArrowTool.ToolId, TextTool.ToolId, CalloutTool.ToolId, StampTool.ToolId, MarkerTool.ToolId,
        PenTool.ToolId, HighlighterTool.ToolId, EraserTool.ToolId, BlurTool.ToolId, MagnifierTool.ToolId, EmojiTool.ToolId,
        ImageTool.ToolId, AreaSelectTool.ToolId, BackgroundEraserTool.ToolId,
    ];

    private static readonly string[] SectionKeys =
    [
        "Style", "Font", "Colors", "Drawing", "Effect", "Geometry", "Magnifier", "MaskBrush", "MaskSelection", "Numbering",
        "StampQuickText", "CalloutShape",
    ];

    [StaFact]
    public void Hilfe_Bildschirmfotos_Erzeugen()
    {
        if (Environment.GetEnvironmentVariable("GSTS_HELP_OUT") is not { Length: > 0 } root)
        {
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

        foreach (var language in new[] { "de", "en" })
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            var directory = System.IO.Path.Combine(root, language, "images");
            Directory.CreateDirectory(directory);
            var texts = new MockTexts(language == "de");

            StartWindowShot(directory);
            EditorShots(directory, texts);
            SettingsShots(directory);
            ShortcutsShot(directory);
        }
    }

    // ---- Fenster ----

    private static void StartWindowShot(string directory)
    {
        var coordinator = TestCoordinator.Create(new FakeSettings()).Coordinator;
        var viewModel = new StartViewModel(coordinator, new FakeSettings(), Substitute.For<IAppShell>(), Substitute.For<IEditorService>(), Substitute.For<IDialogService>(), TestDoubles.EmptyRecents(), Substitute.For<IProjectService>());
        var window = new StartWindow(viewModel, Substitute.For<IThemeService>());
        ShowOffscreen(window);
        try
        {
            SaveWindow(window, directory, "start-window", trim: true);
        }
        finally
        {
            window.Close();
        }
    }

    private static void SettingsShots(string directory)
    {
        var hotkeys = Substitute.For<IHotkeyService>();
        hotkeys.Statuses.Returns(new Dictionary<CaptureMode, HotkeyStatus>());
        var viewModel = new SettingsViewModel(new FakeSettings(), Substitute.For<IAutostartService>(), hotkeys, Substitute.For<IThemeService>(), Substitute.For<IAppPaths>(), Substitute.For<IDialogService>());
        var window = new SettingsWindow(viewModel, Substitute.For<IThemeService>(), Substitute.For<IAppShell>());
        ShowOffscreen(window);
        try
        {
            var navigation = (NavigationView)window.FindName("Navigation");
            foreach (var (page, name) in new[]
            {
                (typeof(GeneralPage), "settings-general"),
                (typeof(CapturePage), "settings-capture"),
                (typeof(PostCapturePage), "settings-postcapture"),
                (typeof(HotkeysPage), "settings-hotkeys"),
                (typeof(AboutPage), "settings-about"),
            })
            {
                navigation.Navigate(page);
                Settle(window);
                SaveWindow(window, directory, name);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void ShortcutsShot(string directory)
    {
        var h = new EditorHarness();
        var window = new ShortcutsWindow(ShortcutCatalog.Build(h.Tools, new AppSettings()), Substitute.For<IThemeService>(), Substitute.For<IAppShell>());
        ShowOffscreen(window);
        try
        {
            SaveWindow(window, directory, "shortcuts-window");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Editor ----

    private static void EditorShots(string directory, MockTexts texts)
    {
        var h = new EditorHarness(new ImageDocument(PixelBufferExtensions.FromBitmapSource(MockScreenshot.Render(texts))));
        var window = new EditorWindow(h.ViewModel, Substitute.For<IEditorService>(), Substitute.For<IThemeService>(), Substitute.For<IAppShell>())
        {
            Width = 1400,
            Height = 900,
        };
        ShowOffscreen(window);
        try
        {
            ToolIcons(h, directory);

            // Werkzeug-Panel im Ausgangszustand (Effekte zugeklappt), danach alle Karten offen.
            h.ViewModel.Activate(h.Tools.Get<RectangleTool>());
            Settle(window);
            SavePanel(window, directory, "properties-panel");
            foreach (var key in SectionKeys)
            {
                h.ViewModel.PanelSections.SetExpanded(key, true);
            }

            foreach (var id in PanelTools)
            {
                h.ViewModel.Activate(h.Tools.Find(id)!);
                Settle(window);
                SavePanel(window, directory, "panel-" + id);
            }

            Annotate(h, texts);
            h.ViewModel.Activate(h.Tools.Get<SelectTool>());
            h.Context.Selection.Set(h.Document.Annotations.OfType<Core.Annotations.CalloutAnnotation>().First());
            h.ViewModel.DismissInfoCommand.Execute(null);
            Settle(window);
            SaveWindow(window, directory, "editor-overview", OverviewRegions(window));
            SaveElement(CommandBar(window), directory, "editor-toolbar");

            // Ebenenliste
            var tabs = Descendants<TabControl>(window).First();
            tabs.SelectedIndex = 1;
            Settle(window);
            SaveElement(Descendants<PropertyPanel>(window).First(), directory, "layers", trim: true);
            tabs.SelectedIndex = 0;

            // Zuschneiden
            h.Context.Selection.Clear();
            h.ViewModel.Activate(h.Tools.Get<CropTool>());
            Settle(window);
            SaveWindow(window, directory, "crop-mode");
            h.ViewModel.Activate(h.Tools.Get<SelectTool>());
        }
        finally
        {
            window.Closing += (_, e) => e.Cancel = false;
            h.Document.MarkSaved();
            window.Close();
        }
    }

    /// <summary>Beispiel-Objekte auf dem Beispiel-Screenshot (Koordinaten passend zu <see cref="MockScreenshot"/>).</summary>
    private static void Annotate(EditorHarness h, MockTexts texts)
    {
        void Drag<T>(double x1, double y1, double x2, double y2)
            where T : ToolBase
        {
            h.ViewModel.Activate(h.Tools.Get<T>());
            var tool = h.ViewModel.ActiveTool;
            tool.OnPointerDown(EditorHarness.Pointer(x1, y1));
            tool.OnPointerMove(EditorHarness.Pointer((x1 + x2) / 2, (y1 + y2) / 2));
            tool.OnPointerMove(EditorHarness.Pointer(x2, y2));
            tool.OnPointerUp(EditorHarness.Pointer(x2, y2));
        }

        void Click<T>(double x, double y)
            where T : ToolBase => Drag<T>(x, y, x, y);

        Drag<HighlighterTool>(62, 463, 560, 463);
        Drag<BlurTool>(194, 205, 606, 238);
        Drag<RectangleTool>(44, 518, 200, 574);
        Drag<ArrowTool>(380, 604, 208, 560);
        Click<MarkerTool>(26, 158);
        Click<MarkerTool>(26, 222);
        Click<MarkerTool>(26, 286);
        Drag<CalloutTool>(660, 120, 900, 200);
        h.Context.CommitTextEdit(texts.Callout);
        Click<StampTool>(790, 420);
        h.Context.CommitTextEdit(texts.Stamp);

        // Spitze der Sprechblase auf das Formular richten.
        var callout = h.Document.Annotations.OfType<Core.Annotations.CalloutAnnotation>().First();
        callout.Tail = new Core.Geometry.PointD(616, 284);
    }

    private static IEnumerable<(Rect Bounds, int Number)> OverviewRegions(EditorWindow window)
    {
        Rect Bounds(FrameworkElement element) => element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
        var status = Ancestor<Border>((FrameworkElement)window.FindName("ZoomBox"));
        return
        [
            (Bounds(CommandBar(window)), 1),
            (Bounds((FrameworkElement)window.FindName("ToolList")), 2),
            (Bounds((FrameworkElement)window.FindName("Canvas")), 3),
            (Bounds(Descendants<PropertyPanel>(window).First()), 4),
            (Bounds(status), 5),
        ];
    }

    private static DockPanel CommandBar(EditorWindow window) =>
        Ancestor<DockPanel>((FrameworkElement)window.FindName("NewButton"));

    /// <summary>
    /// Symbol jedes Werkzeugs (<c>icon-&lt;id&gt;.png</c>) für Tabellen und Überschriften – eigenständig gerendert wie in der
    /// Werkzeugleiste (Symbolschrift bzw. <see cref="ToolBase.IconPath"/>). Ein Abbild des Listeneintrags wäre um dessen
    /// Versatz verschoben und abgeschnitten.
    /// </summary>
    private static void ToolIcons(EditorHarness h, string directory)
    {
        const double box = 32;
        var ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        foreach (var tool in h.Tools.PaletteTools)
        {
            FrameworkElement icon = tool.IconPath is { } data
                ? new Viewbox
                {
                    Width = 20,
                    Height = 20,
                    Child = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Width = 24, Height = 24, Stretch = Stretch.None, Stroke = ink, StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round },
                }
                : new Wpf.Ui.Controls.SymbolIcon { Symbol = tool.Symbol, FontSize = 20, Foreground = ink };
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            var host = new Grid { Width = box, Height = box, Children = { icon } };
            host.Measure(new Size(box, box));
            host.Arrange(new Rect(0, 0, box, box));
            host.UpdateLayout();
            Save(host, new Size(box, box), directory, "icon-" + tool.Id);
        }
    }

    // ---- Rendern ----

    private static void ShowOffscreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -30000;
        window.Top = 0;
        window.ShowActivated = false;
        window.Show();
        Settle(window);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 60; i++)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Thread.Sleep(15);
        }

        window.UpdateLayout();
    }

    /// <summary>Fenster als Bild; mit <paramref name="regions"/> bekommt es einen weißen Rand, auf dem die Nummern der Bereiche sitzen.</summary>
    private static void SaveWindow(Window window, string directory, string name, IEnumerable<(Rect Bounds, int Number)>? regions = null, bool trim = false)
    {
        Settle(window);
        var pad = regions is null ? 0 : 18;
        var windowRect = new Rect(pad, pad, window.ActualWidth, window.ActualHeight);
        var size = new Size(window.ActualWidth + (2 * pad), window.ActualHeight + (2 * pad));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(pad > 0 ? Brushes.White : new SolidColorBrush(Backdrop), null, new Rect(size));
            dc.DrawRectangle(new SolidColorBrush(Backdrop), null, windowRect);
            dc.DrawRectangle(Snapshot(window), null, windowRect);
            foreach (var (bounds, number) in regions ?? [])
            {
                bounds.Offset(pad, pad);
                DrawRegion(dc, bounds, number);
            }
        }

        Save(visual, size, directory, name, trim);
    }

    private static void SavePanel(Window window, string directory, string name)
    {
        var panel = Descendants<PropertyPanel>(window).First();
        var content = (FrameworkElement)Descendants<ScrollViewer>(panel).First().Content;
        // Einige Felder ragen ein paar Pixel über die Breite des Inhalts hinaus (rechter Rand im ScrollViewer).
        SaveElement(content, directory, name, trim: true, extraWidth: 10);
    }

    private static void SaveElement(FrameworkElement element, string directory, string name, bool transparent = false, bool trim = false, double extraWidth = 0)
    {
        element.UpdateLayout();
        var size = new Size(element.RenderSize.Width + extraWidth, element.RenderSize.Height);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (!transparent)
            {
                dc.DrawRectangle(new SolidColorBrush(Backdrop), null, new Rect(size));
            }

            dc.DrawRectangle(Snapshot(element, size), null, new Rect(size));
        }

        Save(visual, size, directory, name, trim);
    }

    /// <summary>Ganzes Element unabhängig von Clips der Vorfahren (z. B. Panel-Inhalt im ScrollViewer).</summary>
    private static VisualBrush Snapshot(FrameworkElement element) => new(element)
    {
        Stretch = Stretch.None,
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top,
        ViewboxUnits = BrushMappingMode.Absolute,
        Viewbox = new Rect(element.RenderSize),
    };

    private static VisualBrush Snapshot(FrameworkElement element, Size size) => new(element)
    {
        Stretch = Stretch.None,
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top,
        ViewboxUnits = BrushMappingMode.Absolute,
        Viewbox = new Rect(size),
    };

    private static void DrawRegion(DrawingContext dc, Rect bounds, int number)
    {
        var accent = new SolidColorBrush(Accent);
        var pen = new Pen(accent, 2.5) { DashStyle = new DashStyle([3, 2], 0) };
        bounds.Inflate(-2, -2);
        dc.DrawRoundedRectangle(null, pen, bounds, 6, 6);
        var center = new Point(bounds.Left + 2, bounds.Top + 2);
        dc.DrawEllipse(accent, new Pen(Brushes.White, 2), center, 14, 14);
        var text = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 16, Brushes.White, 1.0);
        dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
    }

    private static void Save(Visual visual, Size size, string directory, string name, bool trim = false)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * Scale), (int)Math.Ceiling(size.Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        BitmapSource result = bitmap;
        if (trim && ContentHeight(bitmap) is var height && height < bitmap.PixelHeight)
        {
            result = new CroppedBitmap(bitmap, new Int32Rect(0, 0, bitmap.PixelWidth, height));
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(result));
        using var stream = File.Create(System.IO.Path.Combine(directory, name + ".png"));
        encoder.Save(stream);
    }

    /// <summary>Höhe bis zur letzten Zeile, die nicht nur aus Hintergrundfarbe besteht (plus kleiner Rand).</summary>
    private static int ContentHeight(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        for (var y = bitmap.PixelHeight - 1; y >= 0; y--)
        {
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                var i = (y * stride) + (x * 4);
                if (Math.Abs(pixels[i] - Backdrop.B) > 6 || Math.Abs(pixels[i + 1] - Backdrop.G) > 6 || Math.Abs(pixels[i + 2] - Backdrop.R) > 6)
                {
                    return Math.Min(bitmap.PixelHeight, y + 1 + (int)(12 * Scale));
                }
            }
        }

        return bitmap.PixelHeight;
    }

    private static T Ancestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(element); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        throw new InvalidOperationException(typeof(T).Name + " nicht gefunden");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    // ---- Beispiel-Screenshot ----

    private sealed record MockTexts(bool German)
    {
        public string Title => German ? "Kundenportal" : "Customer portal";

        public string[] Navigation => German ? ["Übersicht", "Profil", "Rechnungen", "Support"] : ["Overview", "Profile", "Invoices", "Support"];

        public string Heading => German ? "Profil bearbeiten" : "Edit profile";

        public (string Label, string Value)[] Fields => German
            ? [("Name", "Erika Mustermann"), ("E-Mail", "erika.mustermann@example.com"), ("Telefon", "+49 30 1234567")]
            : [("Name", "Jane Doe"), ("Email", "jane.doe@example.com"), ("Phone", "+1 555 0100")];

        public string Newsletter => German ? "Newsletter abonnieren" : "Subscribe to the newsletter";

        public string Note => German
            ? "Änderungen an der E-Mail-Adresse müssen per Link bestätigt werden."
            : "Changes to the email address must be confirmed via link.";

        public string Save => German ? "Speichern" : "Save";

        public string Cancel => German ? "Abbrechen" : "Cancel";

        public string Callout => German ? "Pflichtfelder prüfen" : "Check required fields";

        public string Stamp => German ? "ENTWURF" : "DRAFT";
    }

    /// <summary>Erfundene Webseite als Bildinhalt (960 × 640), damit die Bildschirmfotos keine echten Daten zeigen.</summary>
    private static class MockScreenshot
    {
        public static RenderTargetBitmap Render(MockTexts t)
        {
            const double width = 960;
            const double height = 640;
            var canvas = new Canvas { Width = width, Height = height, Background = Brushes.White };
            var font = new FontFamily("Segoe UI");

            void Add(UIElement element, double x, double y)
            {
                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
                canvas.Children.Add(element);
            }

            TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null) =>
                new() { Text = text, FontFamily = font, FontSize = size, Foreground = brush, FontWeight = weight ?? FontWeights.Normal };

            var dark = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
            var muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            var blue = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            var line = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));

            Add(new Rectangle { Width = width, Height = 64, Fill = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)) }, 0, 0);
            Add(new Ellipse { Width = 30, Height = 30, Fill = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)) }, 24, 17);
            Add(Text(t.Title, 22, Brushes.White, FontWeights.SemiBold), 66, 16);
            var x = 520.0;
            foreach (var item in t.Navigation)
            {
                Add(Text(item, 15, Brushes.White), x, 22);
                x += 100;
            }

            Add(Text(t.Heading, 26, dark, FontWeights.SemiBold), 48, 92);
            var y = 150.0;
            foreach (var (label, value) in t.Fields)
            {
                Add(Text(label, 14, muted), 52, y - 2);
                Add(new Border { Width = 420, Height = 38, BorderBrush = line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = Brushes.White }, 190, y - 12);
                Add(Text(value, 16, dark), 204, y - 4);
                y += 64;
            }

            Add(new Border { Width = 18, Height = 18, BorderBrush = blue, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(3), Background = blue }, 192, 352);
            Add(Text("✓", 13, Brushes.White, FontWeights.Bold), 196, 350);
            Add(Text(t.Newsletter, 15, dark), 220, 350);

            Add(new Border { Width = 560, Height = 64, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xBF, 0xDB, 0xFE)), BorderThickness = new Thickness(1) }, 48, 430);
            Add(Text(t.Note, 15, dark), 64, 452);

            Add(new Border { Width = 140, Height = 40, CornerRadius = new CornerRadius(6), Background = blue }, 52, 526);
            Add(Text(t.Save, 16, Brushes.White, FontWeights.SemiBold), 52 + 70 - (t.Save.Length * 4.5), 535);
            Add(new Border { Width = 140, Height = 40, CornerRadius = new CornerRadius(6), BorderBrush = line, BorderThickness = new Thickness(1) }, 210, 526);
            Add(Text(t.Cancel, 16, dark), 210 + 70 - (t.Cancel.Length * 4.3), 535);

            canvas.Measure(new Size(width, height));
            canvas.Arrange(new Rect(0, 0, width, height));
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
