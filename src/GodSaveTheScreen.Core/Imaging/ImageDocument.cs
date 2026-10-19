using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Dokument: Basisbild, Annotationen (Z-Reihenfolge = Listenreihenfolge, zuletzt = oben) und Undo-Verlauf.</summary>
public sealed partial class ImageDocument : ObservableObject
{
    public ImageDocument(PixelBuffer baseImage, double dpiX = 96, double dpiY = 96)
    {
        BaseImage = baseImage ?? throw new ArgumentNullException(nameof(baseImage));
        DpiX = dpiX;
        DpiY = dpiY;
        CreatedAt = DateTimeOffset.Now;
        Undo = new UndoManager();
        Undo.Changed += (_, _) => IsDirty = !Undo.IsAtSavePoint;
    }

    public ObservableCollection<Annotation> Annotations { get; } = [];

    public UndoManager Undo { get; }

    /// <summary>Markiert den aktuellen Stand als gespeichert.</summary>
    public void MarkSaved()
    {
        Undo.MarkSaved();
        IsDirty = false;
    }

    public PixelBuffer BaseImage { get; }

    public double DpiX { get; }

    public double DpiY { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Textmarker-Striche hinter allen übrigen Objekten zeichnen (Anzeige und Export).</summary>
    [ObservableProperty]
    private bool _highlightersBehind = true;

    [ObservableProperty]
    private string? _sourcePath;

    /// <summary>Nicht-destruktiver Zuschnitt in Pixeln des Basisbilds; <c>null</c> = kein Zuschnitt. Nur über <c>CropAction</c> ändern.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveBounds))]
    private PixelRect? _cropRect;

    /// <summary>Sichtbarer Bereich: der Zuschnitt oder die volle Bildfläche.</summary>
    public PixelRect EffectiveBounds => CropRect ?? BaseImage.Bounds;

    /// <summary>Stabile Kennung (Dateiname der automatischen Sicherung).</summary>
    public Guid DocumentId { get; init; } = Guid.NewGuid();

    /// <summary>Pfad des Projekts (<c>.gsts</c>), falls das Dokument daraus stammt oder dorthin gespeichert wurde.</summary>
    [ObservableProperty]
    private string? _projectPath;

    /// <summary>Metadaten der Aufnahme (Modus, Fenstertitel) für das Projektformat.</summary>
    public DocumentMetadata Metadata { get; set; } = new();

    [ObservableProperty]
    private bool _isDirty;
}

/// <summary>Herkunft einer Aufnahme (wird im Projekt gespeichert).</summary>
public sealed record DocumentMetadata(string? CaptureMode = null, string? WindowTitle = null);
