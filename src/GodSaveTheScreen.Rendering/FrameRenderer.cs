using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Zeichnet Rahmen und Kanteneffekte eines Dokuments (Anzeige und Export gleich). Stapelfolge von außen nach innen:
/// Schatten, Rand, Inhalt mit Clip, Vignette. Alle Koordinaten sind Bildkoordinaten (Pixel des Basisbilds).
/// </summary>
public static class FrameRenderer
{
    /// <summary>Längste Kante des Schattens in der Anzeige (der Export rechnet im Maßstab 1).</summary>
    public const int PreviewShadowPixels = 2048;

    /// <summary>
    /// Umriss des Inhalts mit runden Ecken und abgerissenen Kanten; <c>null</c>, wenn der Inhalt ein einfaches Rechteck bleibt.
    /// </summary>
    public static Geometry? CreateContentClip(FrameLayout layout, FrameStyle? style)
    {
        if (style is null || (!style.HasCorners && !style.HasTorn))
        {
            return null;
        }

        return ContentShape(layout, style);
    }

    /// <summary>Rand als Ring um den Inhalt (Fläche des verbreiterten Umrisses abzüglich des Inhalts); <c>null</c> ohne Rand.</summary>
    public static Geometry? CreateBorder(FrameLayout layout, FrameStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!style.HasBorder)
        {
            return null;
        }

