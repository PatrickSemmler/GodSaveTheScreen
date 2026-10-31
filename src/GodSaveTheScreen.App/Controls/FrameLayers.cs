using System.Windows;
using System.Windows.Media;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Schatten und Rand des Rahmens hinter der Zeichenfläche. Das Element ist so groß wie der angezeigte Bereich
/// (<see cref="Origin"/> = dessen linke obere Ecke in Bildkoordinaten) und zeichnet über denselben
/// <see cref="FrameRenderer"/> wie der Export. Nicht treffbar.
/// </summary>
public sealed class FrameBackLayer : FrameworkElement
{
    private FrameLayout _layout;
    private FrameStyle? _style;
    private SoftLayer? _shadow;
    private PointD _origin;

    public FrameBackLayer() => IsHitTestVisible = false;

    /// <summary>Linke obere Ecke des angezeigten Bereichs in Bildkoordinaten.</summary>
    public PointD Origin => _origin;

    public void Set(FrameLayout layout, FrameStyle? style, SoftLayer? shadow, PointD origin)
    {
        _layout = layout;
        _style = style;
        _shadow = shadow;
        _origin = origin;
        InvalidateVisual();
    }

    public void Clear()
    {
        _style = null;
        _shadow = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_style is not { } style)
        {
            return;
        }

        drawingContext.PushTransform(new TranslateTransform(-_origin.X, -_origin.Y));
        FrameRenderer.DrawBehind(drawingContext, _layout, style, _shadow);
        drawingContext.Pop();
    }
}

/// <summary>
/// Vignette über den Objekten. Liegt in der Zeichenfläche (Koordinaten = Bildpixel des Basisbilds) und zeichnet nur
/// innerhalb des Inhaltsclips. Nicht treffbar.
/// </summary>
public sealed class FrameOverLayer : FrameworkElement
{
    private FrameLayout _layout;
    private FrameStyle? _style;

    public FrameOverLayer() => IsHitTestVisible = false;

    public void Set(FrameLayout layout, FrameStyle? style)
    {
        _layout = layout;
        _style = style is { HasVignette: true } ? style : null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_style is { } style)
        {
            FrameRenderer.DrawOver(drawingContext, _layout, style);
        }
    }
}
