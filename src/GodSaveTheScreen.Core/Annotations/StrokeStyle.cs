namespace GodSaveTheScreen.Core.Annotations;

public enum DashKind
{
    Solid,
    Dash,
    Dot,
    DashDot,
}

public enum LineJoinKind
{
    Miter,
    Round,
    Bevel,
}

public enum StrokeCapKind
{
    Flat,
    Round,
    Square,
}

/// <summary>Linienende (Spitze) von Linien und Pfeilen.</summary>
public enum LineEndStyle
{
    None,
    Arrow,
    OpenArrow,
    Circle,
    Square,
}

/// <summary>Unveränderliche Strichbeschreibung; Änderungen immer per <c>with</c> und neuem Zuweisen.</summary>
public sealed record StrokeStyle(
    ArgbColor Color,
    double Thickness = 4,
    DashKind Dash = DashKind.Solid,
    LineJoinKind Join = LineJoinKind.Round,
    StrokeCapKind Cap = StrokeCapKind.Round)
{
    public static StrokeStyle Default { get; } = new(ArgbColor.Red);

    public bool IsVisible => Thickness > 0 && !Color.IsTransparent;
}
