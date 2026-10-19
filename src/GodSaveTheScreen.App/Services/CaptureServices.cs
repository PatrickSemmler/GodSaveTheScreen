using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Blendet beim Aufnehmen alle sichtbaren eigenen Fenster aus und zeigt sie danach wieder.</summary>
public interface IWindowVisibilityService
{
    /// <summary>Blendet aus; <c>Dispose</c> blendet die zuvor sichtbaren Fenster wieder ein.</summary>
    IDisposable HideAll();
}

/// <summary>Countdown vor der Aufnahme.</summary>
public interface ICountdownService
{
    /// <summary>Zählt herunter. <c>true</c> = abgelaufen, <c>false</c> = abgebrochen (Esc, Klick oder Token).</summary>
    Task<bool> RunAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>Interaktive Auswahl auf dem eingefrorenen Desktop.</summary>
public interface ISelectionService
{
    /// <summary>Liefert die Auswahl oder <c>null</c> bei Abbruch.</summary>
    Task<SelectionResult?> SelectAsync(ScreenSnapshot snapshot, CaptureMode mode, CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis einer Aufnahme, auf dem die Nachaufnahme-Aktionen arbeiten.</summary>
public sealed record CaptureResultContext(ImageDocument Document, CaptureMode Mode, string? WindowTitle, DateTimeOffset Timestamp);

/// <summary>Aktion nach der Aufnahme (Zwischenablage, Auto-Speichern, Editor). Fehler stoppen andere Aktionen nicht.</summary>
public interface IPostCaptureAction
{
    Task ExecuteAsync(CaptureResultContext context);
}

/// <summary>Benachrichtigungen im Infobereich.</summary>
public interface INotificationService
{
    /// <summary>„Gespeichert unter …“; Klick öffnet den Ordner.</summary>
    void ShowSaved(string path);
}

/// <summary>Oberfläche der Anwendung (Fenster öffnen, beenden).</summary>
public interface IAppShell
{
    void ShowStartWindow();

    void ShowSettings();

    /// <summary>Beendet die Anwendung; fragt bei ungespeicherten Editoren nach.</summary>
    void ExitApplication();
}
