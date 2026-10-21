using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Emoji;

namespace GodSaveTheScreen.Core.Settings;

public enum ThemeSetting
{
    System,
    Light,
    Dark,
}

public enum LogLevelSetting
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
}

/// <summary>Quelle des Ergebnisbilds bei der Fensteraufnahme.</summary>
public enum WindowSourceSetting
{
    /// <summary>Ausschnitt aus dem eingefrorenen Bild – exakt das, was sichtbar war.</summary>
    Snapshot,

    /// <summary><c>PrintWindow</c>: erfasst auch verdeckte Teile (Fallback auf den Snapshot).</summary>
    WindowContent,
}

/// <summary>Aktion bei Klick auf das Tray-Symbol.</summary>
public enum TrayClickActionSetting
{
    CaptureRectangle,
    CaptureWindow,
    CaptureFreeform,
    CaptureFullScreen,
    ShowStartWindow,
    None,
}

public enum ImageFormatSetting
{
    Png,
    Jpeg,
    Bmp,
    Gif,
    Tiff,
    JpegXr,
}

public enum TiffCompressionSetting
{
    None,
    Lzw,
    Zip,
    Rle,
}

/// <summary>Persistierte Anwendungseinstellungen. Fehlende Felder in der Datei behalten ihre Standardwerte.</summary>
public sealed record AppSettings
{
    public const int MaxRecentEmojis = 30;

    public const string DefaultFileNamePattern = "Screenshot {date:yyyy-MM-dd} {time:HH-mm-ss}";

    // Allgemein

    /// <summary><c>System</c>, <c>de</c> oder <c>en</c>.</summary>
    public string Language { get; init; } = "System";

    public ThemeSetting Theme { get; init; } = ThemeSetting.System;

    public LogLevelSetting LogLevel { get; init; } = LogLevelSetting.Information;

    public bool Autostart { get; init; }

    /// <summary>Beim Start nur im Infobereich laufen (kein Startfenster).</summary>
    public bool StartInTray { get; init; }

    public TrayClickActionSetting TrayClickAction { get; init; } = TrayClickActionSetting.CaptureRectangle;

    // Aufnahme

    /// <summary>Mauszeiger in die Aufnahme einzeichnen.</summary>
    public bool CaptureCursor { get; init; }

    public CaptureMode DefaultMode { get; init; } = CaptureMode.Rectangle;

    /// <summary>Standard-Timer in Sekunden (0 = kein Timer).</summary>
    public int DefaultTimerSeconds { get; init; }

    /// <summary>Zuletzt eingegebener benutzerdefinierter Timer in Sekunden.</summary>
    public int CustomTimerSeconds { get; init; } = 7;

    public WindowSourceSetting WindowSource { get; init; } = WindowSourceSetting.Snapshot;

    /// <summary>Abgerundete Fensterecken (Windows 11) transparent machen.</summary>
    public bool RoundedCornersTransparent { get; init; } = true;

    /// <summary>Eigene Fenster (Start, Editor, Countdown …) von Aufnahmen ausschließen.</summary>
    public bool ExcludeOwnWindows { get; init; } = true;

    public bool ShowMagnifier { get; init; } = true;

    public bool CountdownSound { get; init; }

    /// <summary>Kantenglättung der Freiform-Auswahl.</summary>
    public bool FreeformAntiAlias { get; init; } = true;

    // Nach der Aufnahme

    public bool CopyToClipboard { get; init; }

    public bool AutoSave { get; init; }

    /// <summary>Zielordner für Auto-Speichern; leer = Standardordner.</summary>
    public string AutoSaveDirectory { get; init; } = string.Empty;

    public string FileNamePattern { get; init; } = DefaultFileNamePattern;

    public ImageFormatSetting AutoSaveFormat { get; init; } = ImageFormatSetting.Png;

    // Speichern (Editor)

    public int JpegQuality { get; init; } = 90;

    public bool PngInterlace { get; init; }

    public TiffCompressionSetting TiffCompression { get; init; } = TiffCompressionSetting.Lzw;

