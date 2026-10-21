using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Texteigenschaften der Auswahl (gemischte Werte = <c>null</c>); ohne Auswahl die Standardwerte des Textwerkzeugs.
/// Änderungen laufen über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class TextPropertiesViewModel : ObservableObject, IDisposable
{

    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly EffectSwitch<double> _outlineSwitch = new(t => t > 0, 0, 2);
    private readonly EffectSwitch<ArgbColor> _backgroundSwitch = new(c => !c.IsTransparent, ArgbColor.Transparent, ArgbColor.White);
    private IReadOnlyList<FontItem>? _fonts;
    private string _fontsRecentKey = string.Empty;
    private IReadOnlyList<TypefaceItem>? _typefaces;
    private string _typefacesFamily = string.Empty;

    public TextPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Defaults.RecentColorsChanged += OnStateChanged;
    }

    private IReadOnlyList<TextAnnotation> Texts => [.. _context.Selection.Items.OfType<TextAnnotation>()];

    private ToolBase? DefaultsTool => _context.Selection.IsEmpty && _activeTool() is TextTool tool ? tool : null;

    public bool IsApplicable => Texts.Count > 0 || DefaultsTool is not null;

    /// <summary>Alle Schriften: zuletzt verwendete oben, danach alphabetisch (Liste bleibt stabil, bis sich die Verlaufsliste ändert).</summary>
    public IReadOnlyList<FontItem> Fonts
    {
        get
        {
            var recentKey = string.Join('|', _context.Defaults.RecentFonts);
            if (_fonts is null || recentKey != _fontsRecentKey)
            {
                var recent = _context.Defaults.RecentFonts.Select(FontCatalog.Find).OfType<FontItem>().ToList();
                _fonts = [.. recent, .. FontCatalog.All.Where(f => !recent.Contains(f))];
                _fontsRecentKey = recentKey;
            }

            return _fonts;
        }
    }

    /// <summary>Schnitte der aktuellen Schriftfamilie (Liste bleibt stabil, solange die Familie gleich ist).</summary>
    public IReadOnlyList<TypefaceItem> Typefaces
    {
        get
        {
            var family = FontFamilyName ?? _context.Defaults.GetText().FontFamilyName;
            if (_typefaces is null || !string.Equals(family, _typefacesFamily, StringComparison.Ordinal))
            {
                _typefaces = FontCatalog.GetTypefaces(family);
                _typefacesFamily = family;
            }

            return _typefaces;
        }
    }

    /// <summary>Gespeicherte Schrift ist nicht installiert (Ersatzschrift wird verwendet).</summary>
    public bool IsFontMissing => FontFamilyName is { } name && !FontCatalog.Exists(name);

    public string? FontFamilyName
    {
        get => TryGet(t => t.FontFamilyName, s => s.FontFamilyName, out var v) ? v : null;
        set
        {
            if (value is null)
            {
                return;
            }

            Set(nameof(FontFamilyName), value, t => t.FontFamilyName, (t, v) => t.FontFamilyName = v, (s, v) => s with { FontFamilyName = v });
            _context.Defaults.AddRecentFont(value);
        }
    }

    public int? FontWeight
    {
        get => TryGet(t => t.FontWeight, s => s.FontWeight, out var v) ? v : null;
        set => SetNullable(nameof(FontWeight), value, t => t.FontWeight, (t, v) => t.FontWeight = v, (s, v) => s with { FontWeight = v });
    }

    public FontStyleKind? FontStyle
    {
        get => TryGet(t => t.FontStyle, s => s.FontStyle, out var v) ? v : null;
        set => SetNullable(nameof(FontStyle), value, t => t.FontStyle, (t, v) => t.FontStyle = v, (s, v) => s with { FontStyle = v });
    }

    public int? FontStretch
    {
        get => TryGet(t => t.FontStretch, s => s.FontStretch, out var v) ? v : null;
        set => SetNullable(nameof(FontStretch), value, t => t.FontStretch, (t, v) => t.FontStretch = v, (s, v) => s with { FontStretch = v });
    }

    /// <summary>Schnitt (Gewicht + Stil + Breite) der Schriftfamilie; setzt alle drei Werte gemeinsam.</summary>
    public TypefaceItem? SelectedTypeface
    {
        get
        {
            if (FontWeight is not { } weight || FontStyle is not { } style || FontStretch is not { } stretch)
            {
                return null;
            }

            var faces = Typefaces;
            return faces.FirstOrDefault(f => f.Weight == weight && f.Style == style && f.Stretch == stretch)
                   ?? faces.OrderBy(f => Math.Abs(f.Weight - weight) + (f.Style == style ? 0 : 1000) + (Math.Abs(f.Stretch - stretch) * 100)).FirstOrDefault();
        }

        set
        {
            if (value is null)
            {
                return;
            }

            using (_context.Undo.BeginTransaction(Strings.Props_Typeface))
            {
                FontWeight = value.Weight;
                FontStyle = value.Style;
                FontStretch = value.Stretch;
            }
        }
    }

    public double? FontSize
    {
        get => TryGet(t => t.FontSize, s => s.FontSize, out var v) ? v : null;
        set => SetNullable(nameof(FontSize), value is { } size ? Math.Clamp(size, 1, 999) : null, t => t.FontSize, (t, v) => t.FontSize = v, (s, v) => s with { FontSize = v });
    }

    public bool IsBold
    {
        get => (FontWeight ?? 400) >= 600;
        set => FontWeight = value ? 700 : 400;
    }

    public bool IsItalic
    {
        get => (FontStyle ?? FontStyleKind.Normal) != FontStyleKind.Normal;
        set => FontStyle = value ? FontStyleKind.Italic : FontStyleKind.Normal;
    }

    public TextDecorationKind? Decorations
    {
        get => TryGet(t => t.Decorations, s => s.Decorations, out var v) ? v : null;
        set => SetNullable(nameof(Decorations), value, t => t.Decorations, (t, v) => t.Decorations = v, (s, v) => s with { Decorations = v });
    }

    public bool IsUnderline
    {
        get => (Decorations ?? TextDecorationKind.None).HasFlag(TextDecorationKind.Underline);
        set => Decorations = Toggle(Decorations ?? TextDecorationKind.None, TextDecorationKind.Underline, value);
    }

    public bool IsStrikethrough
    {
        get => (Decorations ?? TextDecorationKind.None).HasFlag(TextDecorationKind.Strikethrough);
        set => Decorations = Toggle(Decorations ?? TextDecorationKind.None, TextDecorationKind.Strikethrough, value);
    }

    public TextAlignmentKind? Alignment
    {
        get => TryGet(t => t.TextAlignment, s => s.TextAlignment, out var v) ? v : null;
        set => SetNullable(nameof(Alignment), value, t => t.TextAlignment, (t, v) => t.TextAlignment = v, (s, v) => s with { TextAlignment = v });
    }

    public bool IsAlignLeft
    {
        get => Alignment == TextAlignmentKind.Left;
        set => SetAlignment(value, TextAlignmentKind.Left);
    }

    public bool IsAlignCenter
    {
        get => Alignment == TextAlignmentKind.Center;
        set => SetAlignment(value, TextAlignmentKind.Center);
    }

    public bool IsAlignRight
    {
        get => Alignment == TextAlignmentKind.Right;
        set => SetAlignment(value, TextAlignmentKind.Right);
    }

    public ArgbColor? TextColor
    {
        get => TryGet(t => t.Fill, s => s.Fill, out var v) ? v : null;
        set => SetNullable(nameof(TextColor), value, t => t.Fill, (t, v) => t.Fill = v, (s, v) => s with { Fill = v });
    }

    public ArgbColor? OutlineColor
    {
        get => TryGet(t => t.Stroke.Color, s => s.Stroke.Color, out var v) ? v : null;
        set => SetNullable(nameof(OutlineColor), value, t => t.Stroke.Color, (t, v) => t.Stroke = t.Stroke with { Color = v }, (s, v) => s with { Stroke = s.Stroke with { Color = v } });
    }

    public double? OutlineThickness
    {
        get => TryGet(t => t.Stroke.Thickness, s => s.Stroke.Thickness, out var v) ? v : null;
        set => SetNullable(nameof(OutlineThickness), value is { } thickness ? Math.Clamp(thickness, 0, 50) : null, t => t.Stroke.Thickness, (t, v) => t.Stroke = t.Stroke with { Thickness = v }, (s, v) => s with { Stroke = s.Stroke with { Thickness = v } });
    }

    /// <summary>Ein/Aus-Schalter der Kontur: aus = Stärke 0, ein = zuletzt verwendete Stärke (sonst 2).</summary>
    public bool? IsOutlineOn
    {
        get => _outlineSwitch.IsOn(OutlineThickness);
        set
        {
            if (value is { } on)
            {
                OutlineThickness = _outlineSwitch.Switch(OutlineThickness, on);
            }
        }
    }

    /// <summary>Weicher Rand der Kontur in Bildpixeln (0 = scharf).</summary>
    public double? OutlineBlur
    {
        get => TryGet(t => t.OutlineBlur, s => s.OutlineBlur, out var v) ? v : null;
        set => SetNullable(nameof(OutlineBlur), value is { } blur ? Math.Clamp(blur, 0, TextAnnotation.MaxOutlineBlur) : null, t => t.OutlineBlur, (t, v) => t.OutlineBlur = v, (s, v) => s with { OutlineBlur = v });
    }

    public ArgbColor? BackgroundColor
    {
        get => TryGet(t => t.Background, s => s.Background, out var v) ? v : null;
        set => SetNullable(nameof(BackgroundColor), value, t => t.Background, (t, v) => t.Background = v, (s, v) => s with { Background = v });
    }

    /// <summary>Ein/Aus-Schalter des Hintergrunds: aus = transparent, ein = zuletzt verwendete Farbe (sonst Weiß).</summary>
    public bool? IsBackgroundOn
    {
        get => _backgroundSwitch.IsOn(BackgroundColor);
        set
        {
            if (value is { } on)
            {
                BackgroundColor = _backgroundSwitch.Switch(BackgroundColor, on);
            }
        }
    }

    public double? BackgroundCornerRadius
    {
        get => TryGet(t => t.BackgroundCornerRadius, s => s.BackgroundCornerRadius, out var v) ? v : null;
        set => SetNullable(nameof(BackgroundCornerRadius), value is { } radius ? Math.Clamp(radius, 0, 500) : null, t => t.BackgroundCornerRadius, (t, v) => t.BackgroundCornerRadius = v, (s, v) => s with { BackgroundCornerRadius = v });
    }

    public double? BackgroundPadding
    {
        get => TryGet(t => t.BackgroundPadding, s => s.BackgroundPadding, out var v) ? v : null;
        set => SetNullable(nameof(BackgroundPadding), value is { } padding ? Math.Clamp(padding, 0, 200) : null, t => t.BackgroundPadding, (t, v) => t.BackgroundPadding = v, (s, v) => s with { BackgroundPadding = v });
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Defaults.RecentColorsChanged -= OnStateChanged;
    }

    private static TextDecorationKind Toggle(TextDecorationKind current, TextDecorationKind flag, bool on) =>
        on ? current | flag : current & ~flag;

    private void SetAlignment(bool selected, TextAlignmentKind alignment)
    {
        if (selected)
        {
            Alignment = alignment;
        }
        else
        {
            Refresh();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private bool TryGet<TV>(Func<TextAnnotation, TV> fromText, Func<TextStyle, TV> fromStyle, out TV value)
    {
        if (DefaultsTool is not null)
        {
            value = fromStyle(_context.Defaults.GetText());
            return true;
        }

        return StyleEditing.TryCommon(Texts, fromText, out value);
    }

    private void SetNullable<TV>(string key, TV? value, Func<TextAnnotation, TV> get, Action<TextAnnotation, TV> set, Func<TextStyle, TV, TextStyle> update)
        where TV : struct
    {
        if (value is { } v)
        {
            Set(key, v, get, set, update);
        }
    }

    private void Set<TV>(string key, TV value, Func<TextAnnotation, TV> get, Action<TextAnnotation, TV> set, Func<TextStyle, TV, TextStyle> update)
    {
        if (DefaultsTool is not null)
        {
            _context.Defaults.SetText(update(_context.Defaults.GetText(), value));
        }
        else
        {
            StyleEditing.Apply(_context, key, Texts, value, get, set);
        }

        Refresh();
    }
}
