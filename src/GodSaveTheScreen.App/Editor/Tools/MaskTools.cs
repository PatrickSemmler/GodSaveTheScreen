using System.Windows.Input;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Befehle auf der Transparenzmaske des Dokuments (je ein Undo-Schritt).</summary>
public static class MaskCommands
{
    /// <summary>Macht die Bereichsauswahl transparent (mit weicher Kante aus den Einstellungen) und hebt sie auf.</summary>
    public static bool RemoveSelection(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var mask = context.Document.Mask;
        if (MaskEditing.Remove(mask, context.AreaSelection, context.MaskTool.Feather) is not { } edit)
        {
            return false;
        }

        context.Undo.Execute(new MaskEditAction(mask, edit.Region, edit.Values, Strings.Mask_Remove));
        context.AreaSelection.Clear();
        context.InvalidateInteraction();
        return true;
    }

    /// <summary>Stellt den ganzen Screenshot wieder her.</summary>
    public static void Reset(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var mask = context.Document.Mask;
        if (mask.IsOpaque)
        {
            return;
        }

        var opaque = new byte[mask.Values.Length];
        Array.Fill(opaque, (byte)255);
        context.Undo.Execute(new MaskEditAction(mask, mask.Bounds, opaque, Strings.Mask_Reset));
    }

    /// <summary>Schneidet auf den sichtbaren Teil des Screenshots zu; überstehende Objekte bleiben vollständig sichtbar.</summary>
    public static bool CropToVisible(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var document = context.Document;
        var bounds = document.BaseImage.Bounds;
        var visible = AlphaMask.VisibleBounds(document.MaskedImage);
        var objects = CropGeometry.BoundsOfObjects(document.Annotations, 0, bounds);
        var union = (visible, objects) switch
        {
            ({ } v, { } o) => PixelRect.FromLtrb(Math.Min(v.X, o.X), Math.Min(v.Y, o.Y), Math.Max(v.Right, o.Right), Math.Max(v.Bottom, o.Bottom)),
            ({ } v, null) => v,
            (null, { } o) => o,
            _ => (PixelRect?)null,
        };
        if (union is not { } rect)
        {
            return false;
        }

        var action = new CropAction(document, rect, Strings.Mask_CropToVisible);
        if (action.IsNoOp)
        {
            return false;
        }

        context.Undo.Execute(action);
        return true;
    }
}

/// <summary>
/// Bereichsauswahl (W): Rechteck, Ellipse, Lasso oder Zauberstab; Umschalt fügt hinzu, Alt zieht ab.
/// <c>Entf</c> macht den Bereich transparent. Die Auswahl wird beim Werkzeugwechsel aufgehoben.
/// </summary>
public sealed class AreaSelectTool : ToolBase
{
    public const string ToolId = "area-select";

    private PointD? _start;
    private List<PointD>? _points;
    private SelectionCombine _combine;

    public AreaSelectTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_AreaSelect";

    public override SymbolRegular Symbol => SymbolRegular.SelectObject24;

    public override Key ShortcutKey => Key.W;

    public override void Activate() => Context.Selection.Clear();

    public override void Deactivate()
    {
        Cancel();
        Context.AreaSelection.Clear();
        Context.InvalidateInteraction();
    }

    public override void Cancel()
    {
        if (_start is null)
        {
            return;
        }

        _start = null;
        _points = null;
        Context.Interaction.AreaPreview = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        _combine = args.Shift ? SelectionCombine.Add : args.Alt ? SelectionCombine.Subtract : SelectionCombine.Replace;
        var shape = Context.MaskTool.Shape;
        if (shape == AreaSelectShape.MagicWand)
        {
            var (box, coverage) = AreaSelection.MagicWand(Context.Document.MaskedImage, args.Position, Context.MaskTool.Tolerance);
            Apply(box, coverage);
            return;
        }

        _start = args.Position;
        _points = [args.Position];
        UpdatePreview(args.Position);
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_start is null)
        {
            return;
        }

        if (_points is { } points && points[^1].DistanceTo(args.Position) >= Context.ToImageUnits(2))
        {
            points.Add(args.Position);
        }

