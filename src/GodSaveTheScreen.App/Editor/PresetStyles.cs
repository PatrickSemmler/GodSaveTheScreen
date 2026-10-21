using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Art des Stils, den ein Werkzeug in Vorlagen speichert.</summary>
public enum PresetKind
{
    Shape,
    Freehand,
    Text,
    Marker,
    Blur,
    Magnifier,
    Callout,
    Image,
}

/// <summary>
/// Erfasst Vorlagen aus dem Panel-Stand und wendet sie an: mit Auswahl auf die passenden, nicht gesperrten Objekte
/// (ein Undo-Schritt), sonst auf die Standardwerte des Werkzeugs.
/// </summary>
public static class PresetStyles
{
    public static PresetKind? KindOf(string toolId) => toolId switch
    {
        RectangleTool.ToolId or EllipseTool.ToolId or TriangleTool.ToolId or LineTool.ToolId or ArrowTool.ToolId or PolygonTool.ToolId => PresetKind.Shape,
        PenTool.ToolId or HighlighterTool.ToolId => PresetKind.Freehand,
        TextTool.ToolId => PresetKind.Text,
        MarkerTool.ToolId => PresetKind.Marker,
        BlurTool.ToolId => PresetKind.Blur,
        MagnifierTool.ToolId => PresetKind.Magnifier,
        CalloutTool.ToolId => PresetKind.Callout,
        ImageTool.ToolId => PresetKind.Image,
        _ => null,
    };

    /// <summary>Werkzeug-ID, zu dem das Objekt gehört (Linie/Pfeil: bevorzugt das aktive Werkzeug).</summary>
    public static string? ToolIdOf(Annotation annotation, ToolBase? active) => annotation switch
    {
        RectangleAnnotation => RectangleTool.ToolId,
        EllipseAnnotation => EllipseTool.ToolId,
        TriangleAnnotation => TriangleTool.ToolId,
        PolygonAnnotation => PolygonTool.ToolId,
        LineAnnotation line => active is { Id: LineTool.ToolId or ArrowTool.ToolId } ? active.Id : line.EndCap == LineEndStyle.Arrow ? ArrowTool.ToolId : LineTool.ToolId,
        StrokeAnnotation stroke => stroke.IsHighlighter ? HighlighterTool.ToolId : PenTool.ToolId,
        TextAnnotation => TextTool.ToolId,
        MarkerAnnotation => MarkerTool.ToolId,
        BlurAnnotation => BlurTool.ToolId,
        MagnifierAnnotation => MagnifierTool.ToolId,
        CalloutAnnotation => CalloutTool.ToolId,
        ImageAnnotation => ImageTool.ToolId,
        _ => null,
    };

    /// <summary>Werkzeug, dessen Vorlagen das Panel zeigt: ohne Auswahl das aktive Werkzeug, sonst das der (einheitlichen) Auswahl.</summary>
    public static string? Resolve(IReadOnlyList<Annotation> selection, ToolBase? active)
    {
        if (selection.Count == 0)
        {
            return active is not null && KindOf(active.Id) is not null ? active.Id : null;
        }

        var ids = selection.Select(a => ToolIdOf(a, active)).Distinct().Take(2).ToList();
        return ids.Count == 1 ? ids[0] : null;
    }

    /// <summary>Bildet die Vorlage aus dem Panel-Stand: gemeinsame Werte der Auswahl, gemischte Werte aus den Standardwerten.</summary>
    public static ToolPreset Capture(EditorContext context, string toolId, ShapeStyle builtIn, string name)
    {
        var selection = context.Selection.Items;
        switch (KindOf(toolId))
        {
            case PresetKind.Shape:
                return new ToolPreset { Name = name, Shape = CaptureShape(context, toolId, builtIn, selection.OfType<ShapeAnnotation>().Where(s => s is not StrokeAnnotation)) };
            case PresetKind.Freehand:
                var strokes = selection.OfType<StrokeAnnotation>().ToList();
                var options = context.Freehand;
                return new ToolPreset
                {
                    Name = name,
                    Shape = CaptureShape(context, toolId, builtIn, strokes),
                    Smoothing = Common(strokes, s => s.Smoothing, options.Smoothing),
                    UsePressure = Common(strokes, s => s.UsePressure, options.UsePressure),
                };
            case PresetKind.Text:
                return new ToolPreset { Name = name, Text = CaptureText(context.Defaults.GetText(), selection.OfType<TextAnnotation>().ToList()) };
            case PresetKind.Marker:
                return new ToolPreset { Name = name, Marker = CaptureMarker(context.Defaults.GetMarker(), selection.OfType<MarkerAnnotation>().ToList()) };
            case PresetKind.Blur:
                return new ToolPreset { Name = name, Blur = CaptureBlur(context.BlurTemplate.ToStyle(), selection.OfType<BlurAnnotation>().ToList()) };
            case PresetKind.Magnifier:
                return new ToolPreset { Name = name, Magnifier = CaptureMagnifier(context.MagnifierTemplate.ToStyle(), selection.OfType<MagnifierAnnotation>().ToList()) };
            case PresetKind.Callout:
                return new ToolPreset { Name = name, Callout = CaptureCallout(context.Callout.Style, selection.OfType<CalloutAnnotation>().ToList()) };
            case PresetKind.Image:
                return new ToolPreset { Name = name, Image = CaptureImage(context.Image.Style, selection.OfType<ImageAnnotation>().ToList()) };
            default:
                throw new ArgumentException("Werkzeug ohne Vorlagen.", nameof(toolId));
        }
    }

