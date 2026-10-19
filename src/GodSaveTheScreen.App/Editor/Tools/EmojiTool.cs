using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Emoji: Klick setzt das gewählte Emoji mittig in Standardgröße, Aufziehen bestimmt die Größe
/// (Quadrat, mit <c>Umschalt</c> frei, mit <c>Alt</c> von der Mitte aus).
/// </summary>
public sealed class EmojiTool : ToolBase
{
    public const string ToolId = "emoji";

    /// <summary>Ab dieser Zugstrecke in Bildschirm-DIPs gilt die Geste als Aufziehen, sonst als Klick.</summary>
    private const double DragThreshold = 4;

    private PointD _start;
    private EmojiAnnotation? _preview;

    public EmojiTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Emoji";

    public override SymbolRegular Symbol => SymbolRegular.Emoji24;

    public override Key ShortcutKey => Key.J;

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

        if (args.Ctrl && Context.HitTest(args.Position) is { } hit)
        {
            Context.Selection.Set(hit);
            return;
        }

        _start = args.Position;
        _preview = Context.Emoji.Create(Context.Emoji.DefaultRect(_start));
        Context.Interaction.Preview = null;
        Context.Selection.Clear();
        Context.InvalidateInteraction();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_preview is null || !IsDragging(args.Position))
        {
            return;
        }

        _preview.Rect = DragRect(args.Position, args.Modifiers);
        Context.Interaction.Preview = _preview;
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        var emoji = _preview;
        _preview = null;
        Context.Interaction.Preview = null;
        var rect = IsDragging(args.Position) ? DragRect(args.Position, args.Modifiers) : Context.Emoji.DefaultRect(_start);
        if (Math.Max(rect.Width, rect.Height) < EmojiAnnotation.MinSize)
        {
            rect = Context.Emoji.DefaultRect(_start);
        }

        emoji.Rect = rect;
        Context.InvalidateInteraction();
        Commit(emoji);
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

    /// <summary>Fügt das Emoji als einen Undo-Schritt ein, wählt es aus und trägt es in „Zuletzt verwendet“ ein.</summary>
    public void Commit(EmojiAnnotation emoji)
    {
        Context.Undo.Execute(new AddAnnotationAction(Context.Document.Annotations, emoji, description: "Emoji einfügen"));
        Context.Selection.Set(emoji);
        Context.Emoji.AddRecent(emoji.Sequence);
        if (Context.Settings.Current.SwitchToSelectAfterDraw)
        {
            Context.ActivateTool(SelectTool.ToolId);
        }
    }

    private bool IsDragging(PointD position) => position.DistanceTo(_start) >= Context.ToImageUnits(DragThreshold);

    private RectD DragRect(PointD position, ModifierKeys modifiers)
    {
        // Standard ist ein Quadrat; Umschalt hebt das auf (ConstrainBox: Umschalt = Quadrat), Alt = von der Mitte aus.
        var constraint = modifiers & ModifierKeys.Alt;
        if ((modifiers & ModifierKeys.Shift) == 0)
        {
            constraint |= ModifierKeys.Shift;
        }

        return ShapeGeometry.ConstrainBox(_start, position, constraint);
    }
}
