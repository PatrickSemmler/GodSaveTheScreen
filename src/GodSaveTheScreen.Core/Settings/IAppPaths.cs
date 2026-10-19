namespace GodSaveTheScreen.Core.Settings;

public interface IAppPaths
{
    bool IsPortable { get; }

    string DataDirectory { get; }

    string LogDirectory { get; }

    string SettingsFilePath { get; }
}
