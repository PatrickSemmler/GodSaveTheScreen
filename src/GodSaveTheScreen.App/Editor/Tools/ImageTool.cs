using System.Windows.Input;
using GodSaveTheScreen.Core.Geometry;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Bild: Klick öffnet die Dateiauswahl und setzt das Bild mittig auf den Punkt (Originalgröße, höchstens 80 % des Screenshots);
/// Aufziehen eines Bereichs passt das Bild proportional hinein. Das Panel bearbeitet ohne Auswahl die Vorgaben neuer Bilder.
/// </summary>
public sealed class ImageTool : ToolBase
{
    public const string ToolId = "image";

    /// <summary>Ab dieser Zugstrecke in Bildschirm-DIPs gilt die Geste als Aufziehen, sonst als Klick.</summary>
    private const double DragThreshold = 4;

    private PointD _start;
    private bool _pressed;

    public ImageTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Image";

    public override SymbolRegular Symbol => SymbolRegular.ImageAdd24;

    public override Key ShortcutKey => Key.I;

    public override bool HasStyleDefaults => true;

    public override void Deactivate() => Cancel();

    public override void Cancel()
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        Context.Interaction.RubberBand = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        if (args.Ctrl && Context.HitTest(args.Position) is { } hit)
        {
            Context.Selection.Set(hit);
            return;
        }

        _start = args.Position;
        _pressed = true;
        Context.Selection.Clear();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (!_pressed || !IsDragging(args.Position))
        {
            return;
        }

        Context.Interaction.RubberBand = DragRect(args.Position, args.Modifiers);
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        RectD? area = IsDragging(args.Position) ? DragRect(args.Position, args.Modifiers) : null;
        Context.Interaction.RubberBand = null;
        Context.InvalidateInteraction();
        if (area is { } a && (a.Width < 1 || a.Height < 1))
        {
            area = null;
        }

        Context.RequestImageInsert(area?.Center ?? _start, area);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _pressed)
        {
            Cancel();
            return true;
        }

        return false;
    }

    private bool IsDragging(PointD position) => position.DistanceTo(_start) >= Context.ToImageUnits(DragThreshold);

    private RectD DragRect(PointD position, ModifierKeys modifiers) => ShapeGeometry.ConstrainBox(_start, position, modifiers);
}
