using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Art des Hintergrunds hinter dem Screenshot.</summary>
public enum FrameBackgroundKind
{
    None,
    Solid,
    Gradient,
    Image,
}

/// <summary>
/// Hintergrund hinter dem Screenshot (füllt die ganze Ausgabe einschließlich Innenabstand): einfarbig, linearer Verlauf
/// aus zwei Farben oder ein eigenes Bild, das die Fläche ausfüllt (Seitenverhältnis bleibt, Überstand wird zentriert beschnitten).
/// Unveränderlich; Änderungen per <c>with</c>.
/// </summary>
public sealed record FrameBackground
{
    public const double DefaultAngle = 135;

    /// <summary>Kein Hintergrund (Innenabstand bleibt transparent).</summary>
    public static FrameBackground None { get; } = new();

    public FrameBackgroundKind Kind { get; init; }

    /// <summary>Farbe bzw. Startfarbe des Verlaufs.</summary>
    public ArgbColor ColorA { get; init; } = ArgbColor.FromRgb(0x2B, 0x6C, 0xD9);

    /// <summary>Endfarbe des Verlaufs.</summary>
    public ArgbColor ColorB { get; init; } = ArgbColor.FromRgb(0x8E, 0x3C, 0xD8);

    /// <summary>
    /// Richtung des Verlaufs in Grad: 0 = von links nach rechts, 90 = von oben nach unten, 135 = von links oben nach
    /// rechts unten. Die Farben enden genau in den Ecken bzw. Kanten der Fläche.
    /// </summary>
    public double AngleDegrees { get; init; } = DefaultAngle;

    /// <summary>Bilddatei für <see cref="FrameBackgroundKind.Image"/>; wird nicht in den Einstellungen gespeichert.</summary>
    public ImageAsset? Image { get; init; }

    /// <summary>Der Hintergrund zeichnet etwas (ein Bild ohne Datei zählt nicht).</summary>
    public bool IsVisible => Kind switch
    {
        FrameBackgroundKind.Solid => !ColorA.IsTransparent,
        FrameBackgroundKind.Gradient => !ColorA.IsTransparent || !ColorB.IsTransparent,
        FrameBackgroundKind.Image => Image is not null,
        _ => false,
    };

    /// <summary>Winkel begrenzt auf −360 bis 360; nicht endliche Werte werden zum Standard.</summary>
    public FrameBackground Clamped() => this with
    {
        AngleDegrees = double.IsFinite(AngleDegrees) ? Math.Clamp(AngleDegrees, -360, 360) : DefaultAngle,
        Kind = Enum.IsDefined(Kind) ? Kind : FrameBackgroundKind.None,
    };

    /// <summary>Ohne Bilddatei (Einstellungen und Vorlagen speichern kein Bild, nur die Art).</summary>
    public FrameBackground WithoutImage() => Image is null ? this : this with { Image = null };
}

/// <summary>Art des festen Seitenverhältnisses der Ausgabe.</summary>
public enum FrameAspectKind
{
    Free,
    Wide16x9,
    Standard4x3,
    Square,
    Tall9x16,
    Custom,
}

/// <summary>
/// Festes Seitenverhältnis der Ausgabe. Die Ausgabe wird nur vergrößert (Inhalt, Rand und Innenabstand bleiben unverändert
/// und stehen mittig); <see cref="FrameAspectKind.Free"/> lässt die Ausgabe, wie sie ist.
/// </summary>
public sealed record FrameAspect
{
    public const int MaxPart = 100;

    public static FrameAspect Free { get; } = new();

    public FrameAspectKind Kind { get; init; }

    /// <summary>Breite des benutzerdefinierten Verhältnisses.</summary>
    public int Width { get; init; } = 16;

    /// <summary>Höhe des benutzerdefinierten Verhältnisses.</summary>
    public int Height { get; init; } = 9;

    /// <summary>Verhältnis Breite : Höhe oder <c>null</c> bei freiem Verhältnis.</summary>
    public (int Width, int Height)? Ratio => Kind switch
    {
        FrameAspectKind.Wide16x9 => (16, 9),
        FrameAspectKind.Standard4x3 => (4, 3),
        FrameAspectKind.Square => (1, 1),
        FrameAspectKind.Tall9x16 => (9, 16),
        FrameAspectKind.Custom => (Math.Clamp(Width, 1, MaxPart), Math.Clamp(Height, 1, MaxPart)),
        _ => null,
    };

    public bool IsFixed => Ratio is not null;

    public FrameAspect Clamped() => this with
    {
        Kind = Enum.IsDefined(Kind) ? Kind : FrameAspectKind.Free,
        Width = Math.Clamp(Width, 1, MaxPart),
        Height = Math.Clamp(Height, 1, MaxPart),
    };
}
