using System.Windows.Input;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Überträgt Standardwerte bzw. Vorlagen auf Text- und Markerobjekte.</summary>
public static class StyleApplier
{
    /// <summary>Alle Felder des Stils; mit <paramref name="keepFont"/> bleiben Schriftart, Größe und Schnitt erhalten (Vorlagen).</summary>
    public static void Apply(TextAnnotation text, TextStyle style, bool keepFont = false)
    {
        if (!keepFont)
        {
            text.FontFamilyName = style.FontFamilyName;
            text.FontSize = style.FontSize;
        }

        text.FontWeight = style.FontWeight;
        text.FontStyle = style.FontStyle;
        text.FontStretch = style.FontStretch;
        text.Fill = style.Fill;
        text.Stroke = style.Stroke;
        text.OutlineBlur = style.OutlineBlur;
        text.TextAlignment = style.TextAlignment;
        text.Background = style.Background;
        text.BackgroundPadding = style.BackgroundPadding;
        text.BackgroundCornerRadius = style.BackgroundCornerRadius;
        text.Decorations = style.Decorations;
        text.Shadow = style.Shadow ?? ShadowStyle.None;
    }

    public static void Apply(MarkerAnnotation marker, MarkerStyle style)
    {
        marker.Shape = style.Shape;
        marker.Size = style.Size;
        marker.Fill = style.Fill;
        marker.Stroke = style.Stroke;
        marker.TextColor = style.TextColor;
        marker.FontFamilyName = style.FontFamilyName;
        marker.Shadow = style.Shadow ?? ShadowStyle.None;
    }
}

/// <summary>Text: Klick setzt einen Text, Ziehen zieht eine Textbox mit Umbruchbreite; danach sofort Bearbeitung.</summary>
public sealed class TextTool : ToolBase
{
    public const string ToolId = "text";

    private const double MinimumBoxWidth = 12;

    private PointD _start;
    private bool _dragging;

    public TextTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Text";

    public override SymbolRegular Symbol => SymbolRegular.TextT24;

    public override Key ShortcutKey => Key.T;

    public override Cursor Cursor => Cursors.IBeam;

    public override bool HasStyleDefaults => true;

    public override void Deactivate() => Cancel();

    public override void Cancel()
    {
        _dragging = false;
        Context.Interaction.RubberBand = null;
        Context.InvalidateInteraction();
    }

    public override void OnPointerDown(EditorPointerArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var hit = Context.HitTest(args.Position);
        if (hit is TextAnnotation existing && !existing.IsLocked)
        {
            // Klick auf einen vorhandenen Text bearbeitet ihn.
            Context.BeginTextEdit(existing, isNew: false);
            return;
        }

        if (args.Ctrl && hit is not null)
        {
            Context.Selection.Set(hit);
            return;
        }

        _start = args.Position;
        _dragging = true;
        Context.Selection.Clear();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (!_dragging)
        {
            return;
        }

        Context.Interaction.RubberBand = RectD.FromPoints(_start, args.Position);
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Context.Interaction.RubberBand = null;
        var text = CreateText();
        var box = RectD.FromPoints(_start, args.Position);
        if (box.Width >= MinimumBoxWidth)
        {
            text.Position = box.TopLeft;
            text.WrapWidth = box.Width;
        }
        else
        {
            text.Position = _start;
        }

        Context.InvalidateInteraction();
        Context.BeginTextEdit(text, isNew: true);
    }

    public override bool OnKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape && _dragging)
        {
            Cancel();
            return true;
        }

        return false;
    }

    private TextAnnotation CreateText()
    {
        var text = new TextAnnotation();
        StyleApplier.Apply(text, Context.Defaults.GetText());
        return text;
    }
}

/// <summary>Marker: Klick setzt die nächste freie Nummer, Ziehen erzeugt zusätzlich eine Spitze zum Zielpunkt.</summary>
public sealed class MarkerTool : ToolBase
{
    public const string ToolId = "marker";

    private PointD _start;
    private MarkerAnnotation? _preview;

    public MarkerTool(EditorContext context)
        : base(context)
    {
    }

    public override string Id => ToolId;

    public override string NameKey => "Tool_Marker";

    public override SymbolRegular Symbol => SymbolRegular.NumberCircle124;

    public override Key ShortcutKey => Key.M;

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
        var marker = new MarkerAnnotation
        {
            Center = _start,
            Number = MarkerNumbering.NextNumber(Context.Document.Annotations, Context.Defaults.MarkerStartNumber),
        };
        StyleApplier.Apply(marker, Context.Defaults.GetMarker());
        _preview = marker;
        Context.Interaction.Preview = marker;
        Context.Selection.Clear();
        Context.InvalidateInteraction();
    }

    public override void OnPointerMove(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        UpdatePointer(_preview, args);
        Context.InvalidateInteraction();
    }

    public override void OnPointerUp(EditorPointerArgs args)
    {
        if (_preview is null)
        {
            return;
        }

        var marker = _preview;
        UpdatePointer(marker, args);
        _preview = null;
        Context.Interaction.Preview = null;
        marker.Name = AnnotationNames.NextName(Context.Document.Annotations, marker);
        Context.Undo.Execute(new AddAnnotationAction(Context.Document.Annotations, marker, description: "Marker setzen"));
        Context.Selection.Set(marker);
        Context.InvalidateInteraction();
        if (Context.Settings.Current.SwitchToSelectAfterDraw)
        {
            Context.ActivateTool(SelectTool.ToolId);
        }
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

    private void UpdatePointer(MarkerAnnotation marker, EditorPointerArgs args)
    {
        // Erst wenn der Zeiger deutlich aus dem Marker herausgezogen wird, entsteht eine Spitze.
        marker.PointerTarget = args.Position.DistanceTo(_start) > marker.Size * 0.75 ? args.Position : null;
    }
}
