using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.Core.Settings;

/// <summary>Standardwerte eines Formwerkzeugs (werden je Werkzeug gespeichert).</summary>
public sealed record ShapeStyle
{
    public StrokeStyle Stroke { get; init; } = StrokeStyle.Default;

    public ArgbColor Fill { get; init; } = ArgbColor.Transparent;

    public double CornerRadius { get; init; }

    public LineEndStyle StartCap { get; init; }

    public LineEndStyle EndCap { get; init; }

    public double Opacity { get; init; } = 1;

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;

    /// <summary>Kontur von Linie/Pfeil (Stärke 0 = keine).</summary>
    public StrokeStyle Outline { get; init; } = LineAnnotation.NoOutline;

    public double OutlineBlur { get; init; }
}

/// <summary>Standardwerte des Textwerkzeugs.</summary>
public sealed record TextStyle
{
    public string FontFamilyName { get; init; } = "Segoe UI";

    public int FontWeight { get; init; } = 400;

    public FontStyleKind FontStyle { get; init; }

    public int FontStretch { get; init; } = 5;

    public double FontSize { get; init; } = 28;

    public ArgbColor Fill { get; init; } = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    public StrokeStyle Stroke { get; init; } = new(ArgbColor.White, 0);

    public double OutlineBlur { get; init; }

    public TextAlignmentKind TextAlignment { get; init; }

    public ArgbColor Background { get; init; } = ArgbColor.Transparent;

    public double BackgroundPadding { get; init; } = 6;

    public double BackgroundCornerRadius { get; init; } = 4;

    public TextDecorationKind Decorations { get; init; }

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;
}

/// <summary>Standardwerte des Markerwerkzeugs.</summary>
public sealed record MarkerStyle
{
    public MarkerShape Shape { get; init; }

    public double Size { get; init; } = 32;

    public ArgbColor Fill { get; init; } = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    public StrokeStyle Stroke { get; init; } = new(ArgbColor.White, 2);

    public ArgbColor TextColor { get; init; } = ArgbColor.White;

    public string FontFamilyName { get; init; } = "Segoe UI";

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;
}

/// <summary>Standardwerte des Stempelwerkzeugs (letzte Konfiguration).</summary>
public sealed record StampStyle
{
    /// <summary>Text des nächsten Stempels; leer = lokalisierter Standardtext der Oberfläche.</summary>
    public string Text { get; init; } = string.Empty;

    public string FontFamilyName { get; init; } = StampAnnotation.DefaultFontFamily;

    public int FontWeight { get; init; } = StampAnnotation.DefaultFontWeight;

    public FontStyleKind FontStyle { get; init; }

    public int FontStretch { get; init; } = 5;

    public double FontSize { get; init; } = 40;

    public TextDecorationKind Decorations { get; init; }

    public ArgbColor Color { get; init; } = ArgbColor.FromRgb(0xC6, 0x28, 0x28);

    public StampFrameShape FrameShape { get; init; }

    public double FrameThickness { get; init; } = 5;

    /// <summary>Stärke des Stempel-Looks 0–100.</summary>
    public double Wear { get; init; } = 50;

    /// <summary>Neigung in Grad (−180 bis 180).</summary>
    public double Rotation { get; init; } = -8;

    public bool InkBlend { get; init; }

    public double Opacity { get; init; } = 1;
}

/// <summary>Standardwerte des Sprechblasen-Werkzeugs.</summary>
public sealed record CalloutStyle
{
    public CalloutShape Shape { get; init; }

    public double CornerRadius { get; init; } = 12;

    public double TailWidth { get; init; } = 24;

    public bool TailCurved { get; init; }

    public bool TailCurveReversed { get; init; }

    public string FontFamilyName { get; init; } = "Segoe UI";

    public int FontWeight { get; init; } = 400;

    public FontStyleKind FontStyle { get; init; }

    public int FontStretch { get; init; } = 5;

    public double FontSize { get; init; } = 20;

    public TextDecorationKind Decorations { get; init; }

    public TextAlignmentKind TextAlignment { get; init; } = TextAlignmentKind.Center;

    public ArgbColor TextColor { get; init; } = ArgbColor.White;

    public ArgbColor Background { get; init; } = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>Rahmen („Leuchtfarbe“) außen um die Blase; Stärke 0 = keiner.</summary>
    public StrokeStyle Border { get; init; } = new(ArgbColor.FromRgb(0xB7, 0x1C, 0x1C), 0);

    /// <summary>Weicher Rand des Rahmens (Leuchten).</summary>
    public double BorderBlur { get; init; }

    public double Padding { get; init; } = 10;

    public double Opacity { get; init; } = 1;

    public ShadowStyle Shadow { get; init; } = new(new ArgbColor(0x59, 0, 0, 0), 3, 6);

    /// <summary>Ersetzt fehlende Werte (ältere oder beschädigte Einstellungen) durch die Standardwerte.</summary>
    public CalloutStyle Normalized()
    {
        var defaults = new CalloutStyle();
        return this with
        {
            FontFamilyName = string.IsNullOrWhiteSpace(FontFamilyName) ? defaults.FontFamilyName : FontFamilyName,
            FontSize = FontSize > 0 ? FontSize : defaults.FontSize,
            Border = Border ?? defaults.Border,
            Shadow = Shadow ?? ShadowStyle.None,
            TailWidth = TailWidth > 0 ? TailWidth : defaults.TailWidth,
        };
    }
}

/// <summary>Standardwerte des Bild-Werkzeugs (neu eingefügte Bilder).</summary>
public sealed record ImageStyle
{
    public double Opacity { get; init; } = 1;

    public ShadowStyle Shadow { get; init; } = ShadowStyle.None;

    /// <summary>Kontur um die deckenden Bildteile (nur Farbe und Stärke); Stärke 0 = keine.</summary>
    public StrokeStyle Outline { get; init; } = ImageAnnotation.NoOutline;

    public double OutlineBlur { get; init; }

    /// <summary>Ersetzt fehlende Werte (ältere oder beschädigte Einstellungen) durch die Standardwerte.</summary>
    public ImageStyle Normalized() => this with
    {
        Shadow = Shadow ?? ShadowStyle.None,
        Outline = Outline ?? ImageAnnotation.NoOutline,
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 1,
    };
}

/// <summary>Werkzeug-ID → Standardwerte, mit Wertgleichheit (damit <c>AppSettings</c> als Record vergleichbar bleibt).</summary>
public sealed class ShapeDefaultsMap : Dictionary<string, ShapeStyle>
{
    public ShapeDefaultsMap()
    {
    }

    public ShapeDefaultsMap(IDictionary<string, ShapeStyle> source)
        : base(source)
    {
    }

    public override bool Equals(object? obj) =>
        obj is ShapeDefaultsMap other && Count == other.Count && this.All(p => other.TryGetValue(p.Key, out var v) && Equals(v, p.Value));

    public override int GetHashCode() => Count;
}

/// <summary>Textliste mit Wertgleichheit.</summary>
public sealed class StringList : List<string>
{
    public StringList()
    {
    }

    public StringList(IEnumerable<string> source)
        : base(source)
    {
    }

    public override bool Equals(object? obj) => obj is StringList other && this.SequenceEqual(other);

    public override int GetHashCode() => Count;
}
