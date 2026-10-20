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
