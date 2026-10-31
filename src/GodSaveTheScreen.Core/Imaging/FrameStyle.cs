using System.Text.Json.Serialization;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Seiten eines Rechtecks (für abgerissene Kanten).</summary>
[Flags]
public enum FrameSides
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    All = Left | Top | Right | Bottom,
}

/// <summary>
/// Rahmen und Kanteneffekte des ganzen Bildes (Rand, Schatten, runde Ecken, abgerissene Kante, Vignette, Graustufen).
/// Unveränderlich; Änderungen per <c>with</c>. Ein Effekt ist „aus“, wenn sein Wert neutral ist (Rand 0, transparenter
/// Schatten, Radius 0, keine Rissseiten, Vignettenstärke 0, kein Hintergrund, Innenabstand 0, freies Seitenverhältnis). Sind alle Effekte aus, ist der Rahmen leer
/// (<see cref="IsEmpty"/>) und wird am Dokument als <c>null</c> abgelegt (<see cref="Normalized"/>).
/// </summary>
public sealed record FrameStyle
{
    public const int MaxBorder = 1000;

    public const double MaxCornerRadius = 1000;

    public const double MinTornHeight = 1;

    public const double MaxTornHeight = 100;

    public const double MinTornWidth = 4;

    public const double MaxTornWidth = 300;

    public const double MinVignetteSize = 0.1;

    public const int MaxPadding = 2000;

    /// <summary>Farbe des Rands.</summary>
    public ArgbColor BorderColor { get; init; } = ArgbColor.White;

    /// <summary>Breite des Rands je Seite in ganzen Bildpixeln (0 = kein Rand auf dieser Seite).</summary>
    public int BorderLeft { get; init; }

    public int BorderTop { get; init; }

    public int BorderRight { get; init; }

    public int BorderBottom { get; init; }

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;

    /// <summary>Radius der abgerundeten Ecken in Bildpixeln (0 = eckig).</summary>
    public double CornerRadius { get; init; }

    /// <summary>Seiten mit abgerissener Kante.</summary>
    public FrameSides TornSides { get; init; }

    /// <summary>Tiefe der Zacken in Bildpixeln.</summary>
    public double TornHeight { get; init; } = 8;

    /// <summary>Mittlerer Abstand der Zacken in Bildpixeln.</summary>
    public double TornWidth { get; init; } = 16;

    /// <summary>Startwert des Zufallsmusters; ein anderer Wert ergibt einen anderen Riss.</summary>
    public int TornSeed { get; init; } = 1;

    /// <summary>Stärke der Randabdunklung 0 bis 1 (0 = aus).</summary>
    public double VignetteStrength { get; init; }

    /// <summary>Ausdehnung der Randabdunklung nach innen (Anteil des Bildradius, <see cref="MinVignetteSize"/> bis 1).</summary>
    public double VignetteSize { get; init; } = 0.6;

    /// <summary>Screenshot in Graustufen; Objekte bleiben farbig.</summary>
    public bool Grayscale { get; init; }

    /// <summary>Hintergrund hinter dem Screenshot und im Innenabstand; <c>null</c> = keiner.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrameBackground? Background { get; init; }

    /// <summary>Innenabstand zwischen Rand und Ausgaberand in Bildpixeln (0 = keiner).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Padding { get; init; }

    /// <summary>Festes Seitenverhältnis der Ausgabe; <c>null</c> = frei.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrameAspect? AspectRatio { get; init; }

    [JsonIgnore]
    public bool HasBorder => !BorderColor.IsTransparent && (BorderLeft > 0 || BorderTop > 0 || BorderRight > 0 || BorderBottom > 0);

    [JsonIgnore]
    public bool HasShadow => Shadow is { IsVisible: true };

    [JsonIgnore]
    public bool HasCorners => CornerRadius >= 0.5;

    [JsonIgnore]
    public bool HasTorn => TornSides != FrameSides.None && TornHeight >= MinTornHeight;

