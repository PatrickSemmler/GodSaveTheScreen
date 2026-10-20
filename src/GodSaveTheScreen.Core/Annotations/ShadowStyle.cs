using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>
/// Schlagschatten nach rechts unten. <see cref="Size"/> = Versatz in Bildpixeln (je Achse),
/// <see cref="Blur"/> = Breite des weichen Rands in Bildpixeln. Transparente Farbe = kein Schatten.
/// Unveränderlich; Änderungen immer per <c>with</c> und neuem Zuweisen.
/// </summary>
public sealed record ShadowStyle(ArgbColor Color, double Size = 4, double Blur = 6)
{
    public const double MaxSize = 200;

    public const double MaxBlur = 100;

    /// <summary>Kein Schatten; Größe und Unschärfe sind vorbelegt, damit nach Wahl einer Farbe sofort ein Schatten entsteht.</summary>
    public static ShadowStyle None { get; } = new(ArgbColor.Transparent);

    public bool IsVisible => !Color.IsTransparent;

    /// <summary>Fläche, die der Schatten eines Objekts mit der sichtbaren Ausdehnung <paramref name="visual"/> belegt.</summary>
    public RectD Extend(RectD visual)
    {
        var size = Math.Max(0, Size);
        var blur = Math.Max(0, Blur);
        return new RectD(visual.X + size, visual.Y + size, visual.Width, visual.Height).Inflate(blur, blur);
    }
}

/// <summary>Objekt mit Schlagschatten (Formen, Text, Marker, Emoji).</summary>
public interface IShadowAnnotation
{
    ShadowStyle Shadow { get; set; }
}
