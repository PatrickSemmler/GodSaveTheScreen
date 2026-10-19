using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Annotations;
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

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private bool _isDirty;
}
