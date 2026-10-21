using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Rendering;

/// <summary>Kontext für das Zeichnen. <see cref="Zoom"/> dient nur UI-Hilfslinien; <see cref="BaseImage"/> ab M8 für Unschärfe/Lupe.</summary>
public sealed record RenderContext
{
    public double Zoom { get; init; } = 1;

    public BitmapSource? BaseImage { get; init; }

    /// <summary>Pixel des Basisbilds (Quelle der Unschärfe-Effekte).</summary>
    public Core.Imaging.PixelBuffer? BasePixels { get; init; }

    /// <summary>Alle Objekte des Dokuments in Z-Reihenfolge (die Lupe zeigt die Objekte unter sich).</summary>
    public IReadOnlyList<Annotation>? Annotations { get; init; }

    /// <summary>Textmarker-Striche hinter allen übrigen Objekten zeichnen.</summary>
    public bool HighlightersBehind { get; init; } = true;

    public EffectCache Effects { get; init; } = EffectCache.Shared;

    /// <summary>Geräte-Pixel je Bildpixel (Zoom × DPI in der Anzeige, 1 im Export): bestimmt die Rastergröße von Emojis.</summary>
    public double EmojiPixelScale { get; init; } = 1;

    /// <summary>Größte Rastergröße eines Emojis in Pixeln (Anzeige 2048, Export 4096); darüber wird skaliert.</summary>
    public int EmojiMaxPixels { get; init; } = EmojiRasterizer.MaxPixelSize;

    public EmojiCache Emojis { get; init; } = EmojiCache.Shared;

    /// <summary>Text von Sprechblasen weglassen (während der Textbearbeitung zeigt ein Eingabefeld den Text).</summary>
    public bool HideCalloutText { get; init; }

    public static RenderContext Default { get; } = new();
}

/// <summary>Eingefrorene, gecachte Pinsel und Stifte je Farbe bzw. Strichstil.</summary>
public static class BrushCache
{
    private static readonly ConcurrentDictionary<ArgbColor, SolidColorBrush> Brushes = new();
    private static readonly ConcurrentDictionary<StrokeStyle, Pen> Pens = new();
    private static readonly double[] DashPattern = [4.0, 2.0];
    private static readonly double[] DotPattern = [1.0, 2.0];
    private static readonly double[] DashDotPattern = [4.0, 2.0, 1.0, 2.0];

    public static Color ToColor(ArgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    public static ArgbColor ToArgb(Color color) => new(color.A, color.R, color.G, color.B);

    public static Brush? GetBrush(ArgbColor color)
    {
        if (color.A == 0)
        {
            return null;
        }

        return Brushes.GetOrAdd(color, static c =>
        {
            var brush = new SolidColorBrush(ToColor(c));
            brush.Freeze();
            return brush;
        });
    }

    public static Pen? GetPen(StrokeStyle stroke)
    {
        if (!stroke.IsVisible)
        {
            return null;
        }

        return Pens.GetOrAdd(stroke, static s =>
        {
            var cap = s.Cap switch
            {
                StrokeCapKind.Flat => PenLineCap.Flat,
                StrokeCapKind.Square => PenLineCap.Square,
                _ => PenLineCap.Round,
            };
            var pen = new Pen(GetBrush(s.Color), s.Thickness)
            {
                LineJoin = s.Join switch
                {
                    LineJoinKind.Miter => PenLineJoin.Miter,
                    LineJoinKind.Bevel => PenLineJoin.Bevel,
                    _ => PenLineJoin.Round,
                },
                StartLineCap = cap,
                EndLineCap = cap,
                DashCap = PenLineCap.Flat,
                MiterLimit = 4,
            };
            var dashes = s.Dash switch
            {
                DashKind.Dash => DashPattern,
                DashKind.Dot => DotPattern,
                DashKind.DashDot => DashDotPattern,
                _ => null,
            };
            if (dashes is not null)
            {
                pen.DashStyle = new DashStyle(dashes, 0);
            }

            pen.Freeze();
            return pen;
        });
    }
}

/// <summary>Zeichnet Annotationen. Anzeige im Editor und Export nutzen denselben Pfad.</summary>
public static partial class AnnotationRenderer
{
    static AnnotationRenderer() => TextLayout.Install();

