using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Stempel: Klick setzt einen Stempel mit den Standardwerten mittig auf den Punkt und startet die Bearbeitung des Textes
/// (vorbelegt und markiert, Tippen ersetzt ihn); Klick auf einen vorhandenen Stempel bearbeitet dessen Text.
/// </summary>
public sealed class StampTool : ToolBase
{
    public const string ToolId = "stamp";

    public StampTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Stamp";

    public override SymbolRegular Symbol => SymbolRegular.Shapes24;

    public override string? IconPath => "M10,3.5 H14 V8.5 L16,11.5 H19.5 V15.5 H4.5 V11.5 H8 L10,8.5 Z M4.5,19 H19.5";

    public override Key ShortcutKey => Key.S;

    public override bool HasStyleDefaults => true;

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var hit = Context.HitTest(args.Position);
        if (hit is StampAnnotation existing && !existing.IsLocked && !args.Ctrl)
        {
            Context.BeginTextEdit(existing, isNew: false);
            return;
        }

        if (args.Ctrl && hit is not null)
        {
            Context.Selection.Set(hit);
            return;
        }

        Context.Selection.Clear();
        Context.BeginTextEdit(Context.Stamp.Create(args.Position), isNew: true);
    }
}
