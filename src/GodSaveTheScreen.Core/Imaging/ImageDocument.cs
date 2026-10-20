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
        Mask = new AlphaMask(baseImage.Width, baseImage.Height);
        _canvasBounds = baseImage.Bounds;
        // Erst nach abgeschlossenen Änderungen (Loslassen, Undo/Redo, Laden) anpassen, nicht während des Ziehens.
        Undo.Changed += (_, _) =>
        {
            IsDirty = !Undo.IsAtSavePoint;
            UpdateCanvasBounds();
        };
        Annotations.CollectionChanged += (_, _) => UpdateCanvasBounds();
    }

    /// <summary>Höchstens so viele Pixel darf die Zeichenfläche je Seite über das Basisbild hinauswachsen.</summary>
    public const int MaxCanvasOverhang = 8192;

    public ObservableCollection<Annotation> Annotations { get; } = [];

    public UndoManager Undo { get; }

    /// <summary>Markiert den aktuellen Stand als gespeichert.</summary>
    public void MarkSaved()
    {
        Undo.MarkSaved();
        IsDirty = false;
    }

    public PixelBuffer BaseImage { get; }

    /// <summary>Nicht-destruktiv entfernte Bereiche des Basisbilds; Änderungen über <c>MaskEditAction</c>.</summary>
    public AlphaMask Mask { get; }

    private PixelBuffer? _maskedImage;
    private int _maskedVersion = -1;

    /// <summary>
    /// Basisbild mit angewendeter <see cref="Mask"/> (ohne entfernte Bereiche ist es <see cref="BaseImage"/> selbst).
    /// Grundlage für Anzeige, Export, Unschärfe und Lupe; je Maskenstand ein neuer, danach unveränderter Puffer.
    /// </summary>
    public PixelBuffer MaskedImage
    {
        get
        {
            if (_maskedImage is null || _maskedVersion != Mask.Version)
            {
                _maskedImage = Mask.IsOpaque ? BaseImage : Mask.ApplyTo(BaseImage);
                _maskedVersion = Mask.Version;
            }

            return _maskedImage;
        }
    }

    public double DpiX { get; }

    public double DpiY { get; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Textmarker-Striche hinter allen übrigen Objekten zeichnen (Anzeige und Export).</summary>
    [ObservableProperty]
    private bool _highlightersBehind = true;

    [ObservableProperty]
    private string? _sourcePath;

    /// <summary>Nicht-destruktiver Zuschnitt in Pixeln des Basisbilds; <c>null</c> = kein Zuschnitt. Nur über <c>CropAction</c> ändern.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveBounds))]
    private PixelRect? _cropRect;

    /// <summary>Sichtbarer Bereich: der Zuschnitt oder die (ggf. um überstehende Objekte erweiterte) Zeichenfläche.</summary>
    public PixelRect EffectiveBounds => CropRect ?? CanvasBounds;

    private PixelRect _canvasBounds;

    /// <summary>
    /// Basisbild plus alle sichtbaren Objekte, die darüber hinausragen (der erweiterte Bereich ist transparent).
    /// Kann negative Koordinaten haben; wird nach jedem Undo-Schritt neu berechnet.
    /// </summary>
    public PixelRect CanvasBounds
    {
        get => _canvasBounds;
        private set
        {
            if (SetProperty(ref _canvasBounds, value) && CropRect is null)
            {
                OnPropertyChanged(nameof(EffectiveBounds));
            }
        }
    }

    /// <summary>Berechnet <see cref="CanvasBounds"/> aus Basisbild und sichtbaren Objekten neu.</summary>
    public void UpdateCanvasBounds()
    {
        var image = BaseImage.Bounds;
        double left = image.X, top = image.Y, right = image.Right, bottom = image.Bottom;
        foreach (var annotation in Annotations)
        {
            if (!annotation.IsVisible || annotation.Opacity <= 0)
            {
                continue;
            }

            var bounds = annotation.VisualBounds.Normalize();
            if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height))
            {
                continue;
            }

            left = Math.Min(left, bounds.X);
            top = Math.Min(top, bounds.Y);
            right = Math.Max(right, bounds.Right);
            bottom = Math.Max(bottom, bounds.Bottom);
        }

        CanvasBounds = PixelRect.FromLtrb(
            (int)Math.Floor(Math.Max(left, image.X - MaxCanvasOverhang)),
            (int)Math.Floor(Math.Max(top, image.Y - MaxCanvasOverhang)),
            (int)Math.Ceiling(Math.Min(right, image.Right + MaxCanvasOverhang)),
            (int)Math.Ceiling(Math.Min(bottom, image.Bottom + MaxCanvasOverhang)));
    }

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
