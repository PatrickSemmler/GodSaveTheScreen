using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Capture;

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

    /// <summary>Nummer des ersten Markers in einem Bild.</summary>
    public int MarkerStartNumber { get; init; } = 1;

    /// <summary>Zuletzt verwendete Schriften (Familienname), neueste zuerst.</summary>
    public StringList RecentFonts { get; init; } = [];

    /// <summary>Zuletzt verwendete Farben (<c>#AARRGGBB</c>), neueste zuerst.</summary>
    public StringList RecentColors { get; init; } = [];

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
            shapes[key] = style is null ? new ShapeStyle() : style with { Stroke = style.Stroke ?? StrokeStyle.Default };
        }

        var text = TextDefaults ?? new TextStyle();
        var marker = MarkerDefaults ?? new MarkerStyle();
        return this with
        {
            ShapeDefaults = shapes,
            RecentColors = RecentColors ?? [],
            RecentFonts = RecentFonts ?? [],
            TextDefaults = text with { Stroke = text.Stroke ?? new StrokeStyle(ArgbColor.White, 0), FontFamilyName = text.FontFamilyName ?? "Segoe UI" },
            MarkerDefaults = marker with { Stroke = marker.Stroke ?? new StrokeStyle(ArgbColor.White, 2), FontFamilyName = marker.FontFamilyName ?? "Segoe UI" },
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
