using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Objekt mit direkt auf der Zeichenfläche bearbeitbarem Text (Text, Stempel).</summary>
public interface IEditableTextAnnotation
{
    string Text { get; set; }
}

/// <summary>Rahmenform eines Stempels.</summary>
public enum StampFrameShape
{
    Rectangle,
    Rounded,
    DoubleLine,
    Oval,
}

/// <summary>Maße eines Stempels relativ zu seinem unverdrehten Rahmen (Ursprung = linke obere Ecke).</summary>
/// <param name="Size">Außenmaß inklusive voller Rahmenstärke.</param>
/// <param name="FrameInset">Abstand der Mittellinie des Rahmens vom Außenrand.</param>
/// <param name="InnerInset">Abstand der Mittellinie der Innenlinie (Doppelrahmen) vom Außenrand; 0 = keine.</param>
/// <param name="InnerThickness">Stärke der Innenlinie.</param>
/// <param name="CornerRadius">Eckenradius der Mittellinie (abgerundet bzw. Doppelrahmen).</param>
/// <param name="TextSize">Ausdehnung der Schrift (Tintenfläche der Zeichen).</param>
public readonly record struct StampMetrics(SizeD Size, double FrameInset, double InnerInset, double InnerThickness, double CornerRadius, SizeD TextSize);

/// <summary>
/// Stempel: einfarbiger Rahmen mit Text im „Stempel-Look“. Der Rahmen passt sich an den Text an; Skalieren ändert
/// Schriftgröße und Rahmenstärke gemeinsam. <see cref="Rect"/> ist der unverdrehte Rahmen um <see cref="Center"/>,
/// <see cref="Annotation.Bounds"/> die achsparallele Hülle des gedrehten Rahmens. Die Abnutzung (Kratzer, Ausreißer,
/// ungleichmäßige Farbe) ergibt sich aus <see cref="Wear"/> und dem festen Muster <see cref="Seed"/>.
/// </summary>
public sealed partial class StampAnnotation : Annotation, IRotatableAnnotation, IEditableTextAnnotation
{
    public const double MinFontSize = 4;
    public const double MaxFontSize = 999;
    public const double MaxFrameThickness = 200;
    public const double MaxWear = 100;

    private SizeD? _textSize;
    private double _rotation;

