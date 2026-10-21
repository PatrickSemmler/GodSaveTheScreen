using System.Windows;
using System.Windows.Media;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Zeichnet Sprechblasen: Körper und Spitze als eine Fläche, Rahmen außen herum (scharf oder als weiches Leuchten),
/// darin der umbrochene Text.
/// </summary>
public static class CalloutRenderer
{
    /// <summary>Umriss von Körper und Spitze als eine Fläche (Bildpixel, unverdrehtes System des Körpers).</summary>
    public static Geometry BuildOutline(CalloutAnnotation a)
    {
        ArgumentNullException.ThrowIfNull(a);
        var body = BuildBody(a);
        Geometry outline = body;
        switch (a.TailGeometry)
        {
            case { IsBubbles: true } bubbles:
                foreach (var bubble in bubbles.Bubbles)
                {
                    outline = new CombinedGeometry(GeometryCombineMode.Union, outline, new EllipseGeometry(ToPoint(bubble.Center), bubble.Radius, bubble.Radius));
                }

                break;
            case { } tail:
                outline = new CombinedGeometry(GeometryCombineMode.Union, outline, BuildTail(tail));
                break;
        }

        outline.Freeze();
        return outline;
    }

    public static void Draw(DrawingContext dc, CalloutAnnotation a, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(context);
        var rect = a.Rect.Normalize();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        // Umriss, Spitze und Text liegen im unverdrehten System des Körpers; gedreht wird beim Zeichnen.
        var rotated = a.Rotation != 0;
        if (rotated)
        {
            dc.PushTransform(new RotateTransform(a.Rotation, a.Center.X, a.Center.Y));
        }

        DrawUnrotated(dc, a, context);
        if (rotated)
        {
            dc.Pop();
        }
    }

    private static void DrawUnrotated(DrawingContext dc, CalloutAnnotation a, RenderContext context)
    {
        var outline = BuildOutline(a);
        if (a.Border.IsVisible)
        {
            if (a.BorderBlur > 0)
            {
                DrawGlow(dc, a, outline, context);
            }
            else
            {
                dc.DrawGeometry(BrushCache.GetBrush(a.Border.Color), null, BuildRing(outline, a.Border.Thickness));
            }
        }

        dc.DrawGeometry(BrushCache.GetBrush(a.Background), null, outline);

        if (!context.HideCalloutText && a.Text.Length > 0)
        {
            var block = a.CreateTextBlock();
            dc.DrawText(TextLayout.Create(block, BrushCache.GetBrush(block.Fill) ?? Brushes.Transparent), ToPoint(block.Position));
        }
    }

    /// <summary>Rahmen außen um die Fläche: verbreiterter Umriss ohne die Fläche selbst (auch bei halbtransparentem Hintergrund sauber).</summary>
    private static PathGeometry BuildRing(Geometry outline, double thickness)
    {
        var pen = new Pen(Brushes.Black, thickness * 2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        var ring = Geometry.Combine(outline.GetWidenedPathGeometry(pen), outline, GeometryCombineMode.Exclude, null);
        ring.Freeze();
        return ring;
    }

    /// <summary>Leuchten: Rahmen weichgezeichnet in Rahmenfarbe (gecacht je Objekt, verworfen bei jeder Änderung).</summary>
    private static void DrawGlow(DrawingContext dc, CalloutAnnotation a, Geometry outline, RenderContext context)
    {
        var scale = Math.Max(context.EmojiPixelScale, 0.01);
        var layer = SoftLayerCache.Get(a, "outline", scale, () =>
        {
            var drawing = new GeometryDrawing(Brushes.Black, null, BuildRing(outline, a.Border.Thickness));
            drawing.Freeze();
            return SoftLayer.Create(drawing, a.Border.Color, Math.Clamp(a.BorderBlur, 0, CalloutAnnotation.MaxBorderBlur), scale, context.EmojiMaxPixels);
        });
        if (layer is not null)
        {
            dc.DrawImage(layer.Bitmap, layer.Rect);
        }
    }

    private static Geometry BuildBody(CalloutAnnotation a)
    {
        var r = a.Rect.Normalize();
        var rect = new Rect(r.X, r.Y, r.Width, r.Height);
        switch (a.Shape)
        {
            case CalloutShape.Ellipse:
                return new EllipseGeometry(rect);
            case CalloutShape.Cloud:
                var points = CalloutGeometry.CloudPoints(r, out var arcFactor);
                var cloud = new StreamGeometry();
                using (var ctx = cloud.Open())
                {
                    ctx.BeginFigure(ToPoint(points[0]), isFilled: true, isClosed: true);
                    for (var i = 1; i <= points.Length; i++)
                    {
                        var from = points[i - 1];
                        var to = points[i % points.Length];
                        var radius = from.DistanceTo(to) * arcFactor;
                        ctx.ArcTo(ToPoint(to), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, true);
                    }
                }

                return cloud;
            default:
                var corner = CalloutGeometry.EffectiveCornerRadius(r, a.CornerRadius);
                return new RectangleGeometry(rect, corner, corner);
        }
    }

    private static StreamGeometry BuildTail(CalloutTail tail)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(tail.BaseStart), isFilled: true, isClosed: true);
            if (tail.ControlStart is { } c1)
            {
                ctx.QuadraticBezierTo(ToPoint(c1), ToPoint(tail.Tip), true, true);
            }
            else
            {
                ctx.LineTo(ToPoint(tail.Tip), true, false);
            }

            if (tail.ControlEnd is { } c2)
            {
                ctx.QuadraticBezierTo(ToPoint(c2), ToPoint(tail.BaseEnd), true, true);
            }
            else
            {
                ctx.LineTo(ToPoint(tail.BaseEnd), true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point ToPoint(PointD p) => new(p.X, p.Y);
}
