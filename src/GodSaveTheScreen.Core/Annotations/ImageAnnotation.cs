using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Objekt, das sich an seiner Mitte spiegeln lässt (Emoji, Bild).</summary>
public interface IFlippableAnnotation
{
    bool FlipHorizontal { get; set; }

    bool FlipVertical { get; set; }
}

/// <summary>
/// Eingefügtes Bild (PNG, JPEG, SVG …). <see cref="Rect"/> ist der Rahmen vor der Drehung, die Drehung erfolgt um dessen Mitte;
/// <see cref="Annotation.Bounds"/> ist die achsparallele Hülle des gedrehten Rahmens. Schatten und Kontur folgen der Deckung
/// des Bildes (transparente Bereiche bekommen keinen Schatten).
/// </summary>
public sealed partial class ImageAnnotation : Annotation, IRotatableAnnotation, IShadowAnnotation, IFlippableAnnotation
{
    public const double MinSize = 4;

    public const double MaxOutlineThickness = 50;

    public const double MaxOutlineBlur = 50;

    /// <summary>Die Bilddatei; <c>null</c> nur bei einem leeren Objekt (wird als Platzhalter gezeichnet).</summary>
    [ObservableProperty]
    private ImageAsset? _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bounds))]
    [NotifyPropertyChangedFor(nameof(Center))]
    private RectD _rect;

    /// <summary>Größe beim Einfügen (Pixel der Datei bzw. Standardgröße eines SVG) für „Originalgröße“.</summary>
    [ObservableProperty]
    private SizeD _originalSize;

    [ObservableProperty]
    private bool _flipHorizontal;

    [ObservableProperty]
    private bool _flipVertical;

    [ObservableProperty]
    private ShadowStyle _shadow = ShadowStyle.None;

    /// <summary>Kontur um die deckenden Bildteile (nur Farbe und Stärke werden genutzt); Standard <see cref="NoOutline"/>.</summary>
    [ObservableProperty]
    private StrokeStyle _outline = NoOutline;

    /// <summary>Weicher Rand der Kontur in Bildpixeln; 0 = scharfe Kontur.</summary>
    [ObservableProperty]
    private double _outlineBlur;

    private double _rotation;

    /// <summary>Keine Kontur (Weiß, Stärke 0) – Standard neuer Bilder.</summary>
    public static StrokeStyle NoOutline { get; } = new(ArgbColor.White, 0);

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

    [JsonIgnore]
    public PointD Center => Rect.Normalize().Center;

    [JsonIgnore]
    public override RectD Bounds => RotationMath.Hull(Rect, Rotation);

    /// <summary>Die Kontur reicht um ihre Stärke plus Unschärfe über die deckenden Bildteile hinaus.</summary>
    [JsonIgnore]
    public override double VisualMargin => Outline.IsVisible
        ? Math.Clamp(Outline.Thickness, 0, MaxOutlineThickness) + Math.Clamp(OutlineBlur, 0, MaxOutlineBlur)
        : 0;

    /// <summary>Seitenverhältnis der Originalgröße (Breite / Höhe); 1, wenn unbekannt.</summary>
    [JsonIgnore]
    public double AspectRatio => OriginalSize.IsEmpty ? 1 : OriginalSize.Width / OriginalSize.Height;

    public override void MapPoints(Func<PointD, PointD> map) =>
        Rect = RectD.FromPoints(map(Rect.TopLeft), map(Rect.BottomRight));

    public override bool HitTest(PointD point, double tolerance)
    {
        var local = RotationMath.Unrotate(point, Center, Rotation);
        return Rect.Normalize().Inflate(tolerance, tolerance).Contains(local);
    }

    /// <summary>
    /// Rahmen in <paramref name="size"/>, proportional in <paramref name="bounds"/> eingepasst und darin zentriert
    /// (für Einfügen in einen aufgezogenen Bereich).
    /// </summary>
    public static RectD FitInto(SizeD size, RectD bounds)
    {
        var target = bounds.Normalize();
        if (size.IsEmpty || target.Width <= 0 || target.Height <= 0)
        {
            return target;
        }

        var scale = Math.Min(target.Width / size.Width, target.Height / size.Height);
        var width = size.Width * scale;
        var height = size.Height * scale;
        var center = target.Center;
        return new RectD(center.X - (width / 2), center.Y - (height / 2), width, height);
    }

    /// <summary>
    /// Rahmen beim Einfügen ohne aufgezogenen Bereich: Originalgröße um <paramref name="center"/>; ist das Bild breiter oder
    /// höher als <paramref name="maxFraction"/> der Fläche <paramref name="canvas"/>, wird es proportional darauf verkleinert.
    /// </summary>
    public static RectD PlaceAt(SizeD size, PointD center, SizeD canvas, double maxFraction = 0.8)
    {
        var width = Math.Max(size.Width, MinSize);
        var height = Math.Max(size.Height, MinSize);
        if (!canvas.IsEmpty)
        {
            var scale = Math.Min(1, Math.Min(canvas.Width * maxFraction / width, canvas.Height * maxFraction / height));
            width = Math.Max(MinSize, width * scale);
            height = Math.Max(MinSize, height * scale);
        }

        return new RectD(center.X - (width / 2), center.Y - (height / 2), width, height);
    }
}
