using System.IO;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Temporäre PNG-Dateien für „Bild aus dem Editor ziehen“ (liegen unter <c>%TEMP%\GodSaveTheScreen</c>).</summary>
public static class DragTempFiles
{
    /// <summary>Ältere Dateien werden beim Start gelöscht; das Ziel eines Ziehvorgangs kopiert spätestens dann längst.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    public static string Directory { get; } = Path.Combine(Path.GetTempPath(), "GodSaveTheScreen");

    /// <summary>Löscht Dateien in <paramref name="directory"/>, die vor mehr als <paramref name="maxAge"/> zuletzt geändert wurden. Gibt die Anzahl gelöschter Dateien zurück.</summary>
    public static int Cleanup(string directory, TimeSpan maxAge, DateTime now, ILogger? logger = null)
    {
        var deleted = 0;
        try
        {
            if (!System.IO.Directory.Exists(directory))
            {
                return 0;
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.png"))
            {
                try
                {
                    if (now - File.GetLastWriteTime(file) > maxAge)
                    {
                        File.Delete(file);
                        deleted++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // In Benutzung oder gesperrt: beim nächsten Start erneut versuchen.
                    logger?.LogDebug(ex, "Temporäre Datei {File} konnte nicht gelöscht werden", file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Temporärer Ordner {Directory} konnte nicht aufgeräumt werden", directory);
        }

        return deleted;
    }
}
