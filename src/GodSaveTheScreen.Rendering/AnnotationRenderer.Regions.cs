using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Rendering;

/// <summary>Zeichenreihenfolge: Textmarker-Striche optional hinter allen übrigen Objekten, sonst Z-Reihenfolge des Dokuments.</summary>
public static class AnnotationOrdering
{
    public static bool IsHighlighter(Annotation a) => a is StrokeAnnotation { IsHighlighter: true };

    public static IReadOnlyList<Annotation> RenderOrder(IEnumerable<Annotation> annotations, bool highlightersBehind)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var list = annotations.ToList();
        return highlightersBehind
            ? [.. list.Where(IsHighlighter), .. list.Where(a => !IsHighlighter(a))]
            : list;
    }
}

/// <summary>Freihandstriche, Unschärfe-Bereiche und Lupe.</summary>
public static partial class AnnotationRenderer
{
    private const int CurveSteps = 8;
    private const int CapSteps = 8;
    private const double MinimumPressureWidth = 0.1;

    // ---- Freihand ----

    private static void DrawStroke(DrawingContext dc, StrokeAnnotation a)
    {
        var points = a.Points;
        var thickness = a.Stroke.Thickness;
        var color = a.Stroke.Color;
        if (points.IsDefaultOrEmpty || thickness <= 0 || color.A == 0)
        {
            return;
        }

        // Deckkraft über PushOpacity auf deckender Farbe: Überlappungen innerhalb eines Strichs bleiben gleichmäßig.
        var opaque = color with { A = 255 };
        var pushed = color.A < 255;
        if (pushed)
        {
            dc.PushOpacity(color.A / 255.0);
        }

        var brush = BrushCache.GetBrush(opaque);
        if (points.Length == 1)
        {
            DrawDot(dc, points[0], thickness, brush, a.Stroke.Cap == StrokeCapKind.Square);
        }
        else if (a.HasPressure)
        {
            dc.DrawGeometry(brush, null, BuildPressureOutline(points, a.Smoothing, thickness));
        }
        else
        {
            dc.DrawGeometry(null, BrushCache.GetPen(a.Stroke with { Color = opaque, Dash = DashKind.Solid, Join = LineJoinKind.Round }), BuildCenterline(points, a.Smoothing));
        }

        if (pushed)
        {
            dc.Pop();
        }
    }

    private static void DrawDot(DrawingContext dc, StrokePoint p, double thickness, Brush? brush, bool square)
    {
        var width = thickness * Math.Max(MinimumPressureWidth, p.Pressure);
        if (square)
        {
            dc.DrawRectangle(brush, null, new Rect(p.X - (width / 2), p.Y - (width / 2), width, width));
        }
        else
        {
            dc.DrawEllipse(brush, null, new Point(p.X, p.Y), width / 2, width / 2);
        }
    }

    private static (Point C1, Point C2) Controls(IReadOnlyList<StrokePoint> pts, int i, double smoothing)
    {
        var p0 = pts[Math.Max(i - 1, 0)];
        var p1 = pts[i];
        var p2 = pts[i + 1];
        var p3 = pts[Math.Min(i + 2, pts.Count - 1)];
        var k = Math.Clamp(smoothing, 0, 1) / 6;
        return (
            new Point(p1.X + ((p2.X - p0.X) * k), p1.Y + ((p2.Y - p0.Y) * k)),
            new Point(p2.X - ((p3.X - p1.X) * k), p2.Y - ((p3.Y - p1.Y) * k)));
    }

