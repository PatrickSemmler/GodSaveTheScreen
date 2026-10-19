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

    private EditorContext? _context;

    public InteractionLayer() => IsHitTestVisible = false;

    public void Attach(EditorContext context)
    {
        Detach();
        _context = context;
        context.InteractionInvalidated += OnInvalidated;
        context.Selection.Changed += OnInvalidated;
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
            AnnotationRenderer.Render(drawingContext, preview);
        }

        DrawSelection(drawingContext, unit);

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

    private void OnInvalidated(object? sender, EventArgs e) => InvalidateVisual();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
