using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Temporäres Werkzeug: Klick ins Bild liefert die Farbe des gerenderten Bilds.</summary>
public sealed class PipetteTool : ToolBase
{
    public const string ToolId = "pipette";

    private Core.Imaging.PixelBuffer? _rendered;

    public PipetteTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Pipette";

    public override SymbolRegular Symbol => SymbolRegular.Eyedropper24;

    public override bool ShowInPalette => false;

    /// <summary>Wird mit der gewählten Farbe aufgerufen; danach wechselt der Editor zum vorherigen Werkzeug zurück.</summary>
    public Action<ArgbColor>? Picked { get; set; }

    public Action? Cancelled { get; set; }

    public override void Activate() => _rendered = Context.Exporter.Render(Context.Document);

    public override void Deactivate() => _rendered = null;

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left || _rendered is null)
        {
            return;
        }

        // Das gerenderte Bild enthält nur den Zuschnitt; Positionen sind in Originalkoordinaten.
        var origin = Context.Document.EffectiveBounds;
        var color = Sample(_rendered, new PointD(args.Position.X - origin.X, args.Position.Y - origin.Y));
        Picked?.Invoke(color);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key != Key.Escape)
        {
            return false;
        }

        Cancelled?.Invoke();
        return true;
    }

    public static ArgbColor Sample(Core.Imaging.PixelBuffer image, PointD position)
    {
        var x = Math.Clamp((int)Math.Floor(position.X), 0, image.Width - 1);
        var y = Math.Clamp((int)Math.Floor(position.Y), 0, image.Height - 1);
        var i = (y * image.Stride) + (x * Core.Imaging.PixelBuffer.BytesPerPixel);
        return new ArgbColor(image.Pixels[i + 3], image.Pixels[i + 2], image.Pixels[i + 1], image.Pixels[i]);
    }
}

/// <summary>Alle Werkzeuge eines Editorfensters.</summary>
public sealed class ToolRegistry
{
    public ToolRegistry(IEnumerable<ToolBase> tools)
    {
        Tools = [.. tools];
        PaletteTools = [.. Tools.Where(t => t.ShowInPalette)];
    }

    public IReadOnlyList<ToolBase> Tools { get; }

    public IReadOnlyList<ToolBase> PaletteTools { get; }

    public ToolBase? Find(string id) => Tools.FirstOrDefault(t => t.Id == id);

    public ToolBase? FindByKey(Key key) => key == Key.None ? null : PaletteTools.FirstOrDefault(t => t.ShortcutKey == key);

    public T Get<T>()
        where T : ToolBase => Tools.OfType<T>().First();

    public static ToolRegistry Create(EditorContext context) => new(
    [
        new SelectTool(context),
        new RectangleTool(context),
        new EllipseTool(context),
        new LineTool(context),
        new ArrowTool(context),
        new TriangleTool(context),
        new PolygonTool(context),
        new TextTool(context),
        new MarkerTool(context),
        new PenTool(context),
        new HighlighterTool(context),
        new EraserTool(context),
        new BlurTool(context),
        new MagnifierTool(context),
        new AreaSelectTool(context),
        new BackgroundEraserTool(context),
        new EmojiTool(context),
        new CropTool(context),
        new PipetteTool(context),
    ]);
}