    /// <summary>Misst den Text (Tintenfläche der Zeichen); vom Rendering-Projekt gesetzt.</summary>
    public static Func<StampAnnotation, SizeD>? MeasureProvider { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private string _text = string.Empty;

    /// <summary>Mitte des Stempels (Drehpunkt).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private PointD _center;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private string _fontFamilyName = "Arial Black";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private int _fontWeight = 900;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private FontStyleKind _fontStyle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private int _fontStretch = TextAnnotation.NormalStretch;

    /// <summary>Schriftgröße in Bildpixeln; bestimmt auch Innenabstand und Eckenradius.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private double _fontSize = 40;

    /// <summary>Farbe von Rahmen und Schrift.</summary>
    [ObservableProperty]
    private ArgbColor _color = ArgbColor.FromRgb(0xC6, 0x28, 0x28);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private StampFrameShape _frameShape;

    /// <summary>Stärke der Rahmenlinie in Bildpixeln; 0 = kein Rahmen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Rect))]
    private double _frameThickness = 5;

    /// <summary>Stärke des Stempel-Looks 0–100 (0 = sauberer Druck).</summary>
    [ObservableProperty]
    private double _wear = 50;

    /// <summary>Startwert des Abnutzungsmusters (bleibt beim Speichern erhalten, „Neu würfeln“ ändert ihn).</summary>
    [ObservableProperty]
    private int _seed;

    /// <summary>Tinten-Look: Die Farbe wird mit dem Bild darunter multipliziert.</summary>
    [ObservableProperty]
    private bool _inkBlend;

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

    /// <summary>Tintenfläche des Textes.</summary>
    [JsonIgnore]
    public SizeD TextSize => _textSize ??= MeasureProvider?.Invoke(this) ?? Estimate();

    [JsonIgnore]
    public StampMetrics Metrics => ComputeMetrics(TextSize, FontSize, FrameThickness, FrameShape);

    /// <summary>Unverdrehter Rahmen (Außenkante), mittig um <see cref="Center"/>.</summary>
    [JsonIgnore]
    public RectD Rect
    {
        get
        {
            var size = Metrics.Size;
            return new RectD(Center.X - (size.Width / 2), Center.Y - (size.Height / 2), size.Width, size.Height);
        }
    }

    [JsonIgnore]
    public override RectD Bounds => RotationMath.Hull(Rect, Rotation);

    [JsonIgnore]
    public override double VisualMargin => 1;

    /// <summary>Innenabstand zwischen Text und Rahmen (waagerecht, senkrecht).</summary>
    public static (double X, double Y) Padding(double fontSize) => (fontSize * 0.45, fontSize * 0.32);

    /// <summary>Berechnet die Maße aus Textgröße, Schriftgröße, Rahmenstärke und -form.</summary>
    public static StampMetrics ComputeMetrics(SizeD textSize, double fontSize, double frameThickness, StampFrameShape shape)
    {
        var t = Math.Max(0, frameThickness);
        var (padX, padY) = Padding(fontSize);
        var width = textSize.Width + (2 * padX);
        var height = textSize.Height + (2 * padY);
        double innerThickness = 0;
        double gap = 0;
        if (shape == StampFrameShape.DoubleLine && t > 0)
        {
            innerThickness = t * 0.5;
            gap = Math.Max(t * 0.8, fontSize * 0.08);
            width += 2 * (gap + innerThickness);
            height += 2 * (gap + innerThickness);
        }

        if (shape == StampFrameShape.Oval)
        {
            // Die kleinste Ellipse um ein Rechteck hat √2-fache Halbachsen.
            width *= Math.Sqrt(2);
            height *= Math.Sqrt(2);
        }

        var size = new SizeD(width + (2 * t), height + (2 * t));
        var innerInset = innerThickness > 0 ? t + gap + (innerThickness / 2) : 0;
        var radius = shape switch
        {
            StampFrameShape.Rounded => fontSize * 0.35,
            StampFrameShape.DoubleLine => fontSize * 0.15,
            _ => 0,
        };
        return new StampMetrics(size, t / 2, innerInset, innerThickness, radius, textSize);
    }

    public override void MapPoints(Func<PointD, PointD> map) => Center = map(Center);

    public override void ScaleSizes(double factor)
    {
        FontSize = Math.Clamp(FontSize * factor, MinFontSize, MaxFontSize);
        FrameThickness = Math.Clamp(FrameThickness * factor, 0, MaxFrameThickness);
    }

    public override bool HitTest(PointD point, double tolerance)
    {
        var local = RotationMath.Unrotate(point, Center, Rotation);
        return Rect.Inflate(tolerance, tolerance).Contains(local);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        // Messung vor den Benachrichtigungen verwerfen, damit Beobachter frische Größen lesen.
        if (e.PropertyName is nameof(Text) or nameof(FontFamilyName) or nameof(FontWeight) or nameof(FontStyle)
            or nameof(FontStretch) or nameof(FontSize))
        {
            _textSize = null;
        }

        base.OnPropertyChanged(e);
    }

    /// <summary>Grobe Schätzung, solange kein Renderer installiert ist (nur Tests ohne WPF).</summary>
    private SizeD Estimate()
    {
        var lines = Text.Split('\n');
        var longest = Math.Max(1, lines.Max(l => l.Length));
        return new SizeD(longest * FontSize * 0.7, ((Math.Max(1, lines.Length) - 1) * FontSize * 1.2) + (FontSize * 0.72));
    }
}
