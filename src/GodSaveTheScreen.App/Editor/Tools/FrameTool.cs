using System.Windows.Input;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>
/// Rahmen und Kanteneffekte des ganzen Bildes. Das Werkzeug hat keine Zeigeraktion: Das Eigenschaftenpanel bearbeitet
/// <c>ImageDocument.Frame</c>, jede Änderung wirkt sofort auf der Zeichenfläche.
/// </summary>
public sealed class FrameTool : ToolBase
{
    public const string ToolId = "frame";

    public FrameTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Frame";

    public override SymbolRegular Symbol => SymbolRegular.ImageBorder24;

    public override Key ShortcutKey => Key.F;

    public override Cursor Cursor => Cursors.Arrow;

    /// <summary>Das Panel zeigt Rahmen-Einstellungen (nicht Objekteigenschaften): eine Auswahl wird beim Aktivieren aufgehoben.</summary>
    public override bool HasStyleDefaults => true;
}
