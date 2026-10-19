namespace GodSaveTheScreen.Core.Settings;

/// <summary>
/// Ermittelt Daten- und Logverzeichnis. Liegt <c>portable.txt</c> neben der EXE, wird <c>.\data\</c> verwendet.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string AppFolderName = "GodSaveTheScreen";
    public const string PortableMarkerFileName = "portable.txt";

    public AppPaths(string exeDirectory, string roamingAppData, string localAppData)
    {
        IsPortable = File.Exists(Path.Combine(exeDirectory, PortableMarkerFileName));
        if (IsPortable)
        {
            DataDirectory = Path.Combine(exeDirectory, "data");
            LogDirectory = Path.Combine(DataDirectory, "logs");
        }
        else
        {
            DataDirectory = Path.Combine(roamingAppData, AppFolderName);
            LogDirectory = Path.Combine(localAppData, AppFolderName, "logs");
        }

        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
        RecentFilesPath = Path.Combine(DataDirectory, "recent.json");
        AutosaveDirectory = IsPortable
            ? Path.Combine(DataDirectory, "autosave")
            : Path.Combine(localAppData, AppFolderName, "autosave");
    }

    public bool IsPortable { get; }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string SettingsFilePath { get; }

    public string AutosaveDirectory { get; }

    public string RecentFilesPath { get; }

    public static AppPaths ForCurrentProcess() => new(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
}
