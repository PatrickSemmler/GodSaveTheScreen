using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Sprechblase: Aufziehen bestimmt den Körper (Klick = Standardgröße um den Punkt), danach sofort Texteingabe.
/// Die Spitze steht anfangs unter der Blase und wird im Auswahlwerkzeug am runden Griff verschoben.
/// Klick auf eine vorhandene Sprechblase bearbeitet ihren Text.
/// </summary>
public sealed class CalloutTool : ToolBase
{
    public const string ToolId = "callout";

    private PointD _start;
    private CalloutAnnotation? _preview;

    public CalloutTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Callout";

    public override SymbolRegular Symbol => SymbolRegular.Chat24;

    public override Key ShortcutKey => Key.K;

    public override bool HasStyleDefaults => true;

    public override void Deactivate() => Cancel();

    public override void Cancel()
    {
        if (_preview is null)
        {
            return;
        }

        _preview = null;
        Context.Interaction.Preview = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var hit = Context.HitTest(args.Position);
        if (hit is CalloutAnnotation existing && !existing.IsLocked && !args.Ctrl)
        {
            Context.BeginTextEdit(existing, isNew: false);
            return;
        }

        if (args.Ctrl && hit is not null)
        {
            Context.Selection.Set(hit);
            return;
        }

        _start = args.Position;
        _preview = Context.Callout.Create(new RectD(_start.X, _start.Y, 0, 0));
        _preview.Text = string.Empty;
        Context.Selection.Clear();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        var box = ShapeGeometry.ConstrainBox(_start, args.Position, args.Modifiers);
        if (box.Width < CalloutAnnotation.MinBodySize && box.Height < CalloutAnnotation.MinBodySize)
        {
            Context.Interaction.Preview = null;
        }
        else
        {
            _preview.Rect = box;
            _preview.Tail = CalloutToolState.DefaultTail(box);
            Context.Interaction.Preview = _preview;
        }

        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        var callout = _preview;
        _preview = null;
        Context.Interaction.Preview = null;
        var box = ShapeGeometry.ConstrainBox(_start, args.Position, args.Modifiers);
        if (box.Width < CalloutAnnotation.MinBodySize && box.Height < CalloutAnnotation.MinBodySize)
        {
            // Klick: Standardgröße, mittig um den Punkt.
            var size = CalloutToolState.DefaultBodySize(callout.FontSize);
            box = new RectD(_start.X - (size.Width / 2), _start.Y - (size.Height / 2), size.Width, size.Height);
        }
        else
        {
            box = new RectD(box.X, box.Y, Math.Max(box.Width, CalloutAnnotation.MinBodySize), Math.Max(box.Height, CalloutAnnotation.MinBodySize));
        }

        callout.Rect = box;
        callout.Tail = CalloutToolState.DefaultTail(box);
        Context.InvalidateInteraction();
        Context.BeginTextEdit(callout, isNew: true);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _preview is not null)
        {
            Cancel();
            return true;
        }

        return false;
    }
}
