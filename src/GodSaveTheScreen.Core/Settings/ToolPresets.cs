using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Settings;

/// <summary>Standardwerte des Unschärfe-Werkzeugs.</summary>
public sealed record BlurStyle
{
    public BlurMode Mode { get; init; } = BlurMode.Pixelate;

    public double Strength { get; init; } = 14;

    public RegionShape Shape { get; init; }

    public double FeatherEdge { get; init; }

    public ArgbColor RedactColor { get; init; } = ArgbColor.Black;
}

/// <summary>Letzte Konfiguration von Bereichsauswahl und Hintergrundradierer (Entfernen von Bildbereichen).</summary>
public sealed record MaskToolStyle
{
    public AreaSelectShape Shape { get; init; } = AreaSelectShape.Rectangle;

    /// <summary>Weiche Kante beim Entfernen in Bildpixeln (0 = hart).</summary>
    public double Feather { get; init; }

    /// <summary>Farbtoleranz des Zauberstabs in Prozent.</summary>
    public double Tolerance { get; init; } = 15;

    /// <summary>Durchmesser des Radierers in Bildpixeln.</summary>
    public double BrushSize { get; init; } = 40;

    /// <summary>Härte des Radierers in Prozent (100 = harte Kante).</summary>
    public double BrushHardness { get; init; } = 80;
}

/// <summary>Glättung und Druckempfindlichkeit neuer Freihandstriche.</summary>
public sealed record FreehandStyle
{
    /// <summary>Glättung 0 (Polygonzug) bis 1 (weiche Kurve).</summary>
    public double Smoothing { get; init; } = 0.5;

    public bool UsePressure { get; init; } = true;
}

/// <summary>Letzte Konfiguration des Emoji-Werkzeugs.</summary>
public sealed record EmojiStyle
{
    public double Opacity { get; init; } = 1;

    /// <summary>Kantenlänge neuer Emojis in Bildpixeln bei einem Klick ohne Aufziehen.</summary>
    public double DefaultSize { get; init; } = 96;

    public Emoji.SkinTone SkinTone { get; init; }

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;
}

/// <summary>Standardwerte des Lupen-Werkzeugs.</summary>
public sealed record MagnifierStyle
{
    public double Zoom { get; init; } = 2;

    public MagnifierShape Shape { get; init; } = MagnifierShape.Circle;

    public StrokeStyle Border { get; init; } = new(ArgbColor.FromRgb(0x33, 0x33, 0x33), 3);

    public bool ShowConnector { get; init; } = true;

    public bool ShowSourceFrame { get; init; } = true;

    public bool BaseImageOnly { get; init; }

    public bool SmoothScaling { get; init; } = true;
}

/// <summary>
/// Benannte Vorlage eines Werkzeugs. Genau der zur Werkzeugart passende Stil ist gesetzt:
/// <see cref="Shape"/> (Formen, Stift, Textmarker; Stift/Textmarker zusätzlich <see cref="Smoothing"/> und <see cref="UsePressure"/>),
/// <see cref="Text"/>, <see cref="Marker"/>, <see cref="Blur"/>, <see cref="Magnifier"/>, <see cref="Callout"/> oder <see cref="Image"/>.
/// </summary>
public sealed record ToolPreset
{
    public string Name { get; init; } = string.Empty;

    public ShapeStyle? Shape { get; init; }

    public double? Smoothing { get; init; }

    public bool? UsePressure { get; init; }

    public TextStyle? Text { get; init; }

    public MarkerStyle? Marker { get; init; }

    public BlurStyle? Blur { get; init; }

    public MagnifierStyle? Magnifier { get; init; }

    public CalloutStyle? Callout { get; init; }

    public ImageStyle? Image { get; init; }
}

/// <summary>Vorlagenliste mit Wertgleichheit.</summary>
public sealed class ToolPresetList : List<ToolPreset>
{
    public ToolPresetList()
    {
    }

    public ToolPresetList(IEnumerable<ToolPreset> source)
        : base(source)
    {
    }

    public override bool Equals(object? obj) => obj is ToolPresetList other && this.SequenceEqual(other);

    public override int GetHashCode() => Count;
}

/// <summary>Werkzeug-ID → Vorlagen, mit Wertgleichheit (damit <c>AppSettings</c> als Record vergleichbar bleibt).</summary>
public sealed class ToolPresetMap : Dictionary<string, ToolPresetList>
{
    public ToolPresetMap()
    {
    }

    public ToolPresetMap(IDictionary<string, ToolPresetList> source)
        : base(source)
    {
    }

    public override bool Equals(object? obj) =>
        obj is ToolPresetMap other && Count == other.Count && this.All(p => other.TryGetValue(p.Key, out var v) && Equals(v, p.Value));

    public override int GetHashCode() => Count;
}

/// <summary>Regeln für Vorlagennamen: Randleerzeichen entfernt, Vergleich ohne Beachtung der Groß-/Kleinschreibung.</summary>
public static class PresetNames
{
    public const int MaxLength = 60;

    public static string Clean(string? name) => (name ?? string.Empty).Trim();

    public static bool IsValid(string? name) => Clean(name) is { Length: > 0 and <= MaxLength };

    public static bool AreSame(string? a, string? b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
}
