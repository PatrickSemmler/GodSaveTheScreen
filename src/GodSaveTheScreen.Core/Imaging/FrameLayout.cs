using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>
/// Lage der Teile eines Rahmens in Bildkoordinaten (Pixel des Basisbilds): <see cref="Content"/> ist der Inhalt
/// (<c>EffectiveBounds</c>), <see cref="Border"/> Inhalt plus Rand, <see cref="Output"/> das Ergebnisbild einschließlich Schatten.
/// </summary>
public readonly record struct FrameLayout(PixelRect Content, PixelRect Border, PixelRect Output, int Left, int Top, int Right, int Bottom)
{
    /// <summary>Ohne Rahmen: alle Rechtecke sind der Inhalt.</summary>
    public static FrameLayout Plain(PixelRect content) => new(content, content, content, 0, 0, 0, 0);

    /// <summary>
    /// Berechnet das Layout. Bei abgerissener Kante hat der Rand auf allen Seiten die größte eingestellte Breite.
    /// Der Rand wächst je Seite höchstens um <see cref="ImageDocument.MaxCanvasOverhang"/> über den Inhalt hinaus.
    /// </summary>
    public static FrameLayout Compute(PixelRect content, FrameStyle? style)
    {
        if (style is null)
        {
            return Plain(content);
        }

        var (left, top, right, bottom) = EffectiveBorders(style);
        var border = PixelRect.FromLtrb(content.X - left, content.Y - top, content.Right + right, content.Bottom + bottom);
        var output = border;
        if (style.HasShadow)
        {
            var shadow = style.Shadow.Extend(border.ToRectD());
            output = PixelRect.FromLtrb(
                Math.Min(border.X, (int)Math.Floor(shadow.X)),
                Math.Min(border.Y, (int)Math.Floor(shadow.Y)),
                Math.Max(border.Right, (int)Math.Ceiling(shadow.Right)),
                Math.Max(border.Bottom, (int)Math.Ceiling(shadow.Bottom)));
        }

        var limit = ImageDocument.MaxCanvasOverhang;
        output = PixelRect.FromLtrb(
            Math.Max(output.X, content.X - limit),
            Math.Max(output.Y, content.Y - limit),
            Math.Min(output.Right, content.Right + limit),
            Math.Min(output.Bottom, content.Bottom + limit));
        return new FrameLayout(content, border, output, left, top, right, bottom);
    }

    /// <summary>Randbreiten links, oben, rechts, unten; ohne sichtbaren Rand alle 0, bei Riss einheitlich.</summary>
    public static (int Left, int Top, int Right, int Bottom) EffectiveBorders(FrameStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!style.HasBorder)
        {
            return (0, 0, 0, 0);
        }

        if (style.HasTorn)
        {
            var widest = Math.Max(Math.Max(style.BorderLeft, style.BorderTop), Math.Max(style.BorderRight, style.BorderBottom));
            return (widest, widest, widest, widest);
        }

        return (style.BorderLeft, style.BorderTop, style.BorderRight, style.BorderBottom);
    }
}
