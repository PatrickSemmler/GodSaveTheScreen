using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Undo;

/// <summary>Ändert den Zuschnitt des Dokuments (nicht-destruktiv, rückgängig machbar).</summary>
public sealed class CropAction : IUndoableAction
{
    private readonly ImageDocument _document;
    private readonly PixelRect? _old;
    private readonly PixelRect? _new;

    /// <param name="document">Dokument.</param>
    /// <param name="newRect">Neuer Zuschnitt; wird an das Basisbild geklemmt. Die volle Bildfläche bedeutet „kein Zuschnitt“.</param>
    public CropAction(ImageDocument document, PixelRect? newRect, string description = "Zuschneiden")
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _old = document.CropRect;
        _new = Normalize(document.BaseImage.Bounds, newRect);
        Description = description;
    }

    public string Description { get; }

    /// <summary>Der Zuschnitt bliebe unverändert.</summary>
    public bool IsNoOp => _old == _new;

    public void Do() => _document.CropRect = _new;

    public void Undo() => _document.CropRect = _old;

    public bool TryMerge(IUndoableAction next) => false;

    /// <summary>Klemmt an die Bildgrenzen; leer oder volle Fläche → <c>null</c>.</summary>
    public static PixelRect? Normalize(PixelRect bounds, PixelRect? rect)
    {
        if (rect is not { } r)
        {
            return null;
        }

        var clamped = r.Intersect(bounds);
        if (clamped.IsEmpty)
        {
            throw new ArgumentException("Der Zuschnitt liegt außerhalb des Bilds.", nameof(rect));
        }

        return clamped == bounds ? null : clamped;
    }
}
