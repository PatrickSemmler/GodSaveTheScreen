using System.Windows.Input;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Zuschnittmodus wie im Snipping Tool: gesamtes Bild mit abgedunkeltem Außenbereich, Rahmen mit 8 Griffen.
/// Der Zuschnitt wird erst mit <see cref="Commit"/> als <see cref="CropAction"/> übernommen.
/// </summary>
public sealed class CropTool : ToolBase
{
    public const string ToolId = "crop";

    /// <summary>Trefferbereich der Griffe in Bildschirm-DIPs.</summary>
    public const double HandleHitDip = 10;

    /// <summary>Einrastabstand in Bildschirm-DIPs.</summary>
    public const double SnapDip = 8;

    private const double DragThresholdDip = 3;

    private CropHandle _handle;
    private CropHandle _hover;
    private bool _pressed;
    private bool _newArea;
    private bool _moved;
    private PointD _startPoint;
    private PixelRect _startRect;
    private List<double> _snapX = [];
    private List<double> _snapY = [];

    public CropTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Crop";

    public override SymbolRegular Symbol => SymbolRegular.Crop24;

    public override Key ShortcutKey => Key.C;

    public override Cursor Cursor => (_pressed ? _handle : _hover) switch
    {
        CropHandle.Move => Cursors.SizeAll,
        CropHandle.TopLeft or CropHandle.BottomRight => Cursors.SizeNWSE,
        CropHandle.TopRight or CropHandle.BottomLeft => Cursors.SizeNESW,
        CropHandle.Top or CropHandle.Bottom => Cursors.SizeNS,
        CropHandle.Left or CropHandle.Right => Cursors.SizeWE,
        _ => Cursors.Cross,
    };

    private CropState Crop => Context.Crop;

    public override void Activate()
    {
        Context.Selection.Clear();
        Crop.Begin();
        Context.InvalidateInteraction();
    }

    public override void Deactivate()
    {
        _pressed = false;
        Crop.End();
        Context.InvalidateInteraction();
    }

    /// <summary>Übernimmt den Rahmen als Zuschnitt (rückgängig machbar) und verlässt den Modus.</summary>
    public void Commit()
    {
        var action = new CropAction(Context.Document, Crop.Rect);
        if (!action.IsNoOp)
        {
            Context.Undo.Execute(action);
        }

        Leave();
    }

    /// <summary>Verwirft den Rahmen und verlässt den Modus.</summary>
    public void CancelMode() => Leave();

    private void Leave() => Context.ActivateTool(SelectTool.ToolId);

    /// <summary>Welcher Teil des Rahmens liegt unter dem Punkt? <see cref="CropHandle.None"/> = außerhalb.</summary>
    public CropHandle HitHandle(PointD point)
    {
        var rect = Crop.Rect;
        var tol = Context.ToImageUnits(HandleHitDip);
        var nearLeft = Math.Abs(point.X - rect.X) <= tol;
        var nearRight = Math.Abs(point.X - rect.Right) <= tol;
        var nearTop = Math.Abs(point.Y - rect.Y) <= tol;
        var nearBottom = Math.Abs(point.Y - rect.Bottom) <= tol;
        var inX = point.X >= rect.X - tol && point.X <= rect.Right + tol;
        var inY = point.Y >= rect.Y - tol && point.Y <= rect.Bottom + tol;

        if (nearTop && nearLeft)
        {
            return CropHandle.TopLeft;
        }

        if (nearTop && nearRight)
        {
            return CropHandle.TopRight;
        }

        if (nearBottom && nearLeft)
        {
            return CropHandle.BottomLeft;
        }

        if (nearBottom && nearRight)
        {
            return CropHandle.BottomRight;
        }

        if (nearTop && inX)
        {
            return CropHandle.Top;
        }

        if (nearBottom && inX)
        {
            return CropHandle.Bottom;
        }

        if (nearLeft && inY)
        {
            return CropHandle.Left;
        }

        if (nearRight && inY)
        {
            return CropHandle.Right;
        }

        return rect.ToRectD().Contains(point) ? CropHandle.Move : CropHandle.None;
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        _handle = HitHandle(args.Position);
        _newArea = _handle == CropHandle.None;
        _pressed = true;
        _moved = false;
        _startPoint = args.Position;
        _startRect = Crop.Rect;
        (_snapX, _snapY) = CropGeometry.SnapLines(Crop.FullBounds, Context.Document.Annotations);
        Crop.IsDragging = !_newArea;
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (!_pressed)
        {
            _hover = HitHandle(args.Position);
            return;
        }

        var tolerance = Context.ToImageUnits(SnapDip);
        var bounds = Crop.FullBounds;
        var snapping = !args.Alt;

        if (_newArea)
        {
            if (!_moved && _startPoint.DistanceTo(args.Position) < Context.ToImageUnits(DragThresholdDip))
            {
                return;
            }

            _moved = true;
            Crop.IsDragging = true;
            var aspect = Crop.Aspect;
            var target = args.Position;
            if (aspect is null && snapping)
            {
                target = new PointD(CropGeometry.Snap(target.X, _snapX, tolerance), CropGeometry.Snap(target.Y, _snapY, tolerance));
            }

            Crop.SetRect(CropGeometry.FromDrag(_startPoint, target, bounds, aspect));
            return;
        }

        var dx = args.Position.X - _startPoint.X;
        var dy = args.Position.Y - _startPoint.Y;
        double? ratio = Crop.Aspect;
        if (ratio is null && args.Shift && _startRect.Height > 0)
        {
            ratio = (double)_startRect.Width / _startRect.Height;
        }

        if (snapping && ratio is null)
        {
            (dx, dy) = Snapped(dx, dy, tolerance);
        }

        var next = CropGeometry.Resize(_startRect, _handle, (int)Math.Round(dx), (int)Math.Round(dy), bounds, ratio);
        _moved = true;
        Crop.SetRect(next);
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        _pressed = false;
        Crop.IsDragging = false;
        _hover = HitHandle(args.Position);
    }

