using GodSaveTheScreen.App.Controls;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Auf-/Zugeklappt-Zustand der Panel-Abschnitte in den Einstellungen (gilt für alle Werkzeuge mit demselben Abschnitt).</summary>
public sealed class PanelSectionStore : IPanelSectionStore
{
    private readonly ISettingsService _settings;

    public PanelSectionStore(ISettingsService settings) => _settings = settings;

    public bool IsExpanded(string key, bool fallback) => _settings.Current.PanelSections.TryGetValue(key, out var expanded) ? expanded : fallback;

    public void SetExpanded(string key, bool expanded)
    {
        if (_settings.Current.PanelSections.TryGetValue(key, out var current) && current == expanded)
        {
            return;
        }

        _settings.Current = _settings.Current with { PanelSections = new PanelSectionMap(_settings.Current.PanelSections) { [key] = expanded } };
        _ = _settings.SaveAsync();
    }
}