    [JsonIgnore]
    public bool HasVignette => VignetteStrength > 0.001;

    [JsonIgnore]
    public bool HasBackground => Background is { IsVisible: true };

    [JsonIgnore]
    public bool HasPadding => Padding > 0;

    [JsonIgnore]
    public bool HasAspect => AspectRatio is { IsFixed: true };

    /// <summary>Alle Effekte sind aus.</summary>
    [JsonIgnore]
    public bool IsEmpty => !HasBorder && !HasShadow && !HasCorners && !HasTorn && !HasVignette && !Grayscale && !HasBackground && !HasPadding && !HasAspect;

    /// <summary>Alle vier Randbreiten sind gleich.</summary>
    [JsonIgnore]
    public bool HasUniformBorder => BorderLeft == BorderTop && BorderTop == BorderRight && BorderRight == BorderBottom;

    /// <summary>Werte auf ihren zulässigen Bereich begrenzt; die Effekte bleiben, wie sie sind.</summary>
    public FrameStyle Clamped() => this with
    {
        BorderLeft = Math.Clamp(BorderLeft, 0, MaxBorder),
        BorderTop = Math.Clamp(BorderTop, 0, MaxBorder),
        BorderRight = Math.Clamp(BorderRight, 0, MaxBorder),
        BorderBottom = Math.Clamp(BorderBottom, 0, MaxBorder),
        Shadow = (Shadow ?? ShadowStyle.None) with
        {
            Size = Math.Clamp(Finite(Shadow?.Size ?? 0), 0, ShadowStyle.MaxSize),
            Blur = Math.Clamp(Finite(Shadow?.Blur ?? 0), 0, ShadowStyle.MaxBlur),
        },
        CornerRadius = Math.Clamp(Finite(CornerRadius), 0, MaxCornerRadius),
        TornSides = TornSides & FrameSides.All,
        TornHeight = Math.Clamp(Finite(TornHeight, 8), MinTornHeight, MaxTornHeight),
        TornWidth = Math.Clamp(Finite(TornWidth, 16), MinTornWidth, MaxTornWidth),
        VignetteStrength = Math.Clamp(Finite(VignetteStrength), 0, 1),
        VignetteSize = Math.Clamp(Finite(VignetteSize, 0.6), MinVignetteSize, 1),
        Background = Background is { Kind: not FrameBackgroundKind.None } background ? background.Clamped() : null,
        Padding = Math.Clamp(Padding, 0, MaxPadding),
        AspectRatio = AspectRatio is { IsFixed: true } aspect ? aspect.Clamped() : null,
    };

    /// <summary>Ohne Hintergrundbild (für Einstellungen und Vorlagen, die kein Bild speichern).</summary>
    public FrameStyle WithoutImage() => Background is { Image: not null } background ? this with { Background = background.WithoutImage() } : this;

    /// <summary>
    /// Rahmen aus einer Vorlage: Hat die Vorlage einen Bild-Hintergrund, bleibt das Bild des <paramref name="current"/>en Rahmens
    /// erhalten; gibt es keines, fällt der Hintergrund auf Einfarbig (<see cref="FrameBackground.ColorA"/>) zurück.
    /// </summary>
    public static FrameStyle FromPreset(FrameStyle preset, FrameStyle? current)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (preset.Background is not { Kind: FrameBackgroundKind.Image } background)
        {
            return preset;
        }

        var image = background.Image ?? current?.Background?.Image;
        return preset with { Background = image is not null ? background with { Image = image } : background with { Kind = FrameBackgroundKind.Solid } };
    }

    /// <summary>Begrenzte Werte; sind alle Effekte aus, <c>null</c> (so verhält sich das Dokument wie ohne Rahmen).</summary>
    public FrameStyle? Normalized()
    {
        var clamped = Clamped();
        return clamped.IsEmpty ? null : clamped;
    }

    private static double Finite(double value, double fallback = 0) => double.IsFinite(value) ? value : fallback;
}