    public static void Render(DrawingContext dc, Annotation annotation, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(annotation);
        context ??= RenderContext.Default;
        if (!annotation.IsVisible)
        {
            return;
        }

        var opacity = Math.Clamp(annotation.Opacity, 0, 1);
        if (opacity <= 0)
        {
            return;
        }

        var pushed = opacity < 1;
        if (pushed)
        {
            dc.PushOpacity(opacity);
        }

        if (annotation is IShadowAnnotation { Shadow: { IsVisible: true } shadow })
        {
            DrawShadow(dc, annotation, shadow, context);
        }

        DrawCore(dc, annotation, context);

        if (pushed)
        {
            dc.Pop();
        }
    }

    /// <summary>Rastergröße weicher Ebenen: wie Emojis (Anzeige Zoom × DPI, Export 1), höchstens <see cref="RenderContext.EmojiMaxPixels"/>.</summary>
    private static double SoftScale(RenderContext context) => Math.Max(context.EmojiPixelScale, 0.01);

    /// <summary>Schatten = Deckung des ganzen Objekts in Schattenfarbe, weichgezeichnet und nach rechts unten versetzt.</summary>
    private static void DrawShadow(DrawingContext dc, Annotation annotation, ShadowStyle shadow, RenderContext context)
    {
        SoftLayer? Create()
        {
            var drawing = new DrawingGroup();
            using (var inner = drawing.Open())
            {
                DrawCore(inner, annotation, context);
            }

            drawing.Freeze();
            return SoftLayer.Create(drawing, shadow.Color, Math.Clamp(shadow.Blur, 0, ShadowStyle.MaxBlur), SoftScale(context), context.EmojiMaxPixels);
        }

        // Ohne Text gezeichnete Sprechblase (Textbearbeitung) nicht cachen, sonst fehlte der Text danach im Schatten.
        var layer = context.HideCalloutText && annotation is CalloutAnnotation
            ? Create()
            : SoftLayerCache.Get(annotation, "shadow", SoftScale(context), Create);
        if (layer is null)
        {
            return;
        }

        var offset = Math.Clamp(shadow.Size, 0, ShadowStyle.MaxSize);
        var rect = layer.Rect;
        rect.Offset(offset, offset);
        dc.DrawImage(layer.Bitmap, rect);
    }

    private static void DrawCore(DrawingContext dc, Annotation annotation, RenderContext context)
    {
        switch (annotation)
        {
            case RectangleAnnotation rectangle:
                DrawRectangle(dc, rectangle);
                break;
            case EllipseAnnotation ellipse:
                DrawEllipse(dc, ellipse);
                break;
            case TriangleAnnotation triangle:
                DrawPolygon(dc, triangle.Points, closed: true, triangle);
                break;
            case PolygonAnnotation polygon:
                DrawPolygon(dc, polygon.Points, polygon.IsClosed, polygon);
                break;
            case LineAnnotation line:
                DrawLine(dc, line);
                break;
            case TextAnnotation text:
                DrawText(dc, text, context);
                break;
            case MarkerAnnotation marker:
                DrawMarker(dc, marker);
                break;
            case StrokeAnnotation stroke:
                DrawStroke(dc, stroke);
                break;
            case BlurAnnotation blur:
                DrawBlur(dc, blur, context);
                break;
            case MagnifierAnnotation magnifier:
                DrawMagnifier(dc, magnifier, context);
                break;
            case EmojiAnnotation emoji:
                DrawEmoji(dc, emoji, context);
                break;
            case StampAnnotation stamp:
                StampRenderer.Draw(dc, stamp, context);
                break;
            case CalloutAnnotation callout:
                CalloutRenderer.Draw(dc, callout, context);
                break;
        }
    }

    private static void DrawEmoji(DrawingContext dc, EmojiAnnotation a, RenderContext context)
    {
        var rect = ToRect(a.Rect);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var pivot = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        var pushes = 0;
        if (a.Rotation != 0)
        {
            dc.PushTransform(new RotateTransform(a.Rotation, pivot.X, pivot.Y));
            pushes++;
        }

        if (a.FlipHorizontal || a.FlipVertical)
        {
            dc.PushTransform(new ScaleTransform(a.FlipHorizontal ? -1 : 1, a.FlipVertical ? -1 : 1, pivot.X, pivot.Y));
            pushes++;
        }

        var wanted = Math.Max(rect.Width, rect.Height) * Math.Max(context.EmojiPixelScale, 0.01);
        var size = EmojiCache.Quantize(wanted, context.EmojiMaxPixels);
        var bitmap = context.Emojis.Get(a.Sequence, size);
        if (bitmap is not null)
        {
            dc.DrawImage(bitmap, rect);
        }
        else
        {
            DrawEmojiPlaceholder(dc, rect);
        }

        for (var i = 0; i < pushes; i++)
        {
            dc.Pop();
        }
    }

