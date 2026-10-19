using System.Windows;
using System.Windows.Media;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Ebene über den Objekten: Vorschau beim Zeichnen, Auswahlrahmen, Griffe, Aufziehrahmen.
/// Wird nicht exportiert. Linien und Griffe haben konstante Bildschirmgröße (Zoom-unabhängig).
/// </summary>
public sealed class InteractionLayer : FrameworkElement
{
    private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)));
    private static readonly Brush BandBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x78, 0xD4)));
    private static readonly Brush HandleFill = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush LockedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)));

    private static readonly Brush RulerFill = Freeze(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xE8, 0x9A)));
    private static readonly Brush RulerInk = Freeze(new SolidColorBrush(Color.FromRgb(0x4A, 0x3B, 0x00)));
    private static readonly Brush LabelFill = Freeze(new SolidColorBrush(Color.FromArgb(0xD8, 0x20, 0x20, 0x20)));
    private static readonly Brush DangerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)));
    private static readonly double[] LabelSteps = [100, 200, 500, 1000, 2000, 5000, 10000];

    private EditorContext? _context;

    public InteractionLayer() => IsHitTestVisible = false;

    /// <summary>Kontext für Vorschauobjekte (Basisbild für Unschärfe und Lupe).</summary>
    public RenderContext RenderContext { get; set; } = RenderContext.Default;

    public void Attach(EditorContext context)
    {
        Detach();
        _context = context;
        context.InteractionInvalidated += OnInvalidated;
        context.Selection.Changed += OnInvalidated;
        context.Ruler.PropertyChanged += OnInvalidated;
        InvalidateVisual();
    }

    public void Detach()
    {
        if (_context is null)
        {
            return;
        }

        _context.InteractionInvalidated -= OnInvalidated;
        _context.Selection.Changed -= OnInvalidated;
        _context.Ruler.PropertyChanged -= OnInvalidated;
        _context = null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_context is null)
        {
            return;
        }

        var unit = _context.ToImageUnits(1);

        if (_context.Interaction.Preview is { } preview)
        {
            AnnotationRenderer.Render(drawingContext, preview, RenderContext);
        }

        DrawMarked(drawingContext, unit);
        DrawSelection(drawingContext, unit);
        DrawRuler(drawingContext, unit);
        DrawBrushPreview(drawingContext, unit);

        if (_context.Interaction.RubberBand is { } band)
        {
            var rect = Normalize(band);
            drawingContext.DrawRectangle(BandBrush, new Pen(AccentBrush, unit), rect);
        }
    }

    private static Rect Normalize(RectD r)
    {
        var n = r.Normalize();
        return new Rect(n.X, n.Y, n.Width, n.Height);
    }

    private void DrawSelection(DrawingContext dc, double unit)
    {
        var items = _context!.Selection.Items;
        if (items.Count == 0)
        {
            return;
        }

        var outline = new Pen(AccentBrush, unit) { DashStyle = new DashStyle([4, 3], 0) };
        var locked = new Pen(LockedBrush, unit) { DashStyle = new DashStyle([2, 2], 0) };
        foreach (var item in items.Where(i => i.IsVisible))
        {
            if (item is LineAnnotation or PolygonAnnotation && items.Count == 1)
            {
                continue;
            }

            var bounds = Normalize(item.Bounds);
            bounds.Inflate(unit * 2, unit * 2);
            dc.DrawRectangle(null, item.IsLocked ? locked : outline, bounds);
        }

        var handles = SelectionHandles.Get(items);
        var half = unit * 4;
        var handlePen = new Pen(AccentBrush, unit);
        if (items is [LineAnnotation line])
        {
            dc.DrawLine(new Pen(AccentBrush, unit) { DashStyle = new DashStyle([4, 3], 0) }, new Point(line.Start.X, line.Start.Y), new Point(line.End.X, line.End.Y));
        }

        foreach (var handle in handles)
        {
            var center = new Point(handle.Position.X, handle.Position.Y);
            if (handle.Kind is HandleKind.LineStart or HandleKind.LineEnd or HandleKind.PolygonPoint)
            {
                dc.DrawEllipse(HandleFill, handlePen, center, half, half);
            }
            else
            {
                dc.DrawRectangle(HandleFill, handlePen, new Rect(center.X - half, center.Y - half, half * 2, half * 2));
            }
        }
    }

    private void DrawMarked(DrawingContext dc, double unit)
    {
        if (_context!.Interaction.Marked.Count == 0)
        {
            return;
        }

        var pen = new Pen(DangerBrush, unit * 1.5) { DashStyle = new DashStyle([3, 2], 0) };
        foreach (var item in _context.Interaction.Marked)
        {
            var bounds = Normalize(item.VisualBounds);
            bounds.Inflate(unit * 2, unit * 2);
            dc.DrawRectangle(null, pen, bounds);
        }
    }

    private void DrawBrushPreview(DrawingContext dc, double unit)
    {
        if (_context!.Interaction.BrushPreview is not { } brush)
        {
            return;
        }

        var center = new Point(brush.Center.X, brush.Center.Y);
        var radius = Math.Max(brush.Diameter / 2, unit);
        dc.DrawEllipse(null, new Pen(Brushes.White, unit * 2), center, radius, radius);
        dc.DrawEllipse(null, new Pen(Brushes.Black, unit), center, radius, radius);
    }

    /// <summary>Virtuelles Lineal: Band, Pixelskala und Winkelanzeige. Nur auf der Interaktionsebene, nie im Export.</summary>
    private void DrawRuler(DrawingContext dc, double unit)
    {
        var ruler = _context!.Ruler;
        if (!ruler.IsVisible)
        {
            return;
        }

        var placement = ruler.Placement(_context.Scale);
        var center = new Point(placement.Center.X, placement.Center.Y);
        var half = placement.Length / 2;
        var width = placement.Width;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var scale = Math.Max(_context.Scale, 0.01);
        var edgePen = new Pen(RulerInk, unit);
        var tickPen = new Pen(RulerInk, unit);

        dc.PushTransform(new RotateTransform(placement.AngleDegrees, center.X, center.Y));
        dc.DrawRectangle(RulerFill, edgePen, new Rect(center.X - half, center.Y - (width / 2), placement.Length, width));

        // Skala von der linken Kante aus in Bildpixeln; nur Striche, die auf dem Bildschirm genug Abstand haben.
        var labelStep = LabelSteps.FirstOrDefault(s => s * scale >= 48, LabelSteps[^1]);
        var edgeY = center.Y - (width / 2);
        for (var value = 0; value <= placement.Length; value += 10)
        {
            var isHundred = value % 100 == 0;
            var isFifty = value % 50 == 0;
            if (!isFifty && 10 * scale < 4)
            {
                continue;
            }

            var x = center.X - half + value;
            var length = (isHundred ? 14 : isFifty ? 10 : 6) * unit;
            dc.DrawLine(tickPen, new Point(x, edgeY), new Point(x, edgeY + length));
            if (value % labelStep < 0.001 && value > 0)
            {
                var text = new FormattedText(
                    value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11 * unit,
                    RulerInk,
                    dpi);
                dc.DrawText(text, new Point(x - (text.Width / 2), edgeY + (16 * unit)));
            }
        }

        dc.Pop();

        // Winkelanzeige in der Mitte, immer waagerecht lesbar.
        var angle = new FormattedText(
            ruler.Angle.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + "°",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            13 * unit,
            Brushes.White,
            dpi);
        var pad = 4 * unit;
        dc.DrawRoundedRectangle(
            LabelFill,
            null,
            new Rect(center.X - (angle.Width / 2) - pad, center.Y - (angle.Height / 2) - pad, angle.Width + (2 * pad), angle.Height + (2 * pad)),
            4 * unit,
            4 * unit);
        dc.DrawText(angle, new Point(center.X - (angle.Width / 2), center.Y - (angle.Height / 2)));
    }

    private void OnInvalidated(object? sender, EventArgs e) => InvalidateVisual();

    private void OnInvalidated(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
