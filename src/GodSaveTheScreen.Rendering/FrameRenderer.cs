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

    /// <summary>
    /// Zeichnet Hintergrund (über die ganze Ausgabe), Schatten (falls berechnet) und Rand.
    /// <paramref name="backgroundPixels"/> begrenzt die längste Kante des Hintergrundbilds (Anzeige: kleiner; Export: voll).
    /// </summary>
    public static void DrawBehind(DrawingContext dc, FrameLayout layout, FrameStyle style, SoftLayer? shadow, int backgroundPixels = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(style);
        if (style.Background is { IsVisible: true } background)
        {
            DrawBackground(dc, layout.Output, background, backgroundPixels);
        }

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

    /// <summary>
    /// Füllt <paramref name="area"/> (Bildkoordinaten) mit dem Hintergrund: Farbe, Verlauf (die Farben enden genau in den Ecken
    /// bzw. an den Kanten) oder Bild (füllt die Fläche, Seitenverhältnis bleibt, Überstand wird zentriert beschnitten).
    /// </summary>
    public static void DrawBackground(DrawingContext dc, PixelRect area, FrameBackground background, int imagePixels = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(background);
        var rect = ToRect(area.ToRectD());
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        switch (background.Kind)
        {
            case FrameBackgroundKind.Solid:
                dc.DrawRectangle(BrushCache.GetBrush(background.ColorA), null, rect);
                break;
            case FrameBackgroundKind.Gradient:
                dc.DrawRectangle(CreateGradientBrush(rect, background), null, rect);
                break;
            case FrameBackgroundKind.Image when background.Image is { } asset:
                DrawBackgroundImage(dc, rect, asset, imagePixels);
                break;
        }
    }

    /// <summary>
    /// Verlaufspinsel in absoluten Koordinaten: Die Verlaufslinie läuft in <see cref="FrameBackground.AngleDegrees"/>-Richtung
    /// durch die Mitte; ihre Länge ist so gewählt, dass die Endfarben genau in den Ecken liegen.
    /// </summary>
    public static LinearGradientBrush CreateGradientBrush(Rect rect, FrameBackground background)
    {
        ArgumentNullException.ThrowIfNull(background);
        var radians = background.AngleDegrees * Math.PI / 180;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);
        var length = Math.Abs(rect.Width * dx) + Math.Abs(rect.Height * dy);
        var center = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var brush = new LinearGradientBrush(BrushCache.ToColor(background.ColorA), BrushCache.ToColor(background.ColorB), 0)
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(center.X - (dx * length / 2), center.Y - (dy * length / 2)),
            EndPoint = new Point(center.X + (dx * length / 2), center.Y + (dy * length / 2)),
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Zielrechteck eines Bilds, das <paramref name="area"/> ausfüllt (Seitenverhältnis bleibt, zentriert).</summary>
    public static Rect FillRect(Rect area, double imageWidth, double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return area;
        }

        var scale = Math.Max(area.Width / imageWidth, area.Height / imageHeight);
        var width = imageWidth * scale;
        var height = imageHeight * scale;
        return new Rect(area.X + ((area.Width - width) / 2), area.Y + ((area.Height - height) / 2), width, height);
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

    /// <summary>Längste Kante des Hintergrundbilds in der Anzeige.</summary>
    public const int PreviewBackgroundPixels = 4096;

    private static void DrawBackgroundImage(DrawingContext dc, Rect area, ImageAsset asset, int maxPixels)
    {
        var cache = ImageAssetCache.Shared;
        BitmapSource? bitmap;
        double width;
        double height;
        if (asset.Format == ImageAssetFormat.Svg)
        {
            if (cache.SvgSize(asset) is not { } size || size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            // SVG in der Zielgröße rastern (Anzeige begrenzt).
            var target = FillRect(area, size.Width, size.Height);
            var longest = Math.Max(target.Width, target.Height);
            var factor = Math.Min(1, Math.Min(maxPixels, SvgRasterizer.MaxPixelSize) / Math.Max(longest, 1));
            bitmap = cache.Get(asset, Math.Max(1, (int)Math.Round(target.Width * factor)), Math.Max(1, (int)Math.Round(target.Height * factor)));
            width = size.Width;
            height = size.Height;
        }
        else
        {
            bitmap = cache.Get(asset, 0, 0);
            if (bitmap is not null && maxPixels < int.MaxValue)
            {
                bitmap = BackgroundPreviews.Downscale(asset, bitmap, maxPixels);
            }

            width = bitmap?.PixelWidth ?? 0;
            height = bitmap?.PixelHeight ?? 0;
        }

        if (bitmap is null)
        {
            return;
        }

        var drawn = FillRect(area, width, height);
        var group = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);
        using (var inner = group.Open())
        {
            inner.DrawImage(bitmap, drawn);
        }

        group.Freeze();
        dc.PushClip(new RectangleGeometry(area));
        dc.DrawDrawing(group);
        dc.Pop();
    }

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

/// <summary>Verkleinerte Kopie des Hintergrundbilds für die Anzeige (eine Kopie; der Export nutzt das Original).</summary>
internal static class BackgroundPreviews
{
    private static readonly object Gate = new();
    private static (string Hash, int Pixels, BitmapSource Bitmap)? _last;

    public static BitmapSource Downscale(ImageAsset asset, BitmapSource source, int maxPixels)
    {
        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longest <= maxPixels)
        {
            return source;
        }

        lock (Gate)
        {
            if (_last is { } last && last.Hash == asset.Hash && last.Pixels == maxPixels)
            {
                return last.Bitmap;
            }
        }

        var scale = (double)maxPixels / longest;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        var stride = scaled.PixelWidth * 4;
        var pixels = new byte[stride * scaled.PixelHeight];
        scaled.CopyPixels(pixels, stride, 0);
        var copy = BitmapSource.Create(scaled.PixelWidth, scaled.PixelHeight, 96, 96, scaled.Format, null, pixels, stride);
        copy.Freeze();
        lock (Gate)
        {
            _last = (asset.Hash, maxPixels, copy);
        }

        return copy;
    }
}
