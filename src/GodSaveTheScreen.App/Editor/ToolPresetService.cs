using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>
/// Benutzer-Vorlagen je Werkzeug (in den Einstellungen gespeichert). Namen sind je Werkzeug eindeutig;
/// der Vergleich ignoriert Groß-/Kleinschreibung.
/// </summary>
public sealed class ToolPresetService
{
    private readonly ISettingsService _settings;

    public ToolPresetService(ISettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    /// <summary>Vorlagen des Werkzeugs, alphabetisch sortiert.</summary>
    public IReadOnlyList<ToolPreset> GetPresets(string toolId)
    {
        EnsureSeeded();
        return [.. Read(toolId).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public ToolPreset? Find(string toolId, string name)
    {
        EnsureSeeded();
        return Read(toolId).FirstOrDefault(p => PresetNames.AreSame(p.Name, name));
    }

    public bool Exists(string toolId, string name) => Find(toolId, name) is not null;

    /// <summary>
    /// Speichert die Vorlage; eine gleichnamige (ohne Beachtung der Groß-/Kleinschreibung) wird ersetzt und
    /// übernimmt die neu eingegebene Schreibweise. Gibt <c>false</c> bei ungültigem Namen zurück.
    /// </summary>
    public bool Save(string toolId, ToolPreset preset)
    {
        var name = PresetNames.Clean(preset.Name);
        if (!PresetNames.IsValid(name))
        {
            return false;
        }

        EnsureSeeded();
        var list = Read(toolId);
        list.RemoveAll(p => PresetNames.AreSame(p.Name, name));
        list.Add(preset with { Name = name });
        Write(toolId, list);
        return true;
    }

    /// <summary>Benennt um; trifft der neue Name eine andere Vorlage, wird diese ersetzt.</summary>
    public bool Rename(string toolId, string oldName, string newName)
    {
        var name = PresetNames.Clean(newName);
        if (!PresetNames.IsValid(name))
        {
            return false;
        }

        EnsureSeeded();
        var list = Read(toolId);
        var existing = list.FirstOrDefault(p => PresetNames.AreSame(p.Name, oldName));
        if (existing is null)
        {
            return false;
        }

        list.RemoveAll(p => PresetNames.AreSame(p.Name, oldName) || PresetNames.AreSame(p.Name, name));
        list.Add(existing with { Name = name });
        Write(toolId, list);
        return true;
    }

    public bool Delete(string toolId, string name)
    {
        EnsureSeeded();
        var list = Read(toolId);
        if (list.RemoveAll(p => PresetNames.AreSame(p.Name, name)) == 0)
        {
            return false;
        }

        Write(toolId, list);
        return true;
    }

    /// <summary>Legt fehlende mitgelieferte Vorlagen des Werkzeugs wieder an; vorhandene (auch geänderte) bleiben unberührt.</summary>
    public void RestoreDefaults(string toolId)
    {
        EnsureSeeded();
        var list = Read(toolId);
        var added = false;
        foreach (var preset in DefaultPresets(toolId))
        {
            if (!list.Any(p => PresetNames.AreSame(p.Name, preset.Name)))
            {
                list.Add(preset);
                added = true;
            }
        }

        if (added)
        {
            Write(toolId, list);
        }
    }

    public static bool HasDefaults(string toolId) => DefaultPresets(toolId).Count > 0;

    /// <summary>Mitgelieferte Vorlagen eines Werkzeugs (derzeit nur Text).</summary>
    public static IReadOnlyList<ToolPreset> DefaultPresets(string toolId)
    {
        if (toolId != TextTool.ToolId)
        {
            return [];
        }

        return
        [
            new ToolPreset { Name = Strings.Preset_Label, Text = new TextStyle { FontWeight = 600, FontSize = 28 } },
            new ToolPreset
            {
                Name = Strings.Preset_Warning,
                Text = new TextStyle
                {
                    FontWeight = 700,
                    FontSize = 32,
                    Fill = ArgbColor.FromRgb(0xE5, 0x39, 0x35),
                    Stroke = new StrokeStyle(ArgbColor.White, 4),
                },
            },
            new ToolPreset
            {
                Name = Strings.Preset_Note,
                Text = new TextStyle
                {
                    FontSize = 24,
                    Fill = ArgbColor.FromRgb(0x20, 0x20, 0x20),
                    Background = ArgbColor.FromRgb(0xFF, 0xF1, 0x76),
                    BackgroundPadding = 8,
                    BackgroundCornerRadius = 6,
                },
            },
            new ToolPreset
            {
                Name = Strings.Preset_Hint,
                Text = new TextStyle
                {
                    FontWeight = 600,
                    FontSize = 24,
                    Fill = ArgbColor.White,
                    Background = ArgbColor.FromRgb(0x19, 0x76, 0xD2),
                    BackgroundPadding = 8,
                    BackgroundCornerRadius = 6,
                },
            },
        ];
    }

    /// <summary>Legt die mitgelieferten Vorlagen beim ersten Zugriff einmalig an.</summary>
    private void EnsureSeeded()
    {
        if (_settings.Current.PresetsSeeded)
        {
            return;
        }

        var map = new ToolPresetMap(_settings.Current.ToolPresets);
        foreach (var toolId in new[] { TextTool.ToolId })
        {
            var list = map.TryGetValue(toolId, out var existing) ? new ToolPresetList(existing) : [];
            foreach (var preset in DefaultPresets(toolId))
            {
                if (!list.Any(p => PresetNames.AreSame(p.Name, preset.Name)))
                {
                    list.Add(preset);
                }
            }

            map[toolId] = list;
        }

        _settings.Current = _settings.Current with { ToolPresets = map, PresetsSeeded = true };
        _ = _settings.SaveAsync();
    }

    private ToolPresetList Read(string toolId) =>
        _settings.Current.ToolPresets.TryGetValue(toolId, out var list) ? new ToolPresetList(list) : [];

    private void Write(string toolId, ToolPresetList list)
    {
        var map = new ToolPresetMap(_settings.Current.ToolPresets) { [toolId] = list };
        _settings.Current = _settings.Current with { ToolPresets = map };
        _ = _settings.SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
