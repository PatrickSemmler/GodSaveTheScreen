#pragma warning disable xUnit1051
using System.Windows;
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
        var viewModel = new StartViewModel(coordinator, new FakeSettings(), Substitute.For<IAppShell>());
        var window = new StartWindow(viewModel, Substitute.For<IThemeService>());
        window.Show();
        window.Close();
    }
}