    public override void Cancel()
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        Crop.IsDragging = false;
        Crop.SetRect(_startRect);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.Enter:
                Commit();
                return true;
            case Key.Escape:
                CancelMode();
                return true;
            case Key.Left or Key.Right or Key.Up or Key.Down:
                Nudge(key, (modifiers & ModifierKeys.Shift) != 0 ? 10 : 1);
                return true;
            default:
                return false;
        }
    }

    private void Nudge(Key key, int step)
    {
        var (dx, dy) = key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            _ => (0, step),
        };
        Crop.SetRect(CropGeometry.Move(Crop.Rect, dx, dy, Crop.FullBounds));
    }

    /// <summary>Rastet die bewegten Kanten an Bildrändern und Objektkanten ein.</summary>
    private (double Dx, double Dy) Snapped(double dx, double dy, double tolerance)
    {
        var r = _startRect;
        switch (_handle)
        {
            case CropHandle.Move:
                {
                    var left = CropGeometry.Snap(r.X + dx, _snapX, tolerance);
                    var snappedX = left != r.X + dx ? left - r.X : CropGeometry.Snap(r.Right + dx, _snapX, tolerance) - r.Right;
                    var top = CropGeometry.Snap(r.Y + dy, _snapY, tolerance);
                    var snappedY = top != r.Y + dy ? top - r.Y : CropGeometry.Snap(r.Bottom + dy, _snapY, tolerance) - r.Bottom;
                    return (snappedX, snappedY);
                }

            default:
                {
                    var movesLeft = _handle is CropHandle.TopLeft or CropHandle.Left or CropHandle.BottomLeft;
                    var movesRight = _handle is CropHandle.TopRight or CropHandle.Right or CropHandle.BottomRight;
                    var movesTop = _handle is CropHandle.TopLeft or CropHandle.Top or CropHandle.TopRight;
                    var movesBottom = _handle is CropHandle.BottomLeft or CropHandle.Bottom or CropHandle.BottomRight;
                    if (movesLeft)
                    {
                        dx = CropGeometry.Snap(r.X + dx, _snapX, tolerance) - r.X;
                    }
                    else if (movesRight)
                    {
                        dx = CropGeometry.Snap(r.Right + dx, _snapX, tolerance) - r.Right;
                    }

                    if (movesTop)
                    {
                        dy = CropGeometry.Snap(r.Y + dy, _snapY, tolerance) - r.Y;
                    }
                    else if (movesBottom)
                    {
                        dy = CropGeometry.Snap(r.Bottom + dy, _snapY, tolerance) - r.Bottom;
                    }

                    return (dx, dy);
                }
        }
    }
}
