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

/// <summary>Persistierte Anwendungseinstellungen. Fehlende Felder in der Datei behalten ihre Standardwerte.</summary>
public sealed record AppSettings
{
    /// <summary><c>System</c>, <c>de</c> oder <c>en</c>.</summary>
    public string Language { get; init; } = "System";

    public ThemeSetting Theme { get; init; } = ThemeSetting.System;

    public LogLevelSetting LogLevel { get; init; } = LogLevelSetting.Information;

    /// <summary>Mauszeiger in die Aufnahme einzeichnen.</summary>
    public bool CaptureCursor { get; init; }
}