        UpdatePreview(args.Position);
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_start is not { } start)
        {
            return;
        }

        var points = _points ?? [];
        points.Add(args.Position);
        var shape = Context.MaskTool.Shape;
        var rect = RectD.FromPoints(start, args.Position);
        _start = null;
        _points = null;
        Context.Interaction.AreaPreview = null;

        var clip = Context.AreaSelection.Area;
        var (box, coverage) = shape switch
        {
            AreaSelectShape.Ellipse => AreaSelection.Ellipse(rect, clip),
            AreaSelectShape.Lasso => AreaSelection.Lasso(points, clip),
            _ => AreaSelection.Rectangle(rect, clip),
        };

        // Ein einfacher Klick ohne Fläche hebt die Auswahl auf.
        if (box.IsEmpty && _combine == SelectionCombine.Replace)
        {
            Context.AreaSelection.Clear();
            Context.InvalidateInteraction();
            return;
        }

        Apply(box, coverage);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        switch (key)
        {
            case Key.Delete or Key.Back when modifiers == ModifierKeys.None:
                return MaskCommands.RemoveSelection(Context) || !Context.AreaSelection.IsEmpty;
            case Key.Escape when _start is not null:
                Cancel();
                return true;
            case Key.Escape when !Context.AreaSelection.IsEmpty:
                Context.AreaSelection.Clear();
                Context.InvalidateInteraction();
                return true;
            case Key.A when ctrl && !shift:
                Context.AreaSelection.SelectAll();
                Context.InvalidateInteraction();
                return true;
            case Key.I when ctrl && shift:
                Context.AreaSelection.Invert();
                Context.InvalidateInteraction();
                return true;
            default:
                return false;
        }
    }

    private void Apply(PixelRect box, byte[] coverage)
    {
        if (box.IsEmpty && _combine != SelectionCombine.Replace)
        {
            return;
        }

        Context.AreaSelection.Combine(box, coverage, _combine);
        Context.InvalidateInteraction();
    }

    private void UpdatePreview(PointD position)
    {
        Context.Interaction.AreaPreview = new AreaPreview(
            Context.MaskTool.Shape,
            RectD.FromPoints(_start!.Value, position),
            _points is { } points ? [.. points, position] : []);
        Context.InvalidateInteraction();
    }
}

/// <summary>
/// Hintergrundradierer (D): malt entfernte Bereiche in die Maske (Größe und Härte im Panel); mit Alt wird wiederhergestellt.
/// Ein Strich ist ein Undo-Schritt.
/// </summary>
public sealed class BackgroundEraserTool : ToolBase
{
    public const string ToolId = "background-eraser";

    private MaskBrush? _brush;

    public BackgroundEraserTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_BackgroundEraser";

    public override SymbolRegular Symbol => SymbolRegular.EraserSegment24;

    public override Key ShortcutKey => Key.D;

    /// <summary>Wird bei Größenänderung über die Tastatur aufgerufen (Panel aktualisieren).</summary>
    public event EventHandler? SizeChanged;

    public override void Activate() => Context.Selection.Clear();

    public override void Deactivate()
    {
        Cancel();
        Context.Interaction.BrushPreview = null;
        Context.InvalidateInteraction();
    }

    public override void Cancel()
    {
        _brush?.Cancel();
        _brush = null;
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var style = Context.MaskTool;
        _brush = new MaskBrush(Context.Document.Mask, style.BrushSize, style.BrushHardness / 100, restore: args.Alt);
        _brush.LineTo(args.Position);
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        Context.Interaction.BrushPreview = (args.Position, Context.MaskTool.BrushSize);
        _brush?.LineTo(args.Position);
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_brush is not { } brush)
        {
            return;
        }

        brush.LineTo(args.Position);
        _brush = null;
        if (brush.End() is { } edit)
        {
            Context.Undo.Execute(new MaskEditAction(
                Context.Document.Mask, edit.Region, edit.Before, edit.After, brush.IsRestore ? Strings.Mask_Restore : Strings.Mask_Erase));
        }
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _brush is not null)
        {
            Cancel();
            return true;
        }

        if (modifiers == ModifierKeys.None && key is Key.OemOpenBrackets or Key.OemCloseBrackets)
        {
            var style = Context.MaskTool;
            var factor = key == Key.OemCloseBrackets ? 1.2 : 1 / 1.2;
            var size = Math.Clamp(Math.Round(style.BrushSize * factor), MaskBrush.MinSize, MaskBrush.MaxSize);
            Context.MaskTool = style with { BrushSize = size };
            if (Context.Interaction.BrushPreview is { } preview)
            {
                Context.Interaction.BrushPreview = (preview.Center, size);
                Context.InvalidateInteraction();
            }

            SizeChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }
}