    /// <summary>
    /// Übernimmt die Werte der Auswahl als Standardwerte des Werkzeugs, wenn die Auswahl einheitlich zu diesem
    /// Werkzeug gehört, damit sie beim Zurückwechseln erhalten bleiben.
    /// </summary>
    public static void RememberSelectionAsDefaults(EditorContext context, ToolBase tool)
    {
        if (context.Selection.Items.Count == 0 || KindOf(tool.Id) is null || Resolve(context.Selection.Items, tool) != tool.Id)
        {
            return;
        }

        ApplyToDefaults(context, tool.Id, Capture(context, tool.Id, tool.BuiltInStyle, string.Empty));
    }

    /// <summary>Wendet die Vorlage an. Gibt die Anzahl geänderter Objekte zurück (0 bei Anwendung auf die Standardwerte).</summary>
    public static int Apply(EditorContext context, string toolId, ToolPreset preset)
    {
        var selection = context.Selection.Items;
        if (selection.Count == 0)
        {
            ApplyToDefaults(context, toolId, preset);
            return 0;
        }

        var targets = selection.Where(a => !a.IsLocked && Matches(toolId, a)).ToList();
        var changes = new List<(Annotation Target, Annotation Before, Annotation After)>();
        foreach (var target in targets)
        {
            var before = target.Clone();
            RotationMath.PreserveFrame(target, () => ApplyTo(target, preset));
            var after = target.Clone();
            changes.Add((target, before, after));
        }

        if (changes.Count > 0)
        {
            context.Undo.Execute(new ReplaceGeometryAction(changes, preset.Name));
        }

        return changes.Count;
    }

    private static bool Matches(string toolId, Annotation annotation) => annotation switch
    {
        StrokeAnnotation stroke => toolId == (stroke.IsHighlighter ? HighlighterTool.ToolId : PenTool.ToolId),
        TextAnnotation => toolId == TextTool.ToolId,
        MarkerAnnotation => toolId == MarkerTool.ToolId,
        BlurAnnotation => toolId == BlurTool.ToolId,
        MagnifierAnnotation => toolId == MagnifierTool.ToolId,
        _ => ToolIdOf(annotation, null) is { } id && (id == toolId || (id is LineTool.ToolId or ArrowTool.ToolId && toolId is LineTool.ToolId or ArrowTool.ToolId)),
    };

    private static void ApplyTo(Annotation target, ToolPreset preset)
    {
        switch (target)
        {
            case StrokeAnnotation stroke when preset.Shape is { } style:
                stroke.Apply(style);
                stroke.Smoothing = preset.Smoothing ?? stroke.Smoothing;
                stroke.UsePressure = preset.UsePressure ?? stroke.UsePressure;
                break;
            case ShapeAnnotation shape when preset.Shape is { } style:
                shape.Apply(style);
                break;
            case TextAnnotation text when preset.Text is { } style:
                StyleApplier.Apply(text, style);
                break;
            case MarkerAnnotation marker when preset.Marker is { } style:
                StyleApplier.Apply(marker, style);
                break;
            case BlurAnnotation blur when preset.Blur is { } style:
                blur.Apply(style);
                break;
            case MagnifierAnnotation magnifier when preset.Magnifier is { } style:
                magnifier.Apply(style);
                break;
            case CalloutAnnotation callout when preset.Callout is { } style:
                StyleApplier.Apply(callout, style.Normalized());
                break;
            case ImageAnnotation image when preset.Image is { } style:
                ImageToolState.Apply(image, style);
                break;
        }
    }

