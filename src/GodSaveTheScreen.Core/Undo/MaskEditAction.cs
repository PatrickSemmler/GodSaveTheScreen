using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Undo;

/// <summary>Ändert einen Bereich der Transparenzmaske (speichert nur diesen Bereich vorher/nachher).</summary>
public sealed class MaskEditAction : IUndoableAction
{
    private readonly AlphaMask _mask;
    private readonly PixelRect _region;
    private readonly byte[] _before;
    private readonly byte[] _after;

    public MaskEditAction(AlphaMask mask, PixelRect region, byte[] before, byte[] after, string description)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        _mask = mask;
        _region = region;
        _before = before;
        _after = after;
        Description = description;
    }

    /// <summary>Neue Werte für einen Bereich; die bisherigen werden aus der Maske gelesen.</summary>
    public MaskEditAction(AlphaMask mask, PixelRect region, byte[] after, string description)
        : this(mask, region, mask?.Read(region) ?? throw new ArgumentNullException(nameof(mask)), after, description)
    {
    }

    public string Description { get; }

    public PixelRect Region => _region;

    public void Do() => _mask.Write(_region, _after);

    public void Undo() => _mask.Write(_region, _before);

    public bool TryMerge(IUndoableAction next) => false;
}
