using System.Text.Json.Serialization;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Project;

/// <summary>Ergebnis des Öffnens: das Dokument (Undo-Verlauf leer, nicht verändert) und Hinweise (übersprungene Objekte …).</summary>
public sealed record LoadedProject(ImageDocument Document, IReadOnlyList<string> Warnings);

/// <summary>Lesen und Schreiben von <c>.gsts</c>-Projekten (Bildkodierung über <see cref="IImageCodecService"/>).</summary>
public interface IProjectService
{
    /// <summary>Speichert das Dokument atomar als Projekt inklusive Vorschau. Setzt weder Pfad noch Dirty-Status.</summary>
    Task SaveAsync(ImageDocument document, string path, CancellationToken cancellationToken = default);

    /// <summary>Liest ein Projekt.</summary>
    /// <exception cref="ProjectFormatException">Datei beschädigt oder nicht unterstützt.</exception>
    Task<LoadedProject> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Nur die Vorschau (<c>preview.png</c>) für Listen; <c>null</c>, wenn sie fehlt oder die Datei defekt ist.</summary>
    Task<byte[]?> ReadPreviewAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Momentaufnahme für Hintergrundsicherungen. Muss auf dem UI-Thread laufen (kopiert die Objekte);
    /// das anschließende <see cref="WriteAsync"/> darf auf einem anderen Thread laufen.
    /// </summary>
    ProjectContent Snapshot(ImageDocument document, byte[] basePng);

    /// <summary>Kodiert das Basisbild verlustfrei als PNG (thread-sicher, das Basisbild ist unveränderlich).</summary>
    byte[] EncodeBase(ImageDocument document);

    /// <summary>Schreibt einen Inhalt atomar (temporäre Datei, dann Ersetzen).</summary>
    Task WriteAsync(ProjectContent content, string path, CancellationToken cancellationToken = default);
}

public enum RecentFileKind
{
    Image,
    Project,
}

/// <summary>Eintrag der Liste „Zuletzt verwendet“.</summary>
public sealed record RecentFile(string Path, RecentFileKind Kind, DateTimeOffset LastUsed)
{
    [JsonIgnore]
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Die Datei existiert nicht mehr (Anzeige ausgegraut, entfernbar).</summary>
    [JsonIgnore]
    public bool IsMissing => !File.Exists(Path);
}

/// <summary>Zuletzt geöffnete bzw. gespeicherte Bilder und Projekte (höchstens <see cref="MaxEntries"/>).</summary>
public interface IRecentFilesService
{
    public const int MaxEntries = 15;

    IReadOnlyList<RecentFile> Items { get; }

    event EventHandler? Changed;

    /// <summary>Fügt die Datei oben ein (bereits vorhandene wandert nach oben).</summary>
    void Add(string path);

    void Remove(string path);

    void Clear();
}
