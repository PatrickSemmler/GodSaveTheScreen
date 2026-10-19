using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Unschärfe-Bereich aufziehen (<c>Umschalt</c> = Quadrat); Modus und Stärke kommen aus der Vorlage des Eigenschaftenpanels.</summary>
public sealed class BlurTool : ShapeToolBase<BlurAnnotation>
{
    public const string ToolId = "blur";

    public BlurTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Blur";

    public override SymbolRegular Symbol => SymbolRegular.Square24;

    public override string? IconPath => "M3,3 H21 V21 H3 Z M9,3 V21 M15,3 V21 M3,9 H21 M3,15 H21";

    public override Key ShortcutKey => Key.U;

    public override bool UsesShapeStyle => false;

    protected override void UpdateGeometry(BlurAnnotation shape, PointD start, PointD current, ModifierKeys modifiers) =>
        shape.Rect = ShapeGeometry.ConstrainBox(start, current, modifiers);

    protected override bool IsLargeEnough(BlurAnnotation shape) => ExceedsMinimum(shape.Rect.Width, shape.Rect.Height);

    protected override void ApplyExtra(BlurAnnotation shape, ShapeStyle style)
    {
        var template = Context.BlurTemplate;
        shape.Mode = template.Mode;
        shape.Strength = template.Strength;
        shape.Shape = template.Shape;
        shape.FeatherEdge = template.FeatherEdge;
        shape.RedactColor = template.RedactColor;
    }
}

/// <summary>
/// Lupe: Ziehen legt den Quellbereich fest; die Lupe erscheint versetzt (rechts unten, im Bild geklemmt)
/// und lässt sich danach getrennt von der Quelle verschieben.
/// </summary>
public sealed class MagnifierTool : ShapeToolBase<MagnifierAnnotation>
{
    public const string ToolId = "magnifier";

    public MagnifierTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Magnifier";

    public override SymbolRegular Symbol => SymbolRegular.ZoomIn24;

    public override Key ShortcutKey => Key.Z;

    public override bool UsesShapeStyle => false;

    protected override void UpdateGeometry(MagnifierAnnotation shape, PointD start, PointD current, ModifierKeys modifiers)
    {
        // Eine runde Lupe zeigt einen quadratischen Ausschnitt.
        var constrained = shape.Shape == MagnifierShape.Circle ? modifiers | ModifierKeys.Shift : modifiers;
        var rect = ShapeGeometry.ConstrainBox(start, current, constrained);
        shape.SourceRect = rect;
        var image = Context.Document.BaseImage;
        shape.TargetCenter = MagnifierGeometry.DefaultTargetCenter(rect, shape.Zoom, image.Width, image.Height);
    }

    protected override bool IsLargeEnough(MagnifierAnnotation shape) =>
        Math.Min(shape.SourceRect.Width, shape.SourceRect.Height) >= 4;

    protected override void ApplyExtra(MagnifierAnnotation shape, ShapeStyle style)
    {
        var template = Context.MagnifierTemplate;
        shape.Zoom = template.Zoom;
        shape.Shape = template.Shape;
        shape.Border = template.Border;
        shape.ShowConnector = template.ShowConnector;
        shape.ShowSourceFrame = template.ShowSourceFrame;
        shape.BaseImageOnly = template.BaseImageOnly;
        shape.SmoothScaling = template.SmoothScaling;
    }
}
