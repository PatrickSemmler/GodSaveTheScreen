using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Bearbeitungen der <see cref="AlphaMask"/>, die als ein Undo-Schritt übernommen werden.</summary>
public static class MaskEditing
{
    /// <summary>
    /// Entfernt die (weich gezeichnete) Auswahl aus der Maske, ohne sie zu ändern: liefert Bereich und neue Werte;
    /// <c>null</c>, wenn sich nichts ändern würde.
    /// </summary>
    public static (PixelRect Region, byte[] Values)? Remove(AlphaMask mask, AreaSelection selection, double feather)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(selection);
        var (box, coverage) = selection.Feathered(feather);
        if (box.IsEmpty)
        {
            return null;
        }

        var values = mask.Read(box);
        var changed = false;
        for (var i = 0; i < values.Length; i++)
        {
            var c = coverage[i];
            if (c == 0)
            {
                continue;
            }

            var next = (byte)((values[i] * (255 - c)) / 255);
            changed |= next != values[i];
            values[i] = next;
        }

        return changed ? (box, values) : null;
    }
}

/// <summary>
/// Freihand-Pinsel auf der Maske (Entfernen oder Wiederherstellen) mit Größe und Härte. Ändert die Maske sofort
/// (Live-Anzeige); <see cref="End"/> liefert Vorher/Nachher für den Undo-Schritt, <see cref="Cancel"/> stellt zurück.
/// Innerhalb eines Strichs addieren sich überlappende Tupfer nicht (Maximum der Deckung).
/// </summary>
public sealed class MaskBrush
{
    public const double MinSize = 1;
    public const double MaxSize = 500;

    private readonly AlphaMask _mask;
    private readonly byte[] _original;
    private readonly byte[] _stroke;
    private readonly double _radius;
    private readonly double _hardness;
    private readonly bool _restore;
    private PixelRect _dirty = PixelRect.Empty;
    private PointD? _last;

    /// <param name="mask">Maske des Dokuments.</param>
    /// <param name="size">Durchmesser in Bildpixeln.</param>
    /// <param name="hardness">0–1: Anteil des Radius mit voller Deckung (1 = harte Kante).</param>
    /// <param name="restore">Wiederherstellen statt Entfernen.</param>
    public MaskBrush(AlphaMask mask, double size, double hardness, bool restore)
    {
        ArgumentNullException.ThrowIfNull(mask);
        _mask = mask;
        _original = (byte[])mask.Values.Clone();
        _stroke = new byte[mask.Values.Length];
        _radius = Math.Clamp(size, MinSize, MaxSize) / 2;
        _hardness = Math.Clamp(hardness, 0, 1);
        _restore = restore;
    }

    public bool IsRestore => _restore;

    /// <summary>Bisher veränderter Bereich.</summary>
    public PixelRect Dirty => _dirty;

    /// <summary>Malt bis <paramref name="position"/> (erster Aufruf: ein Tupfer); Tupferabstand = ¼ Radius.</summary>
    public void LineTo(PointD position)
    {
        var from = _last ?? position;
        var distance = from.DistanceTo(position);
        var step = Math.Max(_radius / 4, 0.5);
        var count = _last is null ? 0 : Math.Max(1, (int)Math.Ceiling(distance / step));
        var changed = PixelRect.Empty;
        for (var i = _last is null ? 0 : 1; i <= count; i++)
        {
            var t = count == 0 ? 1 : (double)i / count;
            var p = new PointD(from.X + ((position.X - from.X) * t), from.Y + ((position.Y - from.Y) * t));
            changed = Union(changed, Dab(p));
        }

        _last = position;
        if (!changed.IsEmpty)
        {
            _dirty = Union(_dirty, changed);
            _mask.NotifyChanged(changed);
        }
    }

    /// <summary>Beendet den Strich: Bereich mit Werten vorher/nachher; <c>null</c>, wenn sich nichts geändert hat.</summary>
    public (PixelRect Region, byte[] Before, byte[] After)? End()
    {
        if (_dirty.IsEmpty)
        {
            return null;
        }

        var before = new byte[_dirty.Width * _dirty.Height];
        for (var row = 0; row < _dirty.Height; row++)
        {
            Buffer.BlockCopy(_original, ((_dirty.Y + row) * _mask.Width) + _dirty.X, before, row * _dirty.Width, _dirty.Width);
        }

        var after = _mask.Read(_dirty);
        return before.AsSpan().SequenceEqual(after) ? null : (_dirty, before, after);
    }

    /// <summary>Verwirft den Strich (Maske wie vorher).</summary>
    public void Cancel()
    {
        if (_dirty.IsEmpty)
        {
            return;
        }

        for (var row = 0; row < _dirty.Height; row++)
        {
            var offset = ((_dirty.Y + row) * _mask.Width) + _dirty.X;
            Buffer.BlockCopy(_original, offset, _mask.Values, offset, _dirty.Width);
        }

        _mask.NotifyChanged(_dirty);
        _dirty = PixelRect.Empty;
    }

    /// <summary>Deckung eines Tupfers im Abstand <paramref name="distance"/> vom Mittelpunkt (0–1).</summary>
    public static double Falloff(double distance, double radius, double hardness)
    {
        if (distance >= radius)
        {
            return 0;
        }

        var inner = radius * hardness;
        if (distance <= inner)
        {
            return 1;
        }

        // Weicher Übergang (Smoothstep) zwischen innerem Kreis und Rand.
        var t = 1 - ((distance - inner) / (radius - inner));
        return t * t * (3 - (2 * t));
    }

    private PixelRect Dab(PointD center)
    {
        // Harte Pinsel bekommen einen Halbpixel-Saum gegen Treppenstufen.
        var reach = _radius + 0.5;
        var box = PixelRect.FromLtrb(
            (int)Math.Floor(center.X - reach), (int)Math.Floor(center.Y - reach),
            (int)Math.Ceiling(center.X + reach), (int)Math.Ceiling(center.Y + reach)).Intersect(_mask.Bounds);
        if (box.IsEmpty)
        {
            return PixelRect.Empty;
        }

        var values = _mask.Values;
        for (var y = box.Y; y < box.Bottom; y++)
        {
            var dy = y + 0.5 - center.Y;
            var row = y * _mask.Width;
            for (var x = box.X; x < box.Right; x++)
            {
                var dx = x + 0.5 - center.X;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var edge = Math.Clamp(_radius + 0.5 - distance, 0, 1);
                var cover = (byte)Math.Round(Math.Min(Falloff(distance, _radius + 0.5, _hardness), edge) * 255);
                var index = row + x;
                if (cover <= _stroke[index])
                {
                    continue;
                }

                _stroke[index] = cover;
                var original = _original[index];
                values[index] = _restore
                    ? (byte)(original + (((255 - original) * cover) / 255))
                    : (byte)((original * (255 - cover)) / 255);
            }
        }

        return box;
    }

    private static PixelRect Union(PixelRect a, PixelRect b) =>
        a.IsEmpty ? b : b.IsEmpty ? a : PixelRect.FromLtrb(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));
}
