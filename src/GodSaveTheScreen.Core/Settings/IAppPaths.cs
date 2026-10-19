namespace GodSaveTheScreen.Core.Settings;

public interface IAppPaths
{
    bool IsPortable { get; }

    string DataDirectory { get; }

    string LogDirectory { get; }

    string SettingsFilePath { get; }

    /// <summary>Ordner der automatischen Sicherungen (<c>{DocumentId}.gsts</c>).</summary>
    string AutosaveDirectory { get; }

    /// <summary>Datei der zuletzt verwendeten Dateien.</summary>
    string RecentFilesPath { get; }
}