    /// <summary>Catmull-Rom-Kurve durch alle Punkte als kubische Bézier-Segmente.</summary>
    private static StreamGeometry BuildCenterline(ImmutableArray<StrokePoint> pts, double smoothing)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), isFilled: false, isClosed: false);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var end = new Point(pts[i + 1].X, pts[i + 1].Y);
                if (smoothing < 0.01)
                {
                    ctx.LineTo(end, isStroked: true, isSmoothJoin: false);
                }
                else
                {
                    var (c1, c2) = Controls(pts, i, smoothing);
                    ctx.BezierTo(c1, c2, end, isStroked: true, isSmoothJoin: true);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Umriss eines Strichs mit druckabhängiger Breite (Normale je Stützpunkt), Kappen rund; ein geschlossener Pfad.</summary>
    private static StreamGeometry BuildPressureOutline(ImmutableArray<StrokePoint> pts, double smoothing, double thickness)
    {
        var samples = new List<(Point P, double Width)>();
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var (c1, c2) = Controls(pts, i, smoothing);
            var a = new Point(pts[i].X, pts[i].Y);
            var b = new Point(pts[i + 1].X, pts[i + 1].Y);
            var w1 = thickness * Math.Max(MinimumPressureWidth, pts[i].Pressure);
            var w2 = thickness * Math.Max(MinimumPressureWidth, pts[i + 1].Pressure);
            var steps = smoothing < 0.01 ? 1 : CurveSteps;
            for (var s = 0; s < steps; s++)
            {
                var t = (double)s / steps;
                var u = 1 - t;
                var x = (u * u * u * a.X) + (3 * u * u * t * c1.X) + (3 * u * t * t * c2.X) + (t * t * t * b.X);
                var y = (u * u * u * a.Y) + (3 * u * u * t * c1.Y) + (3 * u * t * t * c2.Y) + (t * t * t * b.Y);
                samples.Add((new Point(x, y), w1 + ((w2 - w1) * t)));
            }
        }

        var last = pts[^1];
        samples.Add((new Point(last.X, last.Y), thickness * Math.Max(MinimumPressureWidth, last.Pressure)));

        var left = new List<Point>(samples.Count);
        var right = new List<Point>(samples.Count);
        var normals = new List<Vector>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var prev = samples[Math.Max(i - 1, 0)].P;
            var next = samples[Math.Min(i + 1, samples.Count - 1)].P;
            var d = next - prev;
            if (d.Length < 1e-9)
            {
                d = i > 0 ? samples[i].P - samples[i - 1].P : new Vector(1, 0);
                if (d.Length < 1e-9)
                {
                    d = new Vector(1, 0);
                }
            }

            d.Normalize();
            var n = new Vector(-d.Y, d.X);
            normals.Add(n);
            var half = samples[i].Width / 2;
            left.Add(samples[i].P + (n * half));
            right.Add(samples[i].P - (n * half));
        }

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(left[0], isFilled: true, isClosed: true);
            for (var i = 1; i < left.Count; i++)
            {
                ctx.LineTo(left[i], false, false);
            }

            AddCap(ctx, samples[^1].P, normals[^1], samples[^1].Width / 2, forward: true);
            for (var i = right.Count - 1; i >= 0; i--)
            {
                ctx.LineTo(right[i], false, false);
            }

            AddCap(ctx, samples[0].P, normals[0], samples[0].Width / 2, forward: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AddCap(StreamGeometryContext ctx, Point center, Vector normal, double radius, bool forward)
    {
        // Tangente ist die um 90° zurückgedrehte Normale: (n.Y, -n.X).
        var tangent = new Vector(normal.Y, -normal.X);
        var sign = forward ? 1 : -1;
        for (var i = 1; i < CapSteps; i++)
        {
            var theta = Math.PI * i / CapSteps;
            var v = (normal * (sign * Math.Cos(theta))) + (tangent * Math.Sin(theta));
            ctx.LineTo(center + (v * radius), false, false);
        }
    }

    // ---- Unschärfe ----

    private static void DrawBlur(DrawingContext dc, BlurAnnotation a, RenderContext context)
    {
        if (context.BasePixels is not { } pixels)
        {
            return;
        }

        var region = RegionEffectProcessor.RegionOf(a, pixels.Width, pixels.Height);
        if (region.IsEmpty)
        {
            return;
        }

        var hard = a.Shape == RegionShape.Rectangle && a.FeatherEdge <= 0;
        var rect = a.Rect.Normalize();
        var strength = a.Mode == BlurMode.Redact ? 0 : (int)Math.Round(Math.Clamp(a.Strength, BlurAnnotation.MinStrength, BlurAnnotation.MaxStrength));
        var key = new EffectKey(
            BaseImageIdentity.Get(pixels),
            region,
            hard ? default : rect,
            a.Mode,
            strength,
            a.Shape,
            hard ? 0 : a.FeatherEdge,
            a.Mode == BlurMode.Redact ? a.RedactColor : default);
        var bitmap = context.Effects.GetOrCreate(key, () =>
            RegionEffectProcessor.Process(pixels, region, a.Mode, a.Strength, a.Shape, a.FeatherEdge, a.RedactColor, rect).ToBitmapSource());
        dc.DrawImage(bitmap, new Rect(region.X, region.Y, region.Width, region.Height));
    }

    // ---- Lupe ----

    private static Geometry ShapeGeometry(MagnifierShape shape, Rect rect) =>
        shape == MagnifierShape.Circle ? new EllipseGeometry(rect) : new RectangleGeometry(rect);

    private static void DrawMagnifier(DrawingContext dc, MagnifierAnnotation a, RenderContext context)
    {
        var source = ToRect(a.SourceRect);
        var target = ToRect(a.TargetRect);
        if (source.Width < 1 || source.Height < 1 || target.Width < 1 || target.Height < 1)
        {
            return;
        }

        var border = BrushCache.GetPen(a.Border);
        var targetShape = ShapeGeometry(a.Shape, target);

        if (a.ShowConnector)
        {
            var tangents = MagnifierGeometry.ConnectorTangents(
                a.SourceRect.Normalize().Center, MagnifierGeometry.InscribedRadius(a.SourceRect),
                a.TargetRect.Center, MagnifierGeometry.InscribedRadius(a.TargetRect));
            if (tangents is { } t)
            {
                var thin = BrushCache.GetPen(a.Border with { Thickness = Math.Max(1, a.Border.Thickness / 2), Dash = DashKind.Solid });
                dc.DrawLine(thin, new Point(t.First.From.X, t.First.From.Y), new Point(t.First.To.X, t.First.To.Y));
                dc.DrawLine(thin, new Point(t.Second.From.X, t.Second.From.Y), new Point(t.Second.To.X, t.Second.To.Y));
            }
        }

        if (a.ShowSourceFrame)
        {
            var frame = BrushCache.GetPen(a.Border with { Thickness = Math.Max(1, a.Border.Thickness / 2), Dash = DashKind.Dash });
            dc.DrawGeometry(null, frame, ShapeGeometry(a.Shape, source));
        }

        if (context.BaseImage is { } baseImage)
        {
            var content = new DrawingGroup();
            using (var g = content.Open())
            {
                g.DrawImage(baseImage, new Rect(0, 0, baseImage.PixelWidth, baseImage.PixelHeight));
                if (!a.BaseImageOnly && context.Annotations is { } all)
                {
                    foreach (var below in LowerAnnotations(a, all, context.HighlightersBehind))
                    {
                        Render(g, below, context);
                    }
                }
            }

            var zoom = a.Zoom;
            content.Transform = new MatrixTransform(zoom, 0, 0, zoom, target.X - (source.X * zoom), target.Y - (source.Y * zoom));
            RenderOptions.SetBitmapScalingMode(content, a.SmoothScaling ? BitmapScalingMode.HighQuality : BitmapScalingMode.NearestNeighbor);
            content.Freeze();

            dc.PushClip(targetShape);
            dc.DrawDrawing(content);
            dc.Pop();
        }

        dc.DrawGeometry(null, border, targetShape);
    }

    /// <summary>Objekte unter der Lupe (ohne andere Lupen: kein Zyklus, keine exponentielle Rekursion).</summary>
    private static IEnumerable<Annotation> LowerAnnotations(MagnifierAnnotation magnifier, IReadOnlyList<Annotation> all, bool highlightersBehind)
    {
        var ordered = AnnotationOrdering.RenderOrder(all, highlightersBehind);
        var index = -1;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ReferenceEquals(ordered[i], magnifier))
            {
                index = i;
                break;
            }
        }

        // Nicht im Dokument (Vorschau beim Zeichnen): alles liegt darunter.
        var count = index < 0 ? ordered.Count : index;
        for (var i = 0; i < count; i++)
        {
            if (ordered[i] is not MagnifierAnnotation && ordered[i].IsVisible)
            {
                yield return ordered[i];
            }
        }
    }
}
