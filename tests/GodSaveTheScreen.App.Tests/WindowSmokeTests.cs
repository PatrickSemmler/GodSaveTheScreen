#pragma warning disable xUnit1051
using System.Windows;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.App.Views.Settings;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using NSubstitute;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Instanziiert die neuen Fenster mit den echten XAML-Ressourcen, um Markup- und Bindungsfehler früh zu finden.</summary>
public sealed class WindowSmokeTests
{
    private static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
    }

    private static SettingsViewModel CreateSettingsViewModel()
    {
        var hotkeys = Substitute.For<IHotkeyService>();
        hotkeys.Statuses.Returns(new Dictionary<CaptureMode, HotkeyStatus>());
        return new SettingsViewModel(
            new FakeSettings(),
            Substitute.For<IAutostartService>(),
            hotkeys,
            Substitute.For<IThemeService>(),
            Substitute.For<IAppPaths>(),
            Substitute.For<IDialogService>());
    }

    [StaFact]
    public void Fenster_LassenSichMitEchtenRessourcenErzeugen()
    {
        // Eine Application gehört an genau einen Thread; daher beide Fenster in einem Test.
        EnsureApplication();
        StartWindowSmoke();
        EditorWindowSmoke();
        var dialog = new MessageDialog("Titel", "Nachricht", DialogKind.Warning, [new DialogButton("OK", 0, IsPrimary: true)]);
        dialog.Show();
        dialog.Close();
        var viewModel = CreateSettingsViewModel();
        var window = new SettingsWindow(viewModel, Substitute.For<IThemeService>());
        window.Show();
        try
        {
            // Loaded hat bereits auf die erste Seite navigiert.
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var navigation = (NavigationView)window.FindName("Navigation");
            foreach (var pageType in new[] { typeof(CapturePage), typeof(PostCapturePage), typeof(HotkeysPage), typeof(AboutPage), typeof(GeneralPage) })
            {
                navigation.Navigate(pageType).ShouldBeTrue(pageType.Name);
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void StartWindowSmoke()
    {
        var coordinator = TestCoordinator.Create(new FakeSettings()).Coordinator;
        var viewModel = new StartViewModel(coordinator, new FakeSettings(), Substitute.For<IAppShell>(), Substitute.For<IEditorService>(), Substitute.For<IDialogService>());
        var window = new StartWindow(viewModel, Substitute.For<IThemeService>());
        window.Show();
        window.Close();
    }

    private static void EditorWindowSmoke()
    {
        var h = new EditorHarness();
        var window = new EditorWindow(h.ViewModel, Substitute.For<IEditorService>(), Substitute.For<IThemeService>());
        window.Show();
        try
        {
            Idle(window);

            // Zeichnen mit dem Rechteckwerkzeug; Ebenenliste, Eigenschaften und Zeichenfläche reagieren.
            h.ViewModel.Activate(h.Tools.Get<RectangleTool>());
            var tool = h.ViewModel.ActiveTool;
            tool.OnPointerDown(EditorHarness.Pointer(10, 10));
            tool.OnPointerMove(EditorHarness.Pointer(80, 60));
            tool.OnPointerUp(EditorHarness.Pointer(80, 60));
            Idle(window);

            h.ViewModel.Layers.Items.Count.ShouldBe(1);
            h.ViewModel.Properties.IsApplicable.ShouldBeTrue();
            var canvas = (GodSaveTheScreen.App.Controls.EditorCanvas)window.FindName("Canvas");
            canvas.ShouldNotBeNull();

            // Zoom ändert die Skalierung der Zeichenfläche (Kontext-Maßstab = Zoom / DPI-Faktor).
            h.ViewModel.SetZoom(2);
            Idle(window);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
            h.Context.Scale.ShouldBe(2 / dpi, 1e-9);

            // Die Farbwahl-Popups (inkl. StandardColorPicker) lassen sich öffnen und schließen.
            var colorButtons = Descendants<GodSaveTheScreen.App.Controls.ColorButton>(window).ToList();
            colorButtons.Count.ShouldBeGreaterThanOrEqualTo(2);
            foreach (var button in colorButtons)
            {
                var toggle = (System.Windows.Controls.Primitives.ToggleButton)button.FindName("Toggle");
                toggle.IsChecked = true;
                Idle(window);
                toggle.IsChecked = false;
                Idle(window);
            }

            // Text: Eingabefeld erscheint direkt auf der Zeichenfläche und verschwindet nach dem Übernehmen.
            h.ViewModel.Activate(h.Tools.Get<TextTool>());
            Idle(window);
            h.ViewModel.TextProperties.IsApplicable.ShouldBeTrue();
            var textTool = h.ViewModel.ActiveTool;
            textTool.OnPointerDown(EditorHarness.Pointer(100, 100));
            textTool.OnPointerUp(EditorHarness.Pointer(100, 100));
            Idle(window);
            var overlay = (System.Windows.Controls.Canvas)canvas.FindName("Overlay");
            overlay.Children.Count.ShouldBe(1);
            var editor = overlay.Children[0].ShouldBeOfType<System.Windows.Controls.TextBox>();
            editor.Text = "Hallo";
            h.Context.CommitTextEdit(editor.Text);
            Idle(window);
            overlay.Children.Count.ShouldBe(0);
            h.Document.Annotations.OfType<GodSaveTheScreen.Core.Annotations.TextAnnotation>().Count().ShouldBe(1);

            // Marker setzen; Eigenschaftenbereich zeigt Marker-Abschnitt.
            h.ViewModel.Activate(h.Tools.Get<MarkerTool>());
            Idle(window);
            h.ViewModel.MarkerProperties.IsApplicable.ShouldBeTrue();
            h.ViewModel.ActiveTool.OnPointerDown(EditorHarness.Pointer(200, 60));
            h.ViewModel.ActiveTool.OnPointerUp(EditorHarness.Pointer(200, 60));
            Idle(window);
            h.Document.Annotations.OfType<GodSaveTheScreen.Core.Annotations.MarkerAnnotation>().Count().ShouldBe(1);

            // Alle Werkzeuge lassen sich aktivieren, ohne dass die Bindungen brechen.
            foreach (var t in h.Tools.PaletteTools)
            {
                h.ViewModel.Activate(t);
                Idle(window);
            }

            h.Document.MarkSaved();
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
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

    private static void Idle(Window window) =>
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
}
