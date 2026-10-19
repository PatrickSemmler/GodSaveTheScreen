using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Services;

/// <summary>Antwort der Rückfrage „Änderungen speichern?“.</summary>
public enum SaveChangesAnswer
{
    Save,
    Discard,
    Cancel,

    /// <summary>Als bearbeitbares Projekt (<c>.gsts</c>) speichern.</summary>
    SaveAsProject,
}

public interface IDialogService
{
    void ShowError(string title, string message);

    /// <summary>Hinweis mit „OK“.</summary>
    void ShowInfo(string title, string message);

    /// <summary>Fehlermeldung mit zusätzlicher Schaltfläche „Log-Ordner öffnen“ (unbehandelte Ausnahmen).</summary>
    void ShowCrash(string title, string message, string logDirectory);

    /// <summary>Ja/Nein-Rückfrage. Gibt <c>true</c> bei „Ja“ zurück.</summary>
    bool Confirm(string title, string message);

    /// <summary>
    /// Speichern / Nicht speichern / Abbrechen. Mit <paramref name="offerProject"/> zusätzlich „Als Projekt speichern“
    /// (Dokument enthält Objekte, die in einem Bildformat nicht mehr bearbeitbar wären).
    /// </summary>
    SaveChangesAnswer AskSaveChanges(string title, string message, bool offerProject = false);

    /// <summary>Wiederherstellen (<c>true</c>) oder Verwerfen (<c>false</c>) nicht gespeicherter Dokumente nach einem Absturz.</summary>
    bool AskRecovery(string title, string message);

    /// <summary>Fragt nach einem Ordner. Gibt <c>null</c> bei Abbruch zurück.</summary>
    string? PickFolder(string? initialDirectory);

    /// <summary>Fragt nach einem Zielpfad. Gibt <c>null</c> bei Abbruch zurück.</summary>
    string? PickSavePath(string suggestedFileName, string filter);

    /// <summary>Speichern-Dialog mit allen Formaten; liefert Pfad und gewähltes Format oder <c>null</c> bei Abbruch.</summary>
    (string Path, Settings.ImageFormatSetting Format)? PickImageSavePath(string suggestedName, string? initialDirectory, Settings.ImageFormatSetting format);

    /// <summary>
    /// Speichern-Dialog mit „GodSaveTheScreen-Projekt“ als erstem Format und allen Bildformaten.
    /// <c>Format == null</c> bedeutet Projekt. <c>null</c> bei Abbruch.
    /// </summary>
    (string Path, Settings.ImageFormatSetting? Format)? PickDocumentSavePath(string suggestedName, string? initialDirectory, Settings.ImageFormatSetting format, bool preferProject);

    /// <summary>Öffnen-Dialog für Bilder und Projekte (mehrfach); leer bei Abbruch.</summary>
    IReadOnlyList<string> PickImageFiles();
}

public interface IClipboardService
{
    void SetImage(PixelBuffer image);

    /// <summary>Bild aus <c>PNG</c>/<c>CF_DIB</c>/Dateiliste; <c>null</c>, wenn keines vorhanden ist.</summary>
    PixelBuffer? GetImage();

    bool ContainsImage();

    /// <summary>Objekte im eigenen Format (JSON) plus gerendertem PNG als Rückfall.</summary>
    void SetAnnotations(IReadOnlyList<Annotation> annotations, PixelBuffer? renderedFallback);

    /// <summary>Objekte aus dem eigenen Zwischenablageformat; leer, wenn keine vorhanden sind.</summary>
    IReadOnlyList<Annotation> GetAnnotations();
}