    private static void ApplyToDefaults(EditorContext context, string toolId, ToolPreset preset)
    {
        switch (KindOf(toolId))
        {
            case PresetKind.Shape when preset.Shape is { } shape:
                context.Defaults.SetShape(toolId, shape);
                break;
            case PresetKind.Freehand when preset.Shape is { } shape:
                context.Defaults.SetShape(toolId, shape);
                context.Freehand = new FreehandOptions(preset.Smoothing ?? context.Freehand.Smoothing, preset.UsePressure ?? context.Freehand.UsePressure);
                break;
            case PresetKind.Text when preset.Text is { } text:
                context.Defaults.SetText(text);
                break;
            case PresetKind.Marker when preset.Marker is { } marker:
                context.Defaults.SetMarker(marker);
                break;
            case PresetKind.Blur when preset.Blur is { } blur:
                context.BlurTemplate.Apply(blur);
                context.SaveBlurTemplate();
                break;
            case PresetKind.Magnifier when preset.Magnifier is { } magnifier:
                context.MagnifierTemplate.Apply(magnifier);
                context.SaveMagnifierTemplate();
                break;
            case PresetKind.Callout when preset.Callout is { } callout:
                context.Callout.Style = callout.Normalized();
                break;
            case PresetKind.Image when preset.Image is { } image:
                context.Image.Style = image.Normalized();
                break;
        }
    }

    /// <summary>Gemeinsamer Wert aller Objekte; bei leerer oder gemischter Auswahl der Rückfallwert.</summary>
    private static T Common<TA, T>(IEnumerable<TA> items, Func<TA, T> get, T fallback) =>
        StyleEditing.TryCommon(items, get, out var value) ? value : fallback;

    private static StrokeStyle CommonStroke<TA>(IReadOnlyCollection<TA> items, Func<TA, StrokeStyle> get, StrokeStyle fallback)
    {
        if (items.Count == 0)
        {
            return fallback;
        }

        if (StyleEditing.TryCommon(items, get, out var whole))
        {
            return whole;
        }

        return fallback with
        {
            Color = Common(items, i => get(i).Color, fallback.Color),
            Thickness = Common(items, i => get(i).Thickness, fallback.Thickness),
            Dash = Common(items, i => get(i).Dash, fallback.Dash),
        };
    }

    private static ShapeStyle CaptureShape(EditorContext context, string toolId, ShapeStyle builtIn, IEnumerable<ShapeAnnotation> source)
    {
        var shapes = source.ToList();
        var fallback = context.Defaults.GetShape(toolId, builtIn);
        if (shapes.Count == 0)
        {
            return fallback;
        }

        var filled = shapes.Where(s => s is not (LineAnnotation or StrokeAnnotation)).ToList();
        var rectangles = shapes.OfType<RectangleAnnotation>().ToList();
        var lines = shapes.OfType<LineAnnotation>().ToList();
        return fallback with
        {
            Stroke = CommonStroke(shapes, s => s.Stroke, fallback.Stroke),
            Fill = Common(filled, s => s.Fill, fallback.Fill),
            Opacity = Common(shapes, s => s.Opacity, fallback.Opacity),
            CornerRadius = Common(rectangles, r => r.CornerRadius, fallback.CornerRadius),
            StartCap = Common(lines, l => l.StartCap, fallback.StartCap),
            EndCap = Common(lines, l => l.EndCap, fallback.EndCap),
            Outline = CommonStroke(lines, l => l.Outline, fallback.Outline),
            OutlineBlur = Common(lines, l => l.OutlineBlur, fallback.OutlineBlur),
            Shadow = Common(shapes.Where(s => s is not StrokeAnnotation), s => s.Shadow, fallback.Shadow),
        };
    }

    private static TextStyle CaptureText(TextStyle fallback, List<TextAnnotation> texts) => texts.Count == 0
        ? fallback
        : new TextStyle
        {
            FontFamilyName = Common(texts, t => t.FontFamilyName, fallback.FontFamilyName),
            FontWeight = Common(texts, t => t.FontWeight, fallback.FontWeight),
            FontStyle = Common(texts, t => t.FontStyle, fallback.FontStyle),
            FontStretch = Common(texts, t => t.FontStretch, fallback.FontStretch),
            FontSize = Common(texts, t => t.FontSize, fallback.FontSize),
            Fill = Common(texts, t => t.Fill, fallback.Fill),
            Stroke = CommonStroke(texts, t => t.Stroke, fallback.Stroke),
            OutlineBlur = Common(texts, t => t.OutlineBlur, fallback.OutlineBlur),
            TextAlignment = Common(texts, t => t.TextAlignment, fallback.TextAlignment),
            Background = Common(texts, t => t.Background, fallback.Background),
            BackgroundPadding = Common(texts, t => t.BackgroundPadding, fallback.BackgroundPadding),
            BackgroundCornerRadius = Common(texts, t => t.BackgroundCornerRadius, fallback.BackgroundCornerRadius),
            Decorations = Common(texts, t => t.Decorations, fallback.Decorations),
            Shadow = Common(texts, t => t.Shadow, fallback.Shadow),
        };

