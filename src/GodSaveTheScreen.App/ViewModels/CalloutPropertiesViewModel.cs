using System.Globalization;
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
/// Eigenschaften der ausgewählten Sprechblasen (gemischte Werte = <c>null</c>); ohne Auswahl die Standardwerte des
/// Sprechblasen-Werkzeugs. Änderungen an Objekten laufen über den <see cref="UndoManager"/>; Schatten und Deckkraft
/// ausgewählter Blasen stehen in den allgemeinen Abschnitten.
/// </summary>
public sealed partial class CalloutPropertiesViewModel : ObservableObject, IDisposable
{
    private static readonly double[] SizeList = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 64, 72, 96, 120, 144];

    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly IReadOnlyList<double> _sizes = SizeList;
    private IReadOnlyList<FontItem>? _fonts;
    private string _fontsRecentKey = string.Empty;
    private IReadOnlyList<TypefaceItem>? _typefaces;
    private string _typefacesFamily = string.Empty;

    public CalloutPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        ShapeChoices =
        [
            new Choice<CalloutShape>(Strings.Callout_Shape_Rounded, CalloutShape.Rounded),
            new Choice<CalloutShape>(Strings.Callout_Shape_Ellipse, CalloutShape.Ellipse),
            new Choice<CalloutShape>(Strings.Callout_Shape_Cloud, CalloutShape.Cloud),
        ];
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Callout.Changed += OnStateChanged;
        _context.Defaults.RecentColorsChanged += OnStateChanged;
    }

    public IReadOnlyList<Choice<CalloutShape>> ShapeChoices { get; }

    public IReadOnlyList<double> FontSizes => _sizes;

    private IReadOnlyList<CalloutAnnotation> Callouts => [.. _context.Selection.Items.OfType<CalloutAnnotation>()];

    private bool IsDefaultsMode => _context.Selection.IsEmpty && _activeTool() is CalloutTool;

    public bool IsApplicable => Callouts.Count > 0 || IsDefaultsMode;

    public bool ShowDefaults => IsDefaultsMode;

    /// <summary>Eckenradius nur bei der abgerundeten Form (bzw. gemischter Auswahl).</summary>
    public bool ShowCornerRadius => Shape is null or CalloutShape.Rounded;

    /// <summary>Gebogene Spitze gibt es nicht bei der Gedankenblase (Kreise).</summary>
    public bool ShowTailCurved => Shape is not CalloutShape.Cloud;

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

    public IReadOnlyList<TypefaceItem> Typefaces
    {
        get
        {
            var family = FontFamilyName ?? _context.Callout.Style.FontFamilyName;
            if (_typefaces is null || !string.Equals(family, _typefacesFamily, StringComparison.Ordinal))
            {
                _typefaces = FontCatalog.GetTypefaces(family);
                _typefacesFamily = family;
            }

            return _typefaces;
        }
    }

    public bool IsFontMissing => FontFamilyName is { } name && !FontCatalog.Exists(name);

    public CalloutShape? Shape
    {
        get => TryGet(c => c.Shape, s => s.Shape, out var v) ? v : null;
        set => SetNullable(nameof(Shape), value, c => c.Shape, (c, v) => c.Shape = v, (s, v) => s with { Shape = v });
    }

    public string? FontFamilyName
    {
        get => TryGet(c => c.FontFamilyName, s => s.FontFamilyName, out var v) ? v : null;
        set
        {
            if (value is null)
            {
                return;
            }

            Set(nameof(FontFamilyName), value, c => c.FontFamilyName, (c, v) => c.FontFamilyName = v, (s, v) => s with { FontFamilyName = v });
            _context.Defaults.AddRecentFont(value);
        }
    }

    public int? FontWeight
    {
        get => TryGet(c => c.FontWeight, s => s.FontWeight, out var v) ? v : null;
        set => SetNullable(nameof(FontWeight), value, c => c.FontWeight, (c, v) => c.FontWeight = v, (s, v) => s with { FontWeight = v });
    }

    public FontStyleKind? FontStyle
    {
        get => TryGet(c => c.FontStyle, s => s.FontStyle, out var v) ? v : null;
        set => SetNullable(nameof(FontStyle), value, c => c.FontStyle, (c, v) => c.FontStyle = v, (s, v) => s with { FontStyle = v });
    }

    public int? FontStretch
    {
        get => TryGet(c => c.FontStretch, s => s.FontStretch, out var v) ? v : null;
        set => SetNullable(nameof(FontStretch), value, c => c.FontStretch, (c, v) => c.FontStretch = v, (s, v) => s with { FontStretch = v });
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
        get => TryGet(c => c.FontSize, s => s.FontSize, out var v) ? v : null;
        set => SetNullable(
            nameof(FontSize),
            value is { } size ? Math.Clamp(size, CalloutAnnotation.MinFontSize, CalloutAnnotation.MaxFontSize) : null,
            c => c.FontSize,
            (c, v) => c.FontSize = v,
            (s, v) => s with { FontSize = v });
    }

    /// <summary>Für die editierbare Größen-ComboBox (freie Eingabe).</summary>
    public string FontSizeText
    {
        get => FontSize is { } size ? size.ToString("0.##", CultureInfo.CurrentUICulture) : string.Empty;
        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentUICulture, out var size) && size > 0)
            {
                FontSize = size;
            }
        }
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
        get => TryGet(c => c.Decorations, s => s.Decorations, out var v) ? v : null;
        set => SetNullable(nameof(Decorations), value, c => c.Decorations, (c, v) => c.Decorations = v, (s, v) => s with { Decorations = v });
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
        get => TryGet(c => c.TextAlignment, s => s.TextAlignment, out var v) ? v : null;
        set => SetNullable(nameof(Alignment), value, c => c.TextAlignment, (c, v) => c.TextAlignment = v, (s, v) => s with { TextAlignment = v });
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
        get => TryGet(c => c.TextColor, s => s.TextColor, out var v) ? v : null;
        set => SetNullable(nameof(TextColor), value, c => c.TextColor, (c, v) => c.TextColor = v, (s, v) => s with { TextColor = v });
    }

    public ArgbColor? BackgroundColor
    {
        get => TryGet(c => c.Background, s => s.Background, out var v) ? v : null;
        set => SetNullable(nameof(BackgroundColor), value, c => c.Background, (c, v) => c.Background = v, (s, v) => s with { Background = v });
    }

    /// <summary>Leuchtfarbe = Farbe des Rahmens.</summary>
    public ArgbColor? BorderColor
    {
        get => TryGet(c => c.Border.Color, s => s.Border.Color, out var v) ? v : null;
        set => SetNullable(nameof(BorderColor), value, c => c.Border.Color, (c, v) => c.Border = c.Border with { Color = v }, (s, v) => s with { Border = s.Border with { Color = v } });
    }

    public double? BorderThickness
    {
        get => TryGet(c => c.Border.Thickness, s => s.Border.Thickness, out var v) ? v : null;
        set => SetNullable(
            nameof(BorderThickness),
            value is { } t ? Math.Clamp(t, 0, CalloutAnnotation.MaxBorder) : null,
            c => c.Border.Thickness,
            (c, v) => c.Border = c.Border with { Thickness = v },
            (s, v) => s with { Border = s.Border with { Thickness = v } });
    }

    /// <summary>Weicher Rand des Rahmens (Leuchten) in Bildpixeln.</summary>
    public double? BorderBlur
    {
        get => TryGet(c => c.BorderBlur, s => s.BorderBlur, out var v) ? v : null;
        set => SetNullable(
            nameof(BorderBlur),
            value is { } b ? Math.Clamp(b, 0, CalloutAnnotation.MaxBorderBlur) : null,
            c => c.BorderBlur,
            (c, v) => c.BorderBlur = v,
            (s, v) => s with { BorderBlur = v });
    }

    public double? CornerRadius
    {
        get => TryGet(c => c.CornerRadius, s => s.CornerRadius, out var v) ? v : null;
        set => SetNullable(
            nameof(CornerRadius),
            value is { } r ? Math.Clamp(r, 0, CalloutAnnotation.MaxCornerRadius) : null,
            c => c.CornerRadius,
            (c, v) => c.CornerRadius = v,
            (s, v) => s with { CornerRadius = v });
    }

    public double? TailWidth
    {
        get => TryGet(c => c.TailWidth, s => s.TailWidth, out var v) ? v : null;
        set => SetNullable(
            nameof(TailWidth),
            value is { } w ? Math.Clamp(w, 1, CalloutAnnotation.MaxTailWidth) : null,
            c => c.TailWidth,
            (c, v) => c.TailWidth = v,
            (s, v) => s with { TailWidth = v });
    }

    public bool TailCurved
    {
        get => TryGet(c => c.TailCurved, s => s.TailCurved, out var v) && v;
        set => Set(nameof(TailCurved), value, c => c.TailCurved, (c, v) => c.TailCurved = v, (s, v) => s with { TailCurved = v });
    }

    /// <summary>Gebogene Spitze in die andere Richtung biegen.</summary>
    public bool TailCurveReversed
    {
        get => TryGet(c => c.TailCurveReversed, s => s.TailCurveReversed, out var v) && v;
        set => Set(nameof(TailCurveReversed), value, c => c.TailCurveReversed, (c, v) => c.TailCurveReversed = v, (s, v) => s with { TailCurveReversed = v });
    }

    /// <summary>Drehung gibt es nur für ausgewählte Blasen, nicht als Werkzeug-Vorgabe.</summary>
    public bool ShowRotation => Callouts.Count > 0;

    /// <summary>Drehung der ausgewählten Blasen in Grad (−180 bis 180; gemischt = <c>null</c>).</summary>
    public double? Rotation
    {
        get => Callouts.Count > 0 && StyleEditing.TryCommon(Callouts, c => Math.Round(c.SignedRotation, 1), out var v) ? v : null;
        set
        {
            if (value is not { } degrees || Callouts.Count == 0)
            {
                return;
            }

            StyleEditing.Apply(_context, nameof(Rotation), Callouts, RotationMath.Normalize(Math.Clamp(degrees, -180, 180)), c => c.Rotation, (c, v) => c.Rotation = v, Strings.Callout_Rotation);
            Refresh();
        }
    }

    public double RotationValue
    {
        get => Rotation ?? 0;
        set => Rotation = Math.Round(value);
    }

    /// <summary>Abstand zwischen Text und Rand.</summary>
    public double? Padding
    {
        get => TryGet(c => c.Padding, s => s.Padding, out var v) ? v : null;
        set => SetNullable(
            nameof(Padding),
            value is { } p ? Math.Clamp(p, 0, CalloutAnnotation.MaxPadding) : null,
            c => c.Padding,
            (c, v) => c.Padding = v,
            (s, v) => s with { Padding = v });
    }

    /// <summary>Deckkraft neuer Sprechblasen in Prozent (Werkzeug-Vorgabe; ausgewählte Blasen im allgemeinen Abschnitt).</summary>
    public double? DefaultOpacityPercent
    {
        get => Math.Round(_context.Callout.Style.Opacity * 100);
        set
        {
            if (value is { } percent)
            {
                _context.Callout.Style = _context.Callout.Style with { Opacity = Math.Clamp(percent, 0, 100) / 100 };
            }
        }
    }

    public double DefaultOpacityValue
    {
        get => DefaultOpacityPercent ?? 100;
        set => DefaultOpacityPercent = Math.Round(value);
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Callout.Changed -= OnStateChanged;
        _context.Defaults.RecentColorsChanged -= OnStateChanged;
    }

    [RelayCommand]
    private void ResetRotation() => Rotation = 0;

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

    private bool TryGet<TV>(Func<CalloutAnnotation, TV> fromCallout, Func<CalloutStyle, TV> fromStyle, out TV value)
    {
        if (IsDefaultsMode)
        {
            value = fromStyle(_context.Callout.Style);
            return true;
        }

        return StyleEditing.TryCommon(Callouts, fromCallout, out value);
    }

    private void SetNullable<TV>(string key, TV? value, Func<CalloutAnnotation, TV> get, Action<CalloutAnnotation, TV> set, Func<CalloutStyle, TV, CalloutStyle> update)
        where TV : struct
    {
        if (value is { } v)
        {
            Set(key, v, get, set, update);
        }
    }

    private void Set<TV>(string key, TV value, Func<CalloutAnnotation, TV> get, Action<CalloutAnnotation, TV> set, Func<CalloutStyle, TV, CalloutStyle> update)
    {
        if (IsDefaultsMode)
        {
            _context.Callout.Style = update(_context.Callout.Style, value);
        }
        else
        {
            // Die Werte der Auswahl werden über SelectionStyleEdited auch Vorgabe neuer Sprechblasen.
            StyleEditing.Apply(_context, key, Callouts, value, get, set, Strings.Callout_Property);
        }

        Refresh();
    }
}
