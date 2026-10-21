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
/// Eigenschaften der ausgewählten Stempel (gemischte Werte = <c>null</c>); ohne Auswahl die Vorgaben des Stempelwerkzeugs.
/// Änderungen an Objekten laufen über den <see cref="UndoManager"/>. Die Deckkraft ausgewählter Stempel steht im allgemeinen Panel.
/// </summary>
public sealed partial class StampPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private IReadOnlyList<FontItem>? _fonts;
    private string _fontsRecentKey = string.Empty;
    private IReadOnlyList<TypefaceItem>? _typefaces;
    private string _typefacesFamily = string.Empty;

    public StampPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        FrameChoices =
        [
            new Choice<StampFrameShape>(Strings.Stamp_Frame_Rectangle, StampFrameShape.Rectangle),
            new Choice<StampFrameShape>(Strings.Stamp_Frame_Rounded, StampFrameShape.Rounded),
            new Choice<StampFrameShape>(Strings.Stamp_Frame_Double, StampFrameShape.DoubleLine),
            new Choice<StampFrameShape>(Strings.Stamp_Frame_Oval, StampFrameShape.Oval),
        ];
        QuickTexts =
        [
            Strings.Stamp_Text_Approved,
            Strings.Stamp_Text_Done,
            Strings.Stamp_Text_Confidential,
            Strings.Stamp_Text_Draft,
            Strings.Stamp_Text_Rejected,
            Strings.Stamp_Text_Important,
        ];
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Stamp.Changed += OnStateChanged;
    }

    public IReadOnlyList<Choice<StampFrameShape>> FrameChoices { get; }

    /// <summary>Schnellauswahl häufiger Stempeltexte (Sprache der Oberfläche).</summary>
    public IReadOnlyList<string> QuickTexts { get; }

    private IReadOnlyList<StampAnnotation> Stamps => [.. _context.Selection.Items.OfType<StampAnnotation>()];

    private bool IsDefaultsMode => _context.Selection.IsEmpty && _activeTool() is StampTool;

    public bool IsApplicable => Stamps.Count > 0 || IsDefaultsMode;

    public bool HasStamps => Stamps.Count > 0;

    public bool ShowDefaults => IsDefaultsMode;

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
            var family = FontFamilyName ?? _context.Stamp.Style.FontFamilyName;
            if (_typefaces is null || !string.Equals(family, _typefacesFamily, StringComparison.Ordinal))
            {
                _typefaces = FontCatalog.GetTypefaces(family);
                _typefacesFamily = family;
            }

            return _typefaces;
        }
    }

    public bool IsFontMissing => FontFamilyName is { } name && !FontCatalog.Exists(name);

    /// <summary>Text der Auswahl bzw. des nächsten Stempels (Bearbeitung direkt im Objekt; hier nur zur Anzeige der Schnellauswahl).</summary>
    public string? Text => IsDefaultsMode ? _context.Stamp.Text : StyleEditing.TryCommon(Stamps, s => s.Text, out var v) ? v : null;

    public string? FontFamilyName
    {
        get => TryGet(s => s.FontFamilyName, s => s.FontFamilyName, out var v) ? v : null;
        set
        {
            if (value is null)
            {
                return;
            }

            Set(nameof(FontFamilyName), value, s => s.FontFamilyName, (s, v) => s.FontFamilyName = v, (s, v) => s with { FontFamilyName = v });
            _context.Defaults.AddRecentFont(value);
        }
    }

    public int? FontWeight
    {
        get => TryGet(s => s.FontWeight, s => s.FontWeight, out var v) ? v : null;
        set => SetNullable(nameof(FontWeight), value, s => s.FontWeight, (s, v) => s.FontWeight = v, (s, v) => s with { FontWeight = v });
    }

    public FontStyleKind? FontStyle
    {
        get => TryGet(s => s.FontStyle, s => s.FontStyle, out var v) ? v : null;
        set => SetNullable(nameof(FontStyle), value, s => s.FontStyle, (s, v) => s.FontStyle = v, (s, v) => s with { FontStyle = v });
    }

    public int? FontStretch
    {
        get => TryGet(s => s.FontStretch, s => s.FontStretch, out var v) ? v : null;
        set => SetNullable(nameof(FontStretch), value, s => s.FontStretch, (s, v) => s.FontStretch = v, (s, v) => s with { FontStretch = v });
    }

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
        get => TryGet(s => s.Decorations, s => s.Decorations, out var v) ? v : null;
        set => SetNullable(nameof(Decorations), value, s => s.Decorations, (s, v) => s.Decorations = v, (s, v) => s with { Decorations = v });
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

    /// <summary>Schriftgröße; Rahmen und Abstände wachsen mit.</summary>
    public double? FontSize
    {
        get => TryGet(s => Math.Round(s.FontSize, 1), s => s.FontSize, out var v) ? v : null;
        set => SetNullable(
            nameof(FontSize),
            value is { } size ? Math.Clamp(size, StampAnnotation.MinFontSize, StampAnnotation.MaxFontSize) : null,
            s => s.FontSize,
            (s, v) => s.FontSize = v,
            (s, v) => s with { FontSize = v });
    }

    public ArgbColor? Color
    {
        get => TryGet(s => s.Color, s => s.Color, out var v) ? v : null;
        set => SetNullable(nameof(Color), value, s => s.Color, (s, v) => s.Color = v, (s, v) => s with { Color = v });
    }

    public StampFrameShape? FrameShape
    {
        get => TryGet(s => s.FrameShape, s => s.FrameShape, out var v) ? v : null;
        set => SetNullable(nameof(FrameShape), value, s => s.FrameShape, (s, v) => s.FrameShape = v, (s, v) => s with { FrameShape = v });
    }

    public double? FrameThickness
    {
        get => TryGet(s => Math.Round(s.FrameThickness, 1), s => s.FrameThickness, out var v) ? v : null;
        set => SetNullable(
            nameof(FrameThickness),
            value is { } t ? Math.Clamp(t, 0, StampAnnotation.MaxFrameThickness) : null,
            s => s.FrameThickness,
            (s, v) => s.FrameThickness = v,
            (s, v) => s with { FrameThickness = v });
    }

    public double FrameThicknessValue
    {
        get => FrameThickness ?? 0;
        set => FrameThickness = Math.Round(value, 1);
    }

    /// <summary>Stärke des Stempel-Looks 0–100.</summary>
    public double? Wear
    {
        get => TryGet(s => Math.Round(s.Wear), s => s.Wear, out var v) ? v : null;
        set => SetNullable(
            nameof(Wear),
            value is { } w ? Math.Clamp(Math.Round(w), 0, StampAnnotation.MaxWear) : null,
            s => s.Wear,
            (s, v) => s.Wear = v,
            (s, v) => s with { Wear = v });
    }

    public double WearValue
    {
        get => Wear ?? 0;
        set => Wear = value;
    }

    /// <summary>Neigung in Grad (−180 bis 180).</summary>
    public double? Rotation
    {
        get => TryGet(s => Math.Round(s.SignedRotation, 1), s => s.Rotation, out var v) ? v : null;
        set
        {
            if (value is not { } degrees)
            {
                return;
            }

            degrees = Math.Clamp(degrees, -180, 180);
            if (IsDefaultsMode)
            {
                _context.Stamp.Style = _context.Stamp.Style with { Rotation = degrees };
            }
            else
            {
                StyleEditing.Apply(_context, nameof(Rotation), Stamps, RotationMath.Normalize(degrees), s => s.Rotation, (s, v) => s.Rotation = v, Strings.Stamp_Rotation);
                _context.Stamp.Style = _context.Stamp.Style with { Rotation = degrees };
            }

            Refresh();
        }
    }

    public double RotationValue
    {
        get => Rotation ?? 0;
        set => Rotation = Math.Round(value);
    }

    public bool InkBlend
    {
        get => TryGet(s => s.InkBlend, s => s.InkBlend, out var v) && v;
        set => Set(nameof(InkBlend), value, s => s.InkBlend, (s, v) => s.InkBlend = v, (s, v) => s with { InkBlend = v });
    }

    /// <summary>Deckkraft neuer Stempel in Prozent (Werkzeug-Vorgabe).</summary>
    public double? DefaultOpacityPercent
    {
        get => Math.Round(_context.Stamp.Style.Opacity * 100);
        set
        {
            if (value is { } percent)
            {
                _context.Stamp.Style = _context.Stamp.Style with { Opacity = Math.Clamp(percent, 0, 100) / 100 };
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
        _context.Stamp.Changed -= OnStateChanged;
    }

    /// <summary>Setzt den Text der ausgewählten Stempel bzw. des nächsten Stempels.</summary>
    [RelayCommand]
    private void ApplyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!IsDefaultsMode)
        {
            StyleEditing.Apply(_context, nameof(Text), Stamps, text, s => s.Text, (s, v) => s.Text = v, Strings.Stamp_Text);
        }

        _context.Stamp.Text = text;

        Refresh();
    }

    /// <summary>Neues Abnutzungsmuster für die ausgewählten Stempel (ein Undo-Schritt).</summary>
    [RelayCommand]
    private void Reroll()
    {
        var targets = Stamps.Where(s => !s.IsLocked).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction(Strings.Stamp_Reroll))
        {
            foreach (var stamp in targets)
            {
                var target = stamp;
                _context.Undo.Execute(new PropertyChangeAction<int>(target, nameof(StampAnnotation.Seed), target.Seed, StampToolState.NewSeed(), v => target.Seed = v, Strings.Stamp_Reroll));
            }
        }

        Refresh();
    }

    private static TextDecorationKind Toggle(TextDecorationKind current, TextDecorationKind flag, bool on) =>
        on ? current | flag : current & ~flag;

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private bool TryGet<TV>(Func<StampAnnotation, TV> fromStamp, Func<StampStyle, TV> fromStyle, out TV value)
    {
        if (IsDefaultsMode)
        {
            value = fromStyle(_context.Stamp.Style);
            return true;
        }

        return StyleEditing.TryCommon(Stamps, fromStamp, out value);
    }

    private void SetNullable<TV>(string key, TV? value, Func<StampAnnotation, TV> get, Action<StampAnnotation, TV> set, Func<StampStyle, TV, StampStyle> update)
        where TV : struct
    {
        if (value is { } v)
        {
            Set(key, v, get, set, update);
        }
    }

    private void Set<TV>(string key, TV value, Func<StampAnnotation, TV> get, Action<StampAnnotation, TV> set, Func<StampStyle, TV, StampStyle> update)
    {
        if (IsDefaultsMode)
        {
            _context.Stamp.Style = update(_context.Stamp.Style, value);
        }
        else
        {
            // Wie bei den übrigen Werkzeugen wird die zuletzt eingestellte Konfiguration Vorgabe neuer Stempel.
            StyleEditing.Apply(_context, key, Stamps, value, get, set, Strings.Stamp_Property);
            _context.Stamp.Style = update(_context.Stamp.Style, value);
        }

        Refresh();
    }
}
