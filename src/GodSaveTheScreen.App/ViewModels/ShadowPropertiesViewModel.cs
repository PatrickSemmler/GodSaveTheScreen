using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Schatten der Auswahl (Formen, Linien, Text, Marker, Emoji, Sprechblasen, Bilder; gemischte Werte = <c>null</c>). Ohne Auswahl bearbeitet es
/// die Standardwerte des aktiven Werkzeugs. Freihandstriche haben keinen Schatten. Änderungen laufen über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class ShadowPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;

    public ShadowPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Emoji.Changed += OnStateChanged;
        _context.Callout.Changed += OnStateChanged;
        _context.Image.Changed += OnStateChanged;
    }

    private IReadOnlyList<Annotation> Targets => [.. _context.Selection.Items.Where(Supports)];

    /// <summary>Werkzeug, dessen Standardschatten ohne Auswahl bearbeitet wird.</summary>
    private ToolBase? DefaultsTool => _context.Selection.IsEmpty && _activeTool() is { } tool && SupportsTool(tool) ? tool : null;

    public bool IsApplicable => Targets.Count > 0 || DefaultsTool is not null;

    /// <summary>Schattenfarbe; transparent = kein Schatten.</summary>
    public ArgbColor? ShadowColor
    {
        get => Get(s => s.Color);
        set => Set(nameof(ShadowColor), value, s => s.Color, (s, v) => s with { Color = v });
    }

    /// <summary>Versatz nach rechts unten in Bildpixeln.</summary>
    public double? ShadowSize
    {
        get => Get(s => s.Size);
        set => Set(nameof(ShadowSize), value is { } v ? Math.Clamp(v, 0, ShadowStyle.MaxSize) : null, s => s.Size, (s, v) => s with { Size = v });
    }

    /// <summary>Breite des weichen Rands in Bildpixeln.</summary>
    public double? ShadowBlur
    {
        get => Get(s => s.Blur);
        set => Set(nameof(ShadowBlur), value is { } v ? Math.Clamp(v, 0, ShadowStyle.MaxBlur) : null, s => s.Blur, (s, v) => s with { Blur = v });
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Emoji.Changed -= OnStateChanged;
        _context.Callout.Changed -= OnStateChanged;
        _context.Image.Changed -= OnStateChanged;
    }

    private static bool Supports(Annotation annotation) => annotation is IShadowAnnotation and not StrokeAnnotation;

    private static bool SupportsTool(ToolBase tool) =>
        tool is TextTool or MarkerTool or EmojiTool or CalloutTool or ImageTool || (tool.UsesShapeStyle && tool is not FreehandToolBase);

    private static ShadowStyle ShadowOf(Annotation annotation) => ((IShadowAnnotation)annotation).Shadow ?? ShadowStyle.None;

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private ShadowStyle DefaultShadow(ToolBase tool) => tool switch
    {
        TextTool => _context.Defaults.GetText().Shadow,
        MarkerTool => _context.Defaults.GetMarker().Shadow,
        EmojiTool => _context.Emoji.Shadow,
        CalloutTool => _context.Callout.Style.Shadow,
        ImageTool => _context.Image.Style.Shadow,
        _ => _context.Defaults.GetShape(tool.Id, tool.BuiltInStyle).Shadow,
    } ?? ShadowStyle.None;

    private void SetDefaultShadow(ToolBase tool, ShadowStyle shadow)
    {
        switch (tool)
        {
            case TextTool:
                _context.Defaults.SetText(_context.Defaults.GetText() with { Shadow = shadow });
                break;
            case MarkerTool:
                _context.Defaults.SetMarker(_context.Defaults.GetMarker() with { Shadow = shadow });
                break;
            case EmojiTool:
                _context.Emoji.Shadow = shadow;
                break;
            case CalloutTool:
                _context.Callout.Style = _context.Callout.Style with { Shadow = shadow };
                break;
            case ImageTool:
                _context.Image.Style = _context.Image.Style with { Shadow = shadow };
                break;
            default:
                _context.Defaults.SetShape(tool.Id, _context.Defaults.GetShape(tool.Id, tool.BuiltInStyle) with { Shadow = shadow });
                break;
        }
    }

    private TV? Get<TV>(Func<ShadowStyle, TV> get)
        where TV : struct
    {
        if (DefaultsTool is { } tool)
        {
            return get(DefaultShadow(tool));
        }

        return StyleEditing.TryCommon(Targets, a => get(ShadowOf(a)), out var value) ? value : null;
    }

    private void Set<TV>(string key, TV? value, Func<ShadowStyle, TV> get, Func<ShadowStyle, TV, ShadowStyle> update)
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (DefaultsTool is { } tool)
        {
            SetDefaultShadow(tool, update(DefaultShadow(tool), v));
        }
        else
        {
            StyleEditing.Apply(_context, key, Targets, v, a => get(ShadowOf(a)), (a, x) => ((IShadowAnnotation)a).Shadow = update(ShadowOf(a), x));
        }

        Refresh();
    }
}
