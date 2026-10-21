using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

public enum FontStyleKind
{
    Normal,
    Italic,
    Oblique,
}

public enum TextAlignmentKind
{
    Left,
    Center,
    Right,
}

[Flags]
public enum TextDecorationKind
{
    None = 0,
    Underline = 1,
    Strikethrough = 2,
}

/// <summary>
/// Textobjekt. Schriftwerte sind einfache Typen (Core bleibt WPF-frei): <see cref="FontWeight"/> 100–900,
/// <see cref="FontStretch"/> 1–9 (5 = normal), <see cref="FontSize"/> in Bildpixeln.
/// Die Layoutgröße liefert der Renderer über <see cref="MeasureProvider"/> (FormattedText).
/// </summary>
/// <remarks>
/// Mit <see cref="Rotation"/> dreht sich der Text um die Mitte seines Rahmens (<see cref="Rect"/>);
/// <see cref="Bounds"/> ist die achsparallele Hülle des gedrehten Rahmens.
/// </remarks>
public sealed partial class TextAnnotation : Annotation, IShadowAnnotation, IEditableTextAnnotation, IRotatableAnnotation
{
    public const int DefaultFontWeight = 400;
    public const int NormalStretch = 5;

    /// <summary>Größte weiche Kontur in Bildpixeln.</summary>
    public const double MaxOutlineBlur = 50;

    private SizeD? _layoutSize;

    private double _rotation;

    /// <summary>Misst Text im aktuellen Zustand; vom Rendering-Projekt gesetzt.</summary>
    public static Func<TextAnnotation, SizeD>? MeasureProvider { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private string _text = string.Empty;

    /// <summary>Oben links des Textblocks (ohne Innenabstand).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD _position;

    [ObservableProperty]
    private string _fontFamilyName = "Segoe UI";

    [ObservableProperty]
    private int _fontWeight = DefaultFontWeight;

    [ObservableProperty]
    private FontStyleKind _fontStyle;

    [ObservableProperty]
    private int _fontStretch = NormalStretch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _fontSize = 24;

    /// <summary>Textfarbe.</summary>
    [ObservableProperty]
    private ArgbColor _fill = ArgbColor.Black;

    /// <summary>Kontur um die Buchstaben; Stärke 0 = keine.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private StrokeStyle _stroke = new(ArgbColor.White, 0);

    /// <summary>Weicher Rand der Kontur in Bildpixeln; 0 = scharfe Kontur. Die Buchstaben bleiben scharf.</summary>
    [ObservableProperty]
    private double _outlineBlur;

    [ObservableProperty]
    private ShadowStyle _shadow = ShadowStyle.None;

    [ObservableProperty]
    private TextAlignmentKind _textAlignment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(HasBackground))]
    private ArgbColor _background = ArgbColor.Transparent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double _backgroundPadding = 6;

    [ObservableProperty]
    private double _backgroundCornerRadius = 4;

    [ObservableProperty]
    private TextDecorationKind _decorations;

    /// <summary>Umbruchbreite in Bildpixeln; <c>null</c> = keine Umbrüche.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private double? _wrapWidth;

    [JsonIgnore]
    public bool HasBackground => Background.A > 0;

    /// <summary>Innenabstand um den Text (nur mit Hintergrund).</summary>
    [JsonIgnore]
    public double EffectivePadding => HasBackground ? BackgroundPadding : 0;

    /// <summary>Layoutgröße des reinen Textblocks.</summary>
    [JsonIgnore]
    public SizeD LayoutSize => _layoutSize ??= MeasureProvider?.Invoke(this) ?? Estimate();

    /// <summary>Rechteck des reinen Textblocks (ohne Innenabstand und Kontur).</summary>
    [JsonIgnore]
    public RectD TextRect => new(Position.X, Position.Y, LayoutSize.Width, LayoutSize.Height);

    /// <inheritdoc />
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Rotation
    {
        get => _rotation;
        set
        {
            if (SetProperty(ref _rotation, RotationMath.Normalize(value)))
            {
                OnPropertyChanged(nameof(Bounds));
            }
        }
    }

    /// <summary>Unverdrehter Rahmen: Textblock plus Innenabstand plus halbe Konturstärke.</summary>
    [JsonIgnore]
    public RectD Rect
    {
        get
        {
            var inflate = EffectivePadding + (Stroke.IsVisible ? Stroke.Thickness / 2 : 0);
            return TextRect.Inflate(inflate, inflate);
        }
    }

    [JsonIgnore]
    public PointD Center => Rect.Center;

    /// <summary>Achsparallele Hülle des gedrehten Rahmens (ohne Drehung = <see cref="Rect"/>).</summary>
    [JsonIgnore]
    public override RectD Bounds => Rotation == 0 ? Rect : RotationMath.Hull(Rect, Rotation);

    [JsonIgnore]
    public override double VisualMargin => ((Stroke.IsVisible ? (Stroke.Thickness / 2) + Math.Max(0, OutlineBlur) : 0) + 1) * RotationMath.HullFactor(Rotation);

    public override void MapPoints(Func<PointD, PointD> map) => Position = map(Position);

    public override void ScaleSizes(double factor)
    {
        FontSize *= factor;
        BackgroundPadding *= factor;
        BackgroundCornerRadius *= factor;
        if (WrapWidth is { } width)
        {
            WrapWidth = width * factor;
        }
    }

    public override bool HitTest(PointD point, double tolerance)
    {
        return Rect.Inflate(tolerance, tolerance).Contains(this.ToLocal(point));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        // Das Layout vor den Benachrichtigungen verwerfen, damit Beobachter frische Größen lesen.
        if (e.PropertyName is nameof(Text) or nameof(FontFamilyName) or nameof(FontWeight) or nameof(FontStyle)
            or nameof(FontStretch) or nameof(FontSize) or nameof(TextAlignment) or nameof(Decorations) or nameof(WrapWidth))
        {
            _layoutSize = null;
        }

        base.OnPropertyChanged(e);
    }

    /// <summary>Grobe Schätzung, solange kein Renderer installiert ist (nur Tests ohne WPF).</summary>
    private SizeD Estimate()
    {
        var lines = Text.Split('\n');
        var width = WrapWidth ?? Math.Max(1, lines.Max(l => l.Length)) * FontSize * 0.55;
        return new SizeD(width, Math.Max(1, lines.Length) * FontSize * 1.25);
    }
}