    public int JxrQuality { get; init; } = 90;

    public bool JxrLossless { get; init; }

    /// <summary>Hintergrundfarbe, auf die Transparenz bei Formaten ohne Alpha (JPEG, BMP) verrechnet wird.</summary>
    public ArgbColor FlattenBackground { get; init; } = ArgbColor.White;

    public ImageFormatSetting LastSaveFormat { get; init; } = ImageFormatSetting.Png;

    /// <summary>Zuletzt im Editor verwendeter Speicherordner; leer = Standardordner.</summary>
    public string LastSaveDirectory { get; init; } = string.Empty;

    // Werkzeuge

    /// <summary>Nach dem Zeichnen eines Objekts zum Auswahlwerkzeug wechseln.</summary>
    public bool SwitchToSelectAfterDraw { get; init; }

    /// <summary>Textmarker hinter Formen und Text zeichnen.</summary>
    public bool HighlighterBehind { get; init; } = true;

    /// <summary>Standardwerte der Formwerkzeuge (Schlüssel = Werkzeug-ID).</summary>
    public ShapeDefaultsMap ShapeDefaults { get; init; } = [];

    public TextStyle TextDefaults { get; init; } = new();

    public MarkerStyle MarkerDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Unschärfe-Werkzeugs.</summary>
    public BlurStyle BlurDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Lupen-Werkzeugs.</summary>
    public MagnifierStyle MagnifierDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration von Stift und Textmarker (Glättung, Druck).</summary>
    public FreehandStyle FreehandDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration von Bereichsauswahl und Hintergrundradierer.</summary>
    public MaskToolStyle MaskToolDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Emoji-Werkzeugs (Deckkraft, Standardgröße, Hautton).</summary>
    public EmojiStyle EmojiDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Stempelwerkzeugs.</summary>
    public StampStyle StampDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Sprechblasen-Werkzeugs.</summary>
    public CalloutStyle CalloutDefaults { get; init; } = new();

    /// <summary>Letzte Konfiguration des Bild-Werkzeugs (Schatten, Kontur, Deckkraft neuer Bilder).</summary>
    public ImageStyle ImageDefaults { get; init; } = new();

    /// <summary>Zuletzt verwendete Emojis (Sequenz), neueste zuerst, höchstens <see cref="MaxRecentEmojis"/>.</summary>
    public StringList EmojiRecent { get; init; } = [];

    /// <summary>Favorisierte Emojis (Sequenz) in der Reihenfolge des Hinzufügens.</summary>
    public StringList EmojiFavorites { get; init; } = [];

    /// <summary>Benutzer-Vorlagen je Werkzeug (Schlüssel = Werkzeug-ID).</summary>
    public ToolPresetMap ToolPresets { get; init; } = [];

    /// <summary>Die mitgelieferten Vorlagen wurden einmalig angelegt.</summary>
    public bool PresetsSeeded { get; init; }

    /// <summary>Nummer des ersten Markers in einem Bild.</summary>
    public int MarkerStartNumber { get; init; } = 1;

    /// <summary>Zuletzt verwendete Schriften (Familienname), neueste zuerst.</summary>
    public StringList RecentFonts { get; init; } = [];

    /// <summary>Zuletzt verwendete Farben (<c>#AARRGGBB</c>), neueste zuerst.</summary>
    public StringList RecentColors { get; init; } = [];

    /// <summary>Der einmalige Erststart-Hinweis wurde angezeigt.</summary>
    public bool FirstRunHintShown { get; init; }

    // Globale Hotkeys (Format siehe HotkeyGesture; leer = nicht belegt)

    public string HotkeyRectangle { get; init; } = "Ctrl+Shift+1";

    public string HotkeyWindow { get; init; } = "Ctrl+Shift+2";

    public string HotkeyFreeform { get; init; } = "Ctrl+Shift+3";

    public string HotkeyFullScreen { get; init; } = "Ctrl+Shift+4";

