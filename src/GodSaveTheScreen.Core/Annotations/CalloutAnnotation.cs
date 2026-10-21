using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Körperform einer Sprechblase.</summary>
public enum CalloutShape
{
    /// <summary>Rechteck mit (einstellbar) abgerundeten Ecken.</summary>
    Rounded,

    Ellipse,

    /// <summary>Gedankenblase: Wolke mit kleiner werdenden Kreisen als Spitze.</summary>
    Cloud,
}

/// <summary>
/// Sprechblase: Körper <see cref="Rect"/> mit umbrochenem, mittig gesetztem Text und einer Spitze zum frei
/// verschiebbaren Punkt <see cref="Tail"/>. Der Rahmen (<see cref="Border"/>) liegt außen um Körper und Spitze;
/// mit <see cref="BorderBlur"/> wird er zum weichen Leuchten. Mit <see cref="Rotation"/> drehen sich Körper und Text um
/// die Körpermitte, der Zielpunkt <see cref="Tail"/> bleibt im Bild stehen.
/// </summary>
public sealed partial class CalloutAnnotation : Annotation, IShadowAnnotation, IEditableTextAnnotation, IRotatableAnnotation
{
    public const double MinFontSize = 4;
    public const double MaxFontSize = 999;
    public const double MaxBorder = 50;
    public const double MaxBorderBlur = 50;
    public const double MaxTailWidth = 500;
    public const double MaxPadding = 200;
    public const double MaxCornerRadius = 500;

    /// <summary>Kleinste Körpergröße beim Aufziehen und Skalieren.</summary>
    public const double MinBodySize = 16;

