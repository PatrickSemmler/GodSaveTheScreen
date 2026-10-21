using System.Globalization;
using System.Windows.Input;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Localization;
using GodSaveTheScreen.App.Resources;
using CaptureMode = GodSaveTheScreen.Core.Capture.CaptureMode;
using GodSaveTheScreen.Core.Hotkeys;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Eintrag der Tastenkürzel-Übersicht.</summary>
public sealed record ShortcutEntry(string Category, string Name, string Keys);

/// <summary>Stellt die Tastenkürzel-Übersicht (Fenster „Tastenkürzel“, <c>Strg+F1</c>) aus Werkzeugen, Befehlen und Einstellungen zusammen.</summary>
public static class ShortcutCatalog
{
    /// <summary>Befehle des Editors: (Kategorie-Schlüssel, Namens-Schlüssel, Kürzel in invarianter Schreibweise).</summary>
    public static readonly IReadOnlyList<(string CategoryKey, string NameKey, string Keys)> EditorCommands =
    [
        ("Shortcuts_File", "Shortcut_Open", "Ctrl+O"),
        ("Shortcuts_File", "Shortcut_Save", "Ctrl+S"),
        ("Shortcuts_File", "Shortcut_SaveAs", "Ctrl+Shift+S"),
        ("Shortcuts_File", "Shortcut_Paste", "Ctrl+V"),
        ("Shortcuts_File", "Shortcut_PasteAsImage", "Ctrl+Shift+V"),
        ("Shortcuts_Edit", "Shortcut_Undo", "Ctrl+Z"),
        ("Shortcuts_Edit", "Shortcut_Redo", "Ctrl+Y"),
        ("Shortcuts_Edit", "Shortcut_Copy", "Ctrl+C"),
        ("Shortcuts_Edit", "Shortcut_CopyImage", "Ctrl+Shift+C"),
        ("Shortcuts_Edit", "Shortcut_Cut", "Ctrl+X"),
        ("Shortcuts_Edit", "Shortcut_Duplicate", "Ctrl+D"),
        ("Shortcuts_Edit", "Shortcut_Delete", "Del"),
        ("Shortcuts_Edit", "Shortcut_SelectAll", "Ctrl+A"),
        ("Shortcuts_Edit", "Shortcut_BringToFront", "Ctrl+Shift+]"),
        ("Shortcuts_Edit", "Shortcut_BringForward", "Ctrl+]"),
        ("Shortcuts_Edit", "Shortcut_SendBackward", "Ctrl+["),
        ("Shortcuts_Edit", "Shortcut_SendToBack", "Ctrl+Shift+["),
        ("Shortcuts_View", "Shortcut_ZoomIn", "Ctrl++"),
        ("Shortcuts_View", "Shortcut_ZoomOut", "Ctrl+-"),
        ("Shortcuts_View", "Shortcut_Fit", "Ctrl+0"),
        ("Shortcuts_View", "Shortcut_ActualSize", "Ctrl+1"),
        ("Shortcuts_View", "Shortcut_Pan", "Space"),
        ("Shortcuts_View", "Shortcut_Ruler", "Ctrl+R"),
        ("Shortcuts_View", "Shortcut_Help", "F1"),
        ("Shortcuts_View", "Shortcut_ContextHelp", "Shift+F1"),
        ("Shortcuts_View", "Shortcut_ShowShortcuts", "Ctrl+F1"),
        ("Shortcuts_Objects", "Shortcut_NextObject", "Tab"),
        ("Shortcuts_Objects", "Shortcut_PreviousObject", "Shift+Tab"),
        ("Shortcuts_Objects", "Shortcut_Nudge", "Arrow"),
        ("Shortcuts_Objects", "Shortcut_Nudge10", "Shift+Arrow"),
        ("Shortcuts_Objects", "Shortcut_Resize", "Alt+Arrow"),
        ("Shortcuts_Objects", "Shortcut_Rotate", "[ / ]"),
        ("Shortcuts_Objects", "Shortcut_FlipHorizontal", "Shift+H"),
        ("Shortcuts_Objects", "Shortcut_FlipVertical", "Shift+V"),
        ("Shortcuts_Area", "Shortcut_AreaAdd", "Shift"),
        ("Shortcuts_Area", "Shortcut_AreaSubtract", "Alt"),
        ("Shortcuts_Area", "Shortcut_RemoveArea", "Del"),
        ("Shortcuts_Area", "Shortcut_InvertArea", "Ctrl+Shift+I"),
        ("Shortcuts_Area", "Shortcut_BrushRestore", "Alt"),
        ("Shortcuts_Area", "Shortcut_BrushSize", "[ / ]"),
        ("Shortcuts_Crop", "Shortcut_CropApply", "Enter"),
        ("Shortcuts_Crop", "Shortcut_CropCancel", "Esc"),
        ("Shortcuts_Crop", "Shortcut_CropNudge", "Arrow"),
        ("Shortcuts_Crop", "Shortcut_CropKeepRatio", "Shift"),
        ("Shortcuts_Crop", "Shortcut_CropNoSnap", "Alt"),
    ];

    /// <summary>Alle Einträge für den Editor: globale Aufnahme-Hotkeys, Werkzeuge, Befehle.</summary>
    public static IReadOnlyList<ShortcutEntry> Build(ToolRegistry? tools, AppSettings settings)
    {
        var entries = new List<ShortcutEntry>();
        var culture = CultureInfo.CurrentUICulture;

        foreach (var (mode, nameKey) in new[]
        {
            (CaptureMode.Rectangle, "Mode_Rectangle"),
            (CaptureMode.Window, "Mode_Window"),
            (CaptureMode.Freeform, "Mode_Freeform"),
            (CaptureMode.FullScreen, "Mode_Fullscreen"),
        })
        {
            if (HotkeyGesture.TryParse(settings.GetHotkey(mode)) is { } gesture)
            {
                entries.Add(new ShortcutEntry(Strings.Shortcuts_Capture, Text(nameKey), gesture.ToDisplayString(culture)));
            }
        }

        if (tools is not null)
        {
            foreach (var tool in tools.PaletteTools.Where(t => t.ShortcutKey != Key.None))
            {
                entries.Add(new ShortcutEntry(Strings.Shortcuts_Tools, StripShortcut(tool.DisplayName), KeyText(tool.ShortcutKey)));
            }
        }

        foreach (var (category, name, keys) in EditorCommands)
        {
            entries.Add(new ShortcutEntry(Text(category), Text(name), ShortcutText.Localize(keys, culture)));
        }

        return entries;
    }

    private static string Text(string key) => Strings.ResourceManager.GetString(key) ?? key;

    private static string KeyText(Key key) => key.ToString();

    /// <summary>Werkzeugnamen enthalten das Kürzel in Klammern („Rechteck (R)“) – in der Übersicht steht es in einer eigenen Spalte.</summary>
    private static string StripShortcut(string name)
    {
        var open = name.LastIndexOf(" (", StringComparison.Ordinal);
        return open > 0 && name.EndsWith(')') ? name[..open] : name;
    }
}
