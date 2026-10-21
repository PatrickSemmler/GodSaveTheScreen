using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Setzt die gespeicherten Einstellungen eines Werkzeugs auf die eingebauten Standardwerte zurück.</summary>
public static class ToolDefaultsReset
{
    /// <summary>Das Werkzeug hat Einstellungen, die sich zurücksetzen lassen.</summary>
    public static bool Supports(ToolBase? tool) =>
        tool is not null && (tool.HasStyleDefaults || tool is EraserTool or AreaSelectTool or BackgroundEraserTool);

    /// <summary>
    /// Setzt die Einstellungen zurück. Stift und Textmarker teilen sich Glättung und Druck; Auswahl, Vorlagen,
    /// zuletzt verwendete Farben/Emojis und Favoriten bleiben unverändert.
    /// </summary>
    public static void Reset(EditorContext context, ToolBase tool)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tool);
        switch (tool)
        {
            case PenTool or HighlighterTool:
                context.Defaults.ResetShape(tool.Id);
                var freehand = new FreehandStyle();
                context.Freehand = new FreehandOptions(freehand.Smoothing, freehand.UsePressure);
                break;
            case EraserTool:
                context.EraserAllObjects = false;
                break;
            case TextTool:
                context.Defaults.SetText(new TextStyle());
                break;
            case MarkerTool:
                context.Defaults.SetMarker(new MarkerStyle());
                context.Defaults.MarkerStartNumber = new AppSettings().MarkerStartNumber;
                break;
            case BlurTool:
                context.BlurTemplate.Apply(new BlurStyle());
                context.SaveBlurTemplate();
                break;
            case MagnifierTool:
                context.MagnifierTemplate.Apply(new MagnifierStyle());
                context.SaveMagnifierTemplate();
                break;
            case AreaSelectTool:
                var area = new MaskToolStyle();
                context.MaskTool = context.MaskTool with { Shape = area.Shape, Feather = area.Feather, Tolerance = area.Tolerance };
                break;
            case BackgroundEraserTool:
                var brush = new MaskToolStyle();
                context.MaskTool = context.MaskTool with { BrushSize = brush.BrushSize, BrushHardness = brush.BrushHardness };
                break;
            case EmojiTool:
                context.Emoji.ResetStyle();
                break;
            case StampTool:
                context.Stamp.Style = new StampStyle();
                break;
            default:
                if (tool.UsesShapeStyle)
                {
                    context.Defaults.ResetShape(tool.Id);
                }

                break;
        }
    }
}