    private static MarkerStyle CaptureMarker(MarkerStyle fallback, List<MarkerAnnotation> markers) => markers.Count == 0
        ? fallback
        : new MarkerStyle
        {
            Shape = Common(markers, m => m.Shape, fallback.Shape),
            Size = Common(markers, m => m.Size, fallback.Size),
            Fill = Common(markers, m => m.Fill, fallback.Fill),
            Stroke = CommonStroke(markers, m => m.Stroke, fallback.Stroke),
            TextColor = Common(markers, m => m.TextColor, fallback.TextColor),
            FontFamilyName = Common(markers, m => m.FontFamilyName, fallback.FontFamilyName),
            Shadow = Common(markers, m => m.Shadow, fallback.Shadow),
        };

    private static BlurStyle CaptureBlur(BlurStyle fallback, List<BlurAnnotation> blurs) => blurs.Count == 0
        ? fallback
        : new BlurStyle
        {
            Mode = Common(blurs, b => b.Mode, fallback.Mode),
            Strength = Common(blurs, b => b.Strength, fallback.Strength),
            Shape = Common(blurs, b => b.Shape, fallback.Shape),
            FeatherEdge = Common(blurs, b => b.FeatherEdge, fallback.FeatherEdge),
            RedactColor = Common(blurs, b => b.RedactColor, fallback.RedactColor),
        };

    private static CalloutStyle CaptureCallout(CalloutStyle fallback, List<CalloutAnnotation> callouts) => callouts.Count == 0
        ? fallback
        : new CalloutStyle
        {
            Shape = Common(callouts, c => c.Shape, fallback.Shape),
            CornerRadius = Common(callouts, c => c.CornerRadius, fallback.CornerRadius),
            TailWidth = Common(callouts, c => c.TailWidth, fallback.TailWidth),
            TailCurved = Common(callouts, c => c.TailCurved, fallback.TailCurved),
            TailCurveReversed = Common(callouts, c => c.TailCurveReversed, fallback.TailCurveReversed),
            FontFamilyName = Common(callouts, c => c.FontFamilyName, fallback.FontFamilyName),
            FontWeight = Common(callouts, c => c.FontWeight, fallback.FontWeight),
            FontStyle = Common(callouts, c => c.FontStyle, fallback.FontStyle),
            FontStretch = Common(callouts, c => c.FontStretch, fallback.FontStretch),
            FontSize = Common(callouts, c => c.FontSize, fallback.FontSize),
            Decorations = Common(callouts, c => c.Decorations, fallback.Decorations),
            TextAlignment = Common(callouts, c => c.TextAlignment, fallback.TextAlignment),
            TextColor = Common(callouts, c => c.TextColor, fallback.TextColor),
            Background = Common(callouts, c => c.Background, fallback.Background),
            Border = CommonStroke(callouts, c => c.Border, fallback.Border),
            BorderBlur = Common(callouts, c => c.BorderBlur, fallback.BorderBlur),
            Padding = Common(callouts, c => c.Padding, fallback.Padding),
            Opacity = Common(callouts, c => c.Opacity, fallback.Opacity),
            Shadow = Common(callouts, c => c.Shadow, fallback.Shadow),
        };

    private static ImageStyle CaptureImage(ImageStyle fallback, List<ImageAnnotation> images) => images.Count == 0
        ? fallback
        : new ImageStyle
        {
            Opacity = Common(images, i => i.Opacity, fallback.Opacity),
            Shadow = Common(images, i => i.Shadow, fallback.Shadow),
            Outline = CommonStroke(images, i => i.Outline, fallback.Outline),
            OutlineBlur = Common(images, i => i.OutlineBlur, fallback.OutlineBlur),
        };

    private static MagnifierStyle CaptureMagnifier(MagnifierStyle fallback, List<MagnifierAnnotation> magnifiers) => magnifiers.Count == 0
        ? fallback
        : new MagnifierStyle
        {
            Zoom = Common(magnifiers, m => m.Zoom, fallback.Zoom),
            Shape = Common(magnifiers, m => m.Shape, fallback.Shape),
            Border = CommonStroke(magnifiers, m => m.Border, fallback.Border),
            ShowConnector = Common(magnifiers, m => m.ShowConnector, fallback.ShowConnector),
            ShowSourceFrame = Common(magnifiers, m => m.ShowSourceFrame, fallback.ShowSourceFrame),
            BaseImageOnly = Common(magnifiers, m => m.BaseImageOnly, fallback.BaseImageOnly),
            SmoothScaling = Common(magnifiers, m => m.SmoothScaling, fallback.SmoothScaling),
        };
}
