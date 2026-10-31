using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>
/// Lage der Teile eines Rahmens in Bildkoordinaten (Pixel des Basisbilds): <see cref="Content"/> ist der Inhalt
/// (<c>EffectiveBounds</c>), <see cref="Border"/> Inhalt plus Rand, <see cref="Output"/> das Ergebnisbild einschließlich
/// Innenabstand, Schatten und Auffüllung auf das Seitenverhältnis. <see cref="AspectLimited"/>: Das gewählte Seitenverhältnis
/// wurde nicht angewendet, weil die Ausgabe sonst zu groß würde.
/// </summary>
public readonly record struct FrameLayout(PixelRect Content, PixelRect Border, PixelRect Output, int Left, int Top, int Right, int Bottom, bool AspectLimited = false)
{
    /// <summary>Höchstzahl der Pixel des Ergebnisbilds, wenn das Seitenverhältnis die Ausgabe vergrößert.</summary>
    public const long MaxAspectPixels = 100_000_000;

    /// <summary>Längste Seite des Ergebnisbilds, wenn das Seitenverhältnis die Ausgabe vergrößert.</summary>
    public const int MaxAspectSide = 16384;

    /// <summary>Ohne Rahmen: alle Rechtecke sind der Inhalt.</summary>
    public static FrameLayout Plain(PixelRect content) => new(content, content, content, 0, 0, 0, 0);

    /// <summary>
    /// Berechnet das Layout: Inhalt, Rand, Innenabstand, dann Auffüllen auf das Seitenverhältnis (die Ausgabe wird nur größer,
    /// der Inhalt steht mittig). Der Schatten gehört zur Ausgabe, soweit er über den Innenabstand hinausreicht.
    /// Bei abgerissener Kante hat der Rand auf allen Seiten die größte eingestellte Breite.
    /// Rand, Innenabstand und Schatten wachsen je Seite höchstens um <see cref="ImageDocument.MaxCanvasOverhang"/> über den Inhalt hinaus.
    /// </summary>
    public static FrameLayout Compute(PixelRect content, FrameStyle? style)
    {
        if (style is null)
        {
            return Plain(content);
        }

        var (left, top, right, bottom) = EffectiveBorders(style);
        var border = PixelRect.FromLtrb(content.X - left, content.Y - top, content.Right + right, content.Bottom + bottom);
        var padding = style.HasPadding ? Math.Clamp(style.Padding, 0, FrameStyle.MaxPadding) : 0;
        var output = PixelRect.FromLtrb(border.X - padding, border.Y - padding, border.Right + padding, border.Bottom + padding);
        if (style.HasShadow)
        {
            var shadow = style.Shadow.Extend(border.ToRectD());
            output = PixelRect.FromLtrb(
                Math.Min(output.X, (int)Math.Floor(shadow.X)),
                Math.Min(output.Y, (int)Math.Floor(shadow.Y)),
                Math.Max(output.Right, (int)Math.Ceiling(shadow.Right)),
                Math.Max(output.Bottom, (int)Math.Ceiling(shadow.Bottom)));
        }

        var limit = ImageDocument.MaxCanvasOverhang;
        output = PixelRect.FromLtrb(
            Math.Max(output.X, content.X - limit),
            Math.Max(output.Y, content.Y - limit),
            Math.Min(output.Right, content.Right + limit),
            Math.Min(output.Bottom, content.Bottom + limit));

        var limited = false;
        if (style.AspectRatio is { Ratio: { } ratio })
        {
            var filled = FillToAspect(output, ratio.Width, ratio.Height);
            if (filled.Width > MaxAspectSide || filled.Height > MaxAspectSide || (long)filled.Width * filled.Height > MaxAspectPixels)
            {
                limited = filled != output;
            }
            else
            {
                output = filled;
            }
        }

        return new FrameLayout(content, border, output, left, top, right, bottom, limited);
    }

    /// <summary>
    /// Vergrößert <paramref name="rect"/> auf das Verhältnis <paramref name="ratioWidth"/> : <paramref name="ratioHeight"/>
    /// (nur eine Achse, mittig; ein ungerades Restpixel kommt nach rechts bzw. unten). Die Abweichung vom Verhältnis beträgt
    /// durch die Rundung höchstens ein Pixel.
    /// </summary>
    public static PixelRect FillToAspect(PixelRect rect, int ratioWidth, int ratioHeight)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || ratioWidth <= 0 || ratioHeight <= 0)
        {
            return rect;
        }

        // Breite/Höhe < Verhältnis  <=>  Breite · rh < Höhe · rw: Es fehlt Breite, sonst Höhe.
        if ((long)rect.Width * ratioHeight < (long)rect.Height * ratioWidth)
        {
            var width = (int)Math.Min(int.MaxValue / 4, ((long)rect.Height * ratioWidth + ratioHeight - 1) / ratioHeight);
            var extra = width - rect.Width;
            var left = extra / 2;
            return PixelRect.FromLtrb(rect.X - left, rect.Y, rect.Right + (extra - left), rect.Bottom);
        }
        else
        {
            var height = (int)Math.Min(int.MaxValue / 4, ((long)rect.Width * ratioHeight + ratioWidth - 1) / ratioWidth);
            var extra = height - rect.Height;
            var top = extra / 2;
            return PixelRect.FromLtrb(rect.X, rect.Y - top, rect.Right, rect.Bottom + (extra - top));
        }
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