    /// <summary>Platzhalter für ein Emoji, das die installierte Schrift nicht darstellen kann: gestrichelter Rahmen mit „?“.</summary>
    private static void DrawEmojiPlaceholder(DrawingContext dc, Rect rect)
    {
        var pen = new Pen(Brushes.Gray, Math.Max(1, Math.Min(rect.Width, rect.Height) / 32))
        {
            DashStyle = new DashStyle([3.0, 2.0], 0),
        };
        pen.Freeze();
        dc.DrawRoundedRectangle(null, pen, rect, 4, 4);
        var fontSize = Math.Max(4, Math.Min(rect.Width, rect.Height) * 0.5);
        var typeface = new Typeface("Segoe UI");
        var text = new FormattedText("?", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Gray, 1.0);
        dc.DrawText(text, new Point(rect.X + ((rect.Width - text.Width) / 2), rect.Y + ((rect.Height - text.Height) / 2)));
    }

    private static void DrawText(DrawingContext dc, TextAnnotation a, RenderContext context)
    {
        if (a.Text.Length == 0)
        {
            return;
        }

        var size = a.LayoutSize;
        var origin = new Point(a.Position.X, a.Position.Y);
        if (a.HasBackground)
        {
            var pad = a.BackgroundPadding;
            var radius = Math.Max(0, a.BackgroundCornerRadius);
            dc.DrawRoundedRectangle(
                BrushCache.GetBrush(a.Background),
                null,
                new Rect(origin.X - pad, origin.Y - pad, size.Width + (2 * pad), size.Height + (2 * pad)),
                radius,
                radius);
        }

        var fill = BrushCache.GetBrush(a.Fill);
        if (a.Stroke.IsVisible)
        {
            // Erst die Kontur (doppelte Stärke, die innere Hälfte verdeckt die Füllung), dann die Füllung.
            var outline = new Pen(BrushCache.GetBrush(a.Stroke.Color), a.Stroke.Thickness * 2)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            outline.Freeze();
            var geometry = TextLayout.Create(a, fill).BuildGeometry(origin);
            if (a.OutlineBlur > 0)
            {
                DrawSoftOutline(dc, a, geometry, context);
            }
            else
            {
                dc.DrawGeometry(null, outline, geometry);
            }

            dc.DrawGeometry(fill, null, geometry);
        }
        else
        {
            dc.DrawText(TextLayout.Create(a, fill), origin);
        }
    }

    /// <summary>Weiche Kontur: Deckung der Kontur (deckend gezeichnet), weichgezeichnet und in Konturfarbe; die Füllung bleibt scharf.</summary>
    private static void DrawSoftOutline(DrawingContext dc, TextAnnotation a, Geometry geometry, RenderContext context)
    {
        var layer = SoftLayerCache.Get(a, "outline", SoftScale(context), () =>
        {
            var pen = new Pen(Brushes.Black, a.Stroke.Thickness * 2)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            pen.Freeze();
            var drawing = new GeometryDrawing(null, pen, geometry);
            drawing.Freeze();
            return SoftLayer.Create(drawing, a.Stroke.Color, Math.Clamp(a.OutlineBlur, 0, TextAnnotation.MaxOutlineBlur), SoftScale(context), context.EmojiMaxPixels);
        });
        if (layer is not null)
        {
            dc.DrawImage(layer.Bitmap, layer.Rect);
        }
    }

    private static void DrawMarker(DrawingContext dc, MarkerAnnotation a)
    {
        var center = new Point(a.Center.X, a.Center.Y);
        var r = a.Radius;
        Geometry body = a.Shape switch
        {
            MarkerShape.Circle => new EllipseGeometry(center, r, r),
            MarkerShape.Square => new RectangleGeometry(new Rect(center.X - r, center.Y - r, a.Size, a.Size)),
            _ => new RectangleGeometry(new Rect(center.X - r, center.Y - r, a.Size, a.Size), a.Size * 0.22, a.Size * 0.22),
        };

        if (a.PointerTarget is { } target)
        {
            // Spitze: Dreieck von einer Querlinie durch den Mittelpunkt zum Ziel; mit dem Körper vereinigt.
            var tip = new Point(target.X, target.Y);
            var dx = tip.X - center.X;
            var dy = tip.Y - center.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length > r)
            {
                var nx = -dy / length;
                var ny = dx / length;
                var half = r * 0.7;
                var tail = new StreamGeometry();
                using (var ctx = tail.Open())
                {
                    ctx.BeginFigure(new Point(center.X + (nx * half), center.Y + (ny * half)), isFilled: true, isClosed: true);
                    ctx.LineTo(tip, false, false);
                    ctx.LineTo(new Point(center.X - (nx * half), center.Y - (ny * half)), false, false);
                }

                tail.Freeze();
                body = new CombinedGeometry(GeometryCombineMode.Union, body, tail);
            }
        }