        var content = ContentShape(layout, style);
        Geometry outer;
        if (style.HasTorn)
        {
            // Einheitliche Breite: Umriss um den Rand verbreitern (runde Gehrung folgt auch den Zacken).
            var width = layout.Left;
            var pen = new Pen(Brushes.Black, width * 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var widened = content.GetWidenedPathGeometry(pen);
            outer = new CombinedGeometry(GeometryCombineMode.Union, content, widened);
        }
        else
        {
            var border = layout.Border.ToRectD();
            var smallest = Math.Min(Math.Min(layout.Left, layout.Top), Math.Min(layout.Right, layout.Bottom));
            var radius = style.HasCorners ? FrameGeometry.ClampRadius(border, style.CornerRadius + smallest) : 0;
            outer = new RectangleGeometry(ToRect(border), radius, radius);
        }

        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, outer, content);
        ring.Freeze();
        return ring;
    }

    /// <summary>Zeichnet Schatten (falls berechnet) und Rand.</summary>
    public static void DrawBehind(DrawingContext dc, FrameLayout layout, FrameStyle style, SoftLayer? shadow)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(style);
        if (shadow is not null && style.HasShadow)
        {
            var offset = Math.Max(0, style.Shadow.Size);
            dc.DrawImage(shadow.Bitmap, new Rect(shadow.Rect.X + offset, shadow.Rect.Y + offset, shadow.Rect.Width, shadow.Rect.Height));
        }

        if (CreateBorder(layout, style) is { } ring)
        {
            dc.DrawGeometry(BrushCache.GetBrush(style.BorderColor), null, ring);
        }
    }

    /// <summary>Zeichnet die Vignette über den Inhalt (im Inhaltsclip).</summary>
    public static void DrawOver(DrawingContext dc, FrameLayout layout, FrameStyle style)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(style);
        if (!style.HasVignette)
        {
            return;
        }

        var rect = ToRect(layout.Content.ToRectD());
        var inner = Math.Clamp(1 - style.VignetteSize, 0, 0.99);
        var strength = (byte)Math.Round(Math.Clamp(style.VignetteStrength, 0, 1) * 255);
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),

            // Der Kreis berührt die Ecken des Rechtecks (halbe Diagonale = 0,7071 der Seite).
            RadiusX = Math.Sqrt(0.5),
            RadiusY = Math.Sqrt(0.5),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(0, 0, 0, 0), inner),
                new GradientStop(Color.FromArgb((byte)Math.Round(strength * 0.3), 0, 0, 0), (inner + 1) / 2),
                new GradientStop(Color.FromArgb(strength, 0, 0, 0), 1),
            ],
        };
        brush.Freeze();
        var clip = CreateContentClip(layout, style);
        if (clip is not null)
        {
            dc.PushClip(clip);
        }

        dc.DrawRectangle(brush, null, rect);
        if (clip is not null)
        {
            dc.Pop();
        }
    }

    /// <summary>
    /// Schatten des Rahmens: Deckung von Rand und Inhalt (<paramref name="content"/> = gerenderter Inhalt über
    /// <c>layout.Content</c>) als weiche Ebene. <c>null</c> ohne Schatten. Die Ebene liegt unversetzt; <see cref="DrawBehind"/>
    /// verschiebt sie um die Schattengröße.
    /// </summary>
    public static SoftLayer? CreateShadow(BitmapSource content, FrameLayout layout, FrameStyle style, double scale, int maxPixels)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(style);
        if (!style.HasShadow)
        {
            return null;
        }

        var silhouette = new DrawingGroup();
        using (var dc = silhouette.Open())
        {
            if (CreateBorder(layout, style) is { } ring)
            {
                dc.DrawGeometry(Brushes.Black, null, ring);
            }

            var clip = CreateContentClip(layout, style);
            if (clip is not null)
            {
                dc.PushClip(clip);
            }

            dc.DrawImage(content, ToRect(layout.Content.ToRectD()));
            if (clip is not null)
            {
                dc.Pop();
            }
        }

        silhouette.Freeze();
        return SoftLayer.Create(silhouette, style.Shadow.Color, style.Shadow.Blur, scale, maxPixels);
    }

    /// <summary>Setzt den Rahmen um den gerenderten Inhalt (Export). Ergebnis ist <c>layout.Output</c> groß.</summary>
    public static PixelBuffer Compose(PixelBuffer content, PixelRect contentBounds, FrameStyle style)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(style);
        var layout = FrameLayout.Compute(contentBounds, style);
        var bitmap = content.ToBitmapSource();
        var shadow = CreateShadow(bitmap, layout, style, 1, ExportShadowPixels);

        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-layout.Output.X, -layout.Output.Y));
            DrawBehind(dc, layout, style, shadow);
            var clip = CreateContentClip(layout, style);
            if (clip is not null)
            {
                dc.PushClip(clip);
            }

            dc.DrawImage(bitmap, ToRect(layout.Content.ToRectD()));
            if (clip is not null)
            {
                dc.Pop();
            }

            DrawOver(dc, layout, style);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(layout.Output.Width, layout.Output.Height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return PixelBufferExtensions.FromBitmapSource(target);
    }

    private const int ExportShadowPixels = 32768;

    private static Geometry ContentShape(FrameLayout layout, FrameStyle style)
    {
        var rect = layout.Content.ToRectD();
        Geometry shape = new RectangleGeometry(ToRect(rect));
        var rounded = style.HasCorners ? FrameGeometry.ClampRadius(rect, style.CornerRadius) : 0;
        if (rounded > 0)
        {
            shape = new RectangleGeometry(ToRect(rect), rounded, rounded);
        }

        if (style.HasTorn)
        {
            var outline = FrameGeometry.TornOutline(rect, style.TornSides, style.TornHeight, style.TornWidth, style.TornSeed);
            var figure = new PathFigure { StartPoint = new Point(outline[0].X, outline[0].Y), IsClosed = true, IsFilled = true };
            for (var i = 1; i < outline.Count; i++)
            {
                figure.Segments.Add(new LineSegment(new Point(outline[i].X, outline[i].Y), isStroked: true));
            }

            var torn = new PathGeometry([figure]);
            shape = rounded > 0 ? new CombinedGeometry(GeometryCombineMode.Intersect, shape, torn) : torn;
        }

        shape.Freeze();
        return shape;
    }

    private static Rect ToRect(RectD rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
