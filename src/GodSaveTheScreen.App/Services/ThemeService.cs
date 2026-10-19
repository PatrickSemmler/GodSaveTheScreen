using System.Windows;
using GodSaveTheScreen.Core.Settings;
using Wpf.Ui.Appearance;

namespace GodSaveTheScreen.App.Services;

public interface IThemeService
{
    void Apply();

    /// <summary>Lässt das Fenster bei System-Theme auf Wechsel der Windows-Einstellung reagieren.</summary>
    void Attach(Window window);
}

public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings) => _settings = settings;

    public void Apply()
    {
        switch (_settings.Current.Theme)
        {
            case ThemeSetting.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case ThemeSetting.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }

    public void Attach(Window window)
    {
        if (_settings.Current.Theme == ThemeSetting.System)
        {
            SystemThemeWatcher.Watch(window);
        }
    }
}