        body.Freeze();
        dc.DrawGeometry(BrushCache.GetBrush(a.Fill), BrushCache.GetPen(a.Stroke), body);

        var digits = a.Number.ToString(CultureInfo.InvariantCulture);
        var fontSize = a.Size * (digits.Length <= 1 ? 0.55 : digits.Length == 2 ? 0.45 : 0.34);
        var typeface = TextLayout.CreateTypeface(a.FontFamilyName, 700, FontStyleKind.Normal, TextAnnotation.NormalStretch);
        var text = new FormattedText(digits, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, BrushCache.GetBrush(a.TextColor) ?? Brushes.White, 1.0);
        dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - (text.Height / 2)));
    }

    private static Rect ToRect(RectD r)
    {
        var n = r.Normalize();
        return new Rect(n.X, n.Y, n.Width, n.Height);
    }

    private static Point ToPoint(PointD p) => new(p.X, p.Y);

    private static void DrawRectangle(DrawingContext dc, RectangleAnnotation a)
    {
        var rect = ToRect(a.Rect);
        var radius = Math.Min(a.CornerRadius, Math.Min(rect.Width, rect.Height) / 2);
        dc.DrawRoundedRectangle(BrushCache.GetBrush(a.Fill), BrushCache.GetPen(a.Stroke), rect, radius, radius);
    }

    private static void DrawEllipse(DrawingContext dc, EllipseAnnotation a)
    {
        var rect = ToRect(a.Rect);
        dc.DrawEllipse(
            BrushCache.GetBrush(a.Fill),
            BrushCache.GetPen(a.Stroke),
            new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)),
            rect.Width / 2,
            rect.Height / 2);
    }

    private static void DrawPolygon(DrawingContext dc, IReadOnlyList<PointD> points, bool closed, ShapeAnnotation shape)
    {
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(points[0]), isFilled: closed, isClosed: closed);
            for (var i = 1; i < points.Count; i++)
            {
                ctx.LineTo(ToPoint(points[i]), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(closed ? BrushCache.GetBrush(shape.Fill) : null, BrushCache.GetPen(shape.Stroke), geometry);
    }

    private static void DrawLine(DrawingContext dc, LineAnnotation a)
    {
        var pen = BrushCache.GetPen(a.Stroke);
        if (pen is null)
        {
            return;
        }

        var layout = LineLayout.Compute(a.Start, a.End, a.Stroke.Thickness, a.StartCap, a.EndCap, a.CapSize);
        dc.DrawLine(pen, ToPoint(layout.LineStart), ToPoint(layout.LineEnd));

        // Spitzen werden immer durchgezogen gezeichnet (nicht gestrichelt) und mit Rundung verbunden.
        var solid = BrushCache.GetPen(a.Stroke with { Dash = DashKind.Solid, Join = LineJoinKind.Round });
        var fill = BrushCache.GetBrush(a.Stroke.Color);
        DrawCap(dc, layout.StartCap, fill, solid);
        DrawCap(dc, layout.EndCap, fill, solid);
    }

    private static void DrawCap(DrawingContext dc, CapGeometry? cap, Brush? fill, Pen? pen)
    {
        if (cap is null)
        {
            return;
        }

        switch (cap.Style)
        {
            case LineEndStyle.Circle:
                dc.DrawEllipse(fill, null, ToPoint(cap.Center), cap.Radius, cap.Radius);
                break;
            case LineEndStyle.OpenArrow:
                var open = new StreamGeometry();
                using (var ctx = open.Open())
                {
                    ctx.BeginFigure(ToPoint(cap.Points[0]), isFilled: false, isClosed: false);
                    ctx.LineTo(ToPoint(cap.Points[1]), true, false);
                    ctx.LineTo(ToPoint(cap.Points[2]), true, false);
                }

                open.Freeze();
                dc.DrawGeometry(null, pen, open);
                break;
            default:
                var filled = new StreamGeometry();
                using (var ctx = filled.Open())
                {
                    ctx.BeginFigure(ToPoint(cap.Points[0]), isFilled: true, isClosed: true);
                    for (var i = 1; i < cap.Points.Count; i++)
                    {
                        ctx.LineTo(ToPoint(cap.Points[i]), false, false);
                    }
                }

                filled.Freeze();
                dc.DrawGeometry(fill, null, filled);
                break;
        }
    }
}