    /// <summary>
    /// Ersetzt Werte, die beim Lesen einer älteren Datei als <c>null</c> ankommen (der JSON-Deserialisierer
    /// übernimmt die Standardwerte fehlender Felder nicht in jedem Fall), durch Standardwerte.
    /// </summary>
    public AppSettings Normalized()
    {
        var shapes = new ShapeDefaultsMap();
        foreach (var (key, style) in ShapeDefaults ?? [])
        {
            shapes[key] = style is null ? new ShapeStyle() : style with { Stroke = style.Stroke ?? StrokeStyle.Default, Shadow = style.Shadow ?? ShadowStyle.None };
        }

        var text = TextDefaults ?? new TextStyle();
        var marker = MarkerDefaults ?? new MarkerStyle();
        var blur = BlurDefaults ?? new BlurStyle();
        var magnifier = MagnifierDefaults ?? new MagnifierStyle();
        var freehand = FreehandDefaults ?? new FreehandStyle();
        var emoji = EmojiDefaults ?? new EmojiStyle();
        var stamp = StampDefaults ?? new StampStyle();
        var callout = CalloutDefaults ?? new CalloutStyle();
        var presets = new ToolPresetMap();
        foreach (var (key, list) in ToolPresets ?? [])
        {
            presets[key] = new ToolPresetList((list ?? []).Where(p => p is not null && PresetNames.IsValid(p.Name)));
        }

        return this with
        {
            ToolPresets = presets,
            BlurDefaults = blur,
            FreehandDefaults = freehand,
            MaskToolDefaults = MaskToolDefaults ?? new MaskToolStyle(),
            MagnifierDefaults = magnifier with { Border = magnifier.Border ?? new MagnifierStyle().Border },
            ShapeDefaults = shapes,
            EmojiDefaults = emoji with { DefaultSize = emoji.DefaultSize > 0 ? emoji.DefaultSize : 96, Shadow = emoji.Shadow ?? ShadowStyle.None },
            StampDefaults = stamp with
            {
                Text = stamp.Text ?? string.Empty,
                FontFamilyName = stamp.FontFamilyName ?? new StampStyle().FontFamilyName,
                FontSize = stamp.FontSize > 0 ? stamp.FontSize : new StampStyle().FontSize,
            },
            CalloutDefaults = callout.Normalized(),
            ImageDefaults = (ImageDefaults ?? new ImageStyle()).Normalized(),
            EmojiRecent = EmojiRecent ?? [],
            EmojiFavorites = EmojiFavorites ?? [],
            RecentColors = RecentColors ?? [],
            RecentFonts = RecentFonts ?? [],
            TextDefaults = text with { Stroke = text.Stroke ?? new StrokeStyle(ArgbColor.White, 0), FontFamilyName = text.FontFamilyName ?? "Segoe UI", Shadow = text.Shadow ?? ShadowStyle.None },
            MarkerDefaults = marker with { Stroke = marker.Stroke ?? new StrokeStyle(ArgbColor.White, 2), FontFamilyName = marker.FontFamilyName ?? "Segoe UI", Shadow = marker.Shadow ?? ShadowStyle.None },
            FileNamePattern = FileNamePattern ?? DefaultFileNamePattern,
            AutoSaveDirectory = AutoSaveDirectory ?? string.Empty,
            LastSaveDirectory = LastSaveDirectory ?? string.Empty,
        };
    }

    public string GetHotkey(CaptureMode mode) => mode switch
    {
        CaptureMode.Rectangle => HotkeyRectangle,
        CaptureMode.Window => HotkeyWindow,
        CaptureMode.Freeform => HotkeyFreeform,
        _ => HotkeyFullScreen,
    };

    public AppSettings WithHotkey(CaptureMode mode, string gesture) => mode switch
    {
        CaptureMode.Rectangle => this with { HotkeyRectangle = gesture },
        CaptureMode.Window => this with { HotkeyWindow = gesture },
        CaptureMode.Freeform => this with { HotkeyFreeform = gesture },
        _ => this with { HotkeyFullScreen = gesture },
    };
}