    private double _rotation;

    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>Körper der Blase (ohne Spitze), unverdreht.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Center))]
    private RectD _rect;

    /// <summary>Spitze (Zielpunkt); liegt sie im Körper, wird keine Spitze gezeichnet.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private PointD _tail;

    [ObservableProperty]
    private CalloutShape _shape;

    /// <summary>Eckenradius der abgerundeten Form.</summary>
    [ObservableProperty]
    private double _cornerRadius = 12;

    /// <summary>Breite der Spitze an der Blase (Gedankenblase: Durchmesser des größten Kreises).</summary>
    [ObservableProperty]
    private double _tailWidth = 24;

    /// <summary>Spitze leicht geschwungen statt gerade.</summary>
    [ObservableProperty]
    private bool _tailCurved;

    /// <summary>Gebogene Spitze in die andere Richtung biegen.</summary>
    [ObservableProperty]
    private bool _tailCurveReversed;

    [ObservableProperty]
    private string _fontFamilyName = "Segoe UI";

    [ObservableProperty]
    private int _fontWeight = TextAnnotation.DefaultFontWeight;

    [ObservableProperty]
    private FontStyleKind _fontStyle;

    [ObservableProperty]
    private int _fontStretch = TextAnnotation.NormalStretch;

    [ObservableProperty]
    private double _fontSize = 20;

    [ObservableProperty]
    private TextDecorationKind _decorations;

    [ObservableProperty]
    private TextAlignmentKind _textAlignment = TextAlignmentKind.Center;

    [ObservableProperty]
    private ArgbColor _textColor = ArgbColor.White;

    /// <summary>Hintergrundfarbe des Körpers.</summary>
    [ObservableProperty]
    private ArgbColor _background = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>Rahmen („Leuchtfarbe“) außen um die Blase; Stärke 0 = keiner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    private StrokeStyle _border = new(ArgbColor.FromRgb(0xB7, 0x1C, 0x1C), 0);

    /// <summary>Weicher Rand des Rahmens in Bildpixeln (Leuchten); 0 = scharfer Rahmen.</summary>
    [ObservableProperty]
    private double _borderBlur;

    /// <summary>Abstand zwischen Text und Rand des Körpers.</summary>
    [ObservableProperty]
    private double _padding = 10;

    [ObservableProperty]
    private ShadowStyle _shadow = ShadowStyle.None;

    /// <inheritdoc />
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

    /// <summary>Drehung als Winkel zwischen −180° und 180° (für Regler).</summary>
    [JsonIgnore]
    public double SignedRotation => Rotation > 180 ? Rotation - 360 : Rotation;

    /// <summary>Drehpunkt: Mitte des Körpers.</summary>
    [JsonIgnore]
    public PointD Center => Rect.Normalize().Center;

    /// <summary>Zielpunkt im unverdrehten Koordinatensystem des Körpers.</summary>
    [JsonIgnore]
    public PointD LocalTail => RotationMath.Unrotate(Tail, Center, Rotation);

    /// <summary>
    /// Spitze in der aktuellen Lage im unverdrehten Koordinatensystem des Körpers (<c>null</c>, wenn der Zielpunkt im
    /// Körper liegt); gezeichnet wird sie mit <see cref="Rotation"/> um <see cref="Center"/> gedreht.
    /// </summary>
    [JsonIgnore]
    public CalloutTail? TailGeometry => CalloutGeometry.ComputeTail(Shape, Rect, CornerRadius, LocalTail, TailWidth, TailCurved, TailCurveReversed);

    /// <summary>Fläche, in der der Text umbrochen und senkrecht zentriert wird.</summary>
    [JsonIgnore]
    public RectD TextArea => CalloutGeometry.TextArea(Shape, Rect, Padding);

    [JsonIgnore]
    public override RectD Bounds
    {
        get
        {
            var body = RotationMath.Hull(Rect.Normalize(), Rotation);
            return TailGeometry is null ? body : body.Union(new RectD(Tail.X, Tail.Y, 0, 0));
        }
    }

    /// <summary>Rahmen liegt außen (volle Stärke) plus weicher Rand.</summary>
    [JsonIgnore]
    public override double VisualMargin => (Border.IsVisible ? Border.Thickness + Math.Max(0, BorderBlur) : 0) + 1;

    /// <summary>Text als Textobjekt (Schrift, Farbe, Umbruchbreite und Lage im Körper) für Messung und Zeichnen.</summary>
    public TextAnnotation CreateTextBlock()
    {
        var area = TextArea;
        var block = new TextAnnotation
        {
            Text = Text,
            FontFamilyName = FontFamilyName,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            FontStretch = FontStretch,
            FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize),
            Decorations = Decorations,
            TextAlignment = TextAlignment,
            Fill = TextColor,
            WrapWidth = area.Width,
        };
        var size = block.LayoutSize;
        block.Position = new PointD(area.X, area.Y + ((area.Height - size.Height) / 2));
        return block;
    }

    /// <summary>Vergrößert den Körper nach unten, bis der Text hineinpasst (Breite bleibt). <c>true</c> = geändert.</summary>
    public bool FitHeightToText()
    {
        var rect = Rect.Normalize();
        var needed = CreateTextBlock().LayoutSize.Height;
        var height = rect.Height;
        for (var i = 0; i < 8; i++)
        {
            var area = CalloutGeometry.TextArea(Shape, rect with { Height = height }, Padding);
            var missing = needed - area.Height;
            if (missing <= 0.5)
            {
                break;
            }

            height += missing * (Shape == CalloutShape.Rounded ? 1 : 1.5);
        }

        if (height <= rect.Height + 0.5)
        {
            return false;
        }

        // Gedreht: Oberkante im Bild stehen lassen (der Körper wächst entlang seiner eigenen Achse nach unten).
        var grown = rect with { Height = Math.Ceiling(height) };
        var center = RotationMath.Rotate(grown.Center, rect.Center, Rotation);
        Rect = new RectD(center.X - (grown.Width / 2), center.Y - (grown.Height / 2), grown.Width, grown.Height);
        return true;
    }

    public override void MapPoints(Func<PointD, PointD> map)
    {
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));
        Tail = map(Tail);
    }

    public override void ScaleSizes(double factor)
    {
        FontSize = Math.Clamp(FontSize * factor, MinFontSize, MaxFontSize);
        Padding = Math.Clamp(Padding * factor, 0, MaxPadding);
        CornerRadius = Math.Clamp(CornerRadius * factor, 0, MaxCornerRadius);
        TailWidth = Math.Clamp(TailWidth * factor, 1, MaxTailWidth);
        Border = Border with { Thickness = Math.Clamp(Border.Thickness * factor, 0, MaxBorder) };
    }

    public override bool HitTest(PointD point, double tolerance)
    {
        point = RotationMath.Unrotate(point, Center, Rotation);
        if (CalloutGeometry.BodyContains(Shape, Rect, CornerRadius, point, tolerance))
        {
            return true;
        }

        if (TailGeometry is not { } tail)
        {
            return false;
        }

        if (tail.IsBubbles)
        {
            return tail.Bubbles.Any(b => b.Center.DistanceTo(point) <= b.Radius + tolerance);
        }

        var outline = tail.Outline();
        return HitTesting.PointInPolygon(point, outline) || HitTesting.DistanceToPolyline(point, outline, closed: true) <= tolerance;
    }
}
