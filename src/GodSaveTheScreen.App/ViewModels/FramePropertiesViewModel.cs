using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Einstellungen des Rahmen-Werkzeugs: bearbeitet <see cref="ImageDocument.Frame"/> (Rand, Schatten, runde Ecken,
/// abgerissene Kante, Vignette, Graustufen). Jede Änderung ist eine Undo-Aktion und wird in den gemerkten Werten
/// (<see cref="FrameToolState.Defaults"/>) abgelegt. Die Ein/Aus-Schalter stellen beim Einschalten die zuletzt genutzten
/// Werte wieder her.
/// </summary>
public sealed partial class FramePropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly Func<ImageAsset?> _pickBackground;
    private readonly Random _random = new();

    /// <summary>Randbreite je Seite getrennt einstellen (nur Panel-Zustand, nicht gespeichert).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBorderPerSide))]
    [NotifyPropertyChangedFor(nameof(IsBorderUniform))]
    private bool _borderPerSide;

    public FramePropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool, Func<ImageAsset?>? pickBackground = null)
    {
        _context = context;
        _activeTool = activeTool;
        _pickBackground = pickBackground ?? (() => null);
        _context.Undo.Changed += OnStateChanged;
        _context.Frame.Changed += OnStateChanged;
    }

    /// <summary>Der Rahmen-Abschnitt ist sichtbar: Rahmen-Werkzeug aktiv.</summary>
    public bool IsApplicable => _activeTool() is FrameTool;

    public bool HasFrame => Frame is not null;

    /// <summary>Aktueller Rahmen (<c>null</c> = keiner) bzw. ein leerer Rahmen als Ausgangspunkt.</summary>
    private FrameStyle? Frame => _context.Document.Frame;

    private FrameStyle Working => Frame ?? new FrameStyle();

    // ---- Rand ----

    public bool? IsBorderOn
    {
        get => Frame?.HasBorder ?? false;
        set => SetEffect(FrameEffect.Border, value);
    }

    public ArgbColor? BorderColor
    {
        get => Working.BorderColor;
        set
        {
            if (value is { } color)
            {
                Update(f => f with { BorderColor = color }, Strings.Frame_Border);
            }
        }
    }

    /// <summary>Gemeinsame Breite aller Seiten (bei getrennten Seiten die größte).</summary>
    public double? BorderWidth
    {
        get => Math.Max(Math.Max(Working.BorderLeft, Working.BorderTop), Math.Max(Working.BorderRight, Working.BorderBottom));
        set
        {
            if (value is { } width)
            {
                var w = ToBorder(width);
                Update(f => f with { BorderLeft = w, BorderTop = w, BorderRight = w, BorderBottom = w }, Strings.Frame_Border);
            }
        }
    }

    public double? BorderLeft
    {
        get => Working.BorderLeft;
        set => SetSide(value, (f, w) => f with { BorderLeft = w });
    }

    public double? BorderTop
    {
        get => Working.BorderTop;
        set => SetSide(value, (f, w) => f with { BorderTop = w });
    }

    public double? BorderRight
    {
        get => Working.BorderRight;
        set => SetSide(value, (f, w) => f with { BorderRight = w });
    }

    public double? BorderBottom
    {
        get => Working.BorderBottom;
        set => SetSide(value, (f, w) => f with { BorderBottom = w });
    }

    /// <summary>Bei abgerissener Kante hat der Rand überall die gleiche Breite.</summary>
    public bool CanBorderPerSide => !Working.HasTorn;

    public bool IsBorderUniform => !(BorderPerSide && CanBorderPerSide);

    public bool IsBorderPerSideVisible => BorderPerSide && CanBorderPerSide;

    /// <summary>Hinweis, dass der Rand bei abgerissener Kante überall gleich breit ist.</summary>
    public bool IsBorderTornNoteVisible => Working.HasBorder && !CanBorderPerSide;

    // ---- Schatten ----

    public bool? IsShadowOn
    {
        get => Frame?.HasShadow ?? false;
        set => SetEffect(FrameEffect.Shadow, value);
    }

    public ArgbColor? ShadowColor
    {
        get => Working.Shadow.Color;
        set
        {
            if (value is { } color)
            {
                Update(f => f with { Shadow = f.Shadow with { Color = color } }, Strings.Section_Shadow);
            }
        }
    }

    public double? ShadowSize
    {
        get => Working.Shadow.Size;
        set
        {
            if (value is { } size)
            {
                Update(f => f with { Shadow = f.Shadow with { Size = Math.Clamp(size, 0, ShadowStyle.MaxSize) } }, Strings.Section_Shadow);
            }
        }
    }

    public double? ShadowBlur
    {
        get => Working.Shadow.Blur;
        set
        {
            if (value is { } blur)
            {
                Update(f => f with { Shadow = f.Shadow with { Blur = Math.Clamp(blur, 0, ShadowStyle.MaxBlur) } }, Strings.Section_Shadow);
            }
        }
    }

    // ---- Abgerundete Ecken ----

    public bool? IsCornersOn
    {
        get => Frame?.HasCorners ?? false;
        set => SetEffect(FrameEffect.Corners, value);
    }

    public double? CornerRadius
    {
        get => Working.CornerRadius;
        set
        {
            if (value is { } radius)
            {
                Update(f => f with { CornerRadius = Math.Clamp(radius, 0, FrameStyle.MaxCornerRadius) }, Strings.Frame_Corners);
            }
        }
    }

    // ---- Abgerissene Kante ----

    public bool? IsTornOn
    {
        get => Frame?.HasTorn ?? false;
        set => SetEffect(FrameEffect.Torn, value);
    }

    public bool TornTop
    {
        get => Working.TornSides.HasFlag(FrameSides.Top);
        set => SetTornSide(FrameSides.Top, value);
    }

    public bool TornBottom
    {
        get => Working.TornSides.HasFlag(FrameSides.Bottom);
        set => SetTornSide(FrameSides.Bottom, value);
    }

    public bool TornLeft
    {
        get => Working.TornSides.HasFlag(FrameSides.Left);
        set => SetTornSide(FrameSides.Left, value);
    }

    public bool TornRight
    {
        get => Working.TornSides.HasFlag(FrameSides.Right);
        set => SetTornSide(FrameSides.Right, value);
    }

    public double? TornHeight
    {
        get => Working.TornHeight;
        set
        {
            if (value is { } height)
            {
                Update(f => f with { TornHeight = Math.Clamp(height, FrameStyle.MinTornHeight, FrameStyle.MaxTornHeight) }, Strings.Frame_TornEdge);
            }
        }
    }

    public double? TornWidth
    {
        get => Working.TornWidth;
        set
        {
            if (value is { } width)
            {
                Update(f => f with { TornWidth = Math.Clamp(width, FrameStyle.MinTornWidth, FrameStyle.MaxTornWidth) }, Strings.Frame_TornEdge);
            }
        }
    }

    // ---- Vignette ----

    public bool? IsVignetteOn
    {
        get => Frame?.HasVignette ?? false;
        set => SetEffect(FrameEffect.Vignette, value);
    }

    /// <summary>Stärke der Abdunklung in Prozent.</summary>
    public double? VignetteStrengthPercent
    {
        get => Math.Round(Working.VignetteStrength * 100);
        set
        {
            if (value is { } percent)
            {
                Update(f => f with { VignetteStrength = Math.Clamp(percent, 0, 100) / 100 }, Strings.Frame_Vignette);
            }
        }
    }

    public double VignetteStrengthValue
    {
        get => VignetteStrengthPercent ?? 0;
        set => VignetteStrengthPercent = Math.Round(value);
    }

    /// <summary>Ausdehnung der Abdunklung nach innen in Prozent.</summary>
    public double? VignetteSizePercent
    {
        get => Math.Round(Working.VignetteSize * 100);
        set
        {
            if (value is { } percent)
            {
                Update(f => f with { VignetteSize = Math.Clamp(percent, FrameStyle.MinVignetteSize * 100, 100) / 100 }, Strings.Frame_Vignette);
            }
        }
    }

    public double VignetteSizeValue
    {
        get => VignetteSizePercent ?? 60;
        set => VignetteSizePercent = Math.Round(value);
    }

    // ---- Graustufen ----

    public bool? IsGrayscale
    {
        get => Working.Grayscale;
        set
        {
            if (value is { } on)
            {
                Update(f => f with { Grayscale = on }, Strings.Frame_Grayscale);
            }
        }
    }

    // ---- Hintergrund ----

    /// <summary>Wählbare Hintergrundarten (die Auswahl „Bild“ öffnet die Dateiauswahl).</summary>
    public IReadOnlyList<BackgroundKindChoice> BackgroundKinds { get; } =
    [
        new(FrameBackgroundKind.Solid, Strings.Frame_Background_Solid),
        new(FrameBackgroundKind.Gradient, Strings.Frame_Background_Gradient),
        new(FrameBackgroundKind.Image, Strings.Frame_Background_Image),
    ];

    public bool? IsBackgroundOn
    {
        get => Frame?.HasBackground ?? false;
        set => SetEffect(FrameEffect.Background, value);
    }

    private FrameBackground CurrentBackground => Working.Background ?? new FrameBackground { Kind = FrameBackgroundKind.Solid };

    /// <summary>Art des Hintergrunds; Wechsel auf „Bild“ fragt eine Datei ab (Abbruch ändert nichts).</summary>
    public FrameBackgroundKind BackgroundKind
    {
        get => CurrentBackground.Kind == FrameBackgroundKind.None ? FrameBackgroundKind.Solid : CurrentBackground.Kind;
        set
        {
            if (value == BackgroundKind && Frame?.HasBackground == true)
            {
                return;
            }

            if (value == FrameBackgroundKind.Image)
            {
                ChooseBackgroundImage();
                Refresh();
                return;
            }

            Update(f => f with { Background = (f.Background ?? CurrentBackground) with { Kind = value }, Padding = f.HasPadding ? f.Padding : DefaultPadding() }, Strings.Frame_Background);
        }
    }

    public bool IsBackgroundSolid => BackgroundKind == FrameBackgroundKind.Solid;

    public bool IsBackgroundGradient => BackgroundKind == FrameBackgroundKind.Gradient;

    public bool IsBackgroundImage => BackgroundKind == FrameBackgroundKind.Image;

    public ArgbColor? BackgroundColorA
    {
        get => CurrentBackground.ColorA;
        set
        {
            if (value is { } color)
            {
                Update(f => f with { Background = (f.Background ?? CurrentBackground) with { ColorA = color } }, Strings.Frame_Background);
            }
        }
    }

    public ArgbColor? BackgroundColorB
    {
        get => CurrentBackground.ColorB;
        set
        {
            if (value is { } color)
            {
                Update(f => f with { Background = (f.Background ?? CurrentBackground) with { ColorB = color } }, Strings.Frame_Background);
            }
        }
    }

    public double? BackgroundAngle
    {
        get => Math.Round(CurrentBackground.AngleDegrees, 1);
        set
        {
            if (value is { } angle)
            {
                Update(f => f with { Background = (f.Background ?? CurrentBackground) with { AngleDegrees = Math.Clamp(angle, -360, 360) } }, Strings.Frame_Background);
            }
        }
    }

    public double BackgroundAngleValue
    {
        get => BackgroundAngle ?? FrameBackground.DefaultAngle;
        set => BackgroundAngle = Math.Round(value);
    }

    /// <summary>Kleine Vorschau des Hintergrundbilds; <c>null</c> ohne Bild.</summary>
    public ImageSource? BackgroundImagePreview => Frame?.Background?.Image is { } asset ? ImageAssetCache.Shared.Get(asset, 96, 96) : null;

    public bool HasBackgroundImage => Frame?.Background?.Image is not null;

    public bool HasNoBackgroundImage => !HasBackgroundImage;

    /// <summary>Einfarbig oder Verlauf (Farbfelder sichtbar).</summary>
    public bool IsBackgroundColorKind => !IsBackgroundImage;

    // ---- Innenabstand und Seitenverhältnis ----

    public IReadOnlyList<AspectChoice> AspectChoices { get; } =
    [
        new(FrameAspectKind.Free, Strings.Frame_Aspect_Free),
        new(FrameAspectKind.Wide16x9, Strings.Frame_Aspect_16x9),
        new(FrameAspectKind.Standard4x3, Strings.Frame_Aspect_4x3),
        new(FrameAspectKind.Square, Strings.Frame_Aspect_Square),
        new(FrameAspectKind.Tall9x16, Strings.Frame_Aspect_9x16),
        new(FrameAspectKind.Custom, Strings.Frame_Aspect_Custom),
    ];

    public double? Padding
    {
        get => Working.Padding;
        set
        {
            if (value is { } padding)
            {
                Update(f => f with { Padding = (int)Math.Clamp(Math.Round(padding), 0, FrameStyle.MaxPadding) }, Strings.Frame_Padding);
            }
        }
    }

    public FrameAspectKind AspectKind
    {
        get => Working.AspectRatio?.Kind ?? FrameAspectKind.Free;
        set
        {
            if (value == AspectKind)
            {
                return;
            }

            // Wechsel in „Benutzerdefiniert“ behält das bisherige Verhältnis als Ausgangswert.
            Update(
                f =>
                {
                    var current = f.AspectRatio ?? FrameAspect.Free;
                    var ratio = current.Ratio ?? (current.Width, current.Height);
                    return f with { AspectRatio = current with { Kind = value, Width = ratio.Item1, Height = ratio.Item2 } };
                },
                Strings.Frame_Aspect);
        }
    }

    public bool IsAspectCustom => AspectKind == FrameAspectKind.Custom;

    public double? AspectWidth
    {
        get => Working.AspectRatio?.Width ?? 16;
        set => SetAspectPart(value, (a, v) => a with { Width = v });
    }

    public double? AspectHeight
    {
        get => Working.AspectRatio?.Height ?? 9;
        set => SetAspectPart(value, (a, v) => a with { Height = v });
    }

    /// <summary>Das gewählte Seitenverhältnis wird nicht angewendet, weil das Ergebnis zu groß würde.</summary>
    public bool IsAspectLimited => Frame is { HasAspect: true } frame && FrameLayout.Compute(_context.Document.EffectiveBounds, frame).AspectLimited;

    /// <summary>Kurzfassung der eingeklappten Karte, z. B. „32 px · 16:9“.</summary>
    public string LayoutSummary
    {
        get
        {
            var padding = string.Format(CultureInfo.CurrentUICulture, "{0} px", Working.Padding);
            var ratio = Working.AspectRatio?.Ratio;
            return ratio is { } r ? $"{padding} · {r.Width}:{r.Height}" : padding;
        }
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Undo.Changed -= OnStateChanged;
        _context.Frame.Changed -= OnStateChanged;
    }

    /// <summary>Entfernt Rahmen und alle Effekte in einem Undo-Schritt.</summary>
    [RelayCommand]
    private void RemoveFrame()
    {
        if (_context.Frame.Set(null, Strings.Frame_Remove, FrameToolState.RemoveKey))
        {
            Refresh();
        }
    }

    /// <summary>Fragt eine Bilddatei ab und setzt sie als Hintergrundbild (Abbruch ändert nichts).</summary>
    [RelayCommand]
    private void ChooseBackgroundImage()
    {
        if (_pickBackground() is not { } asset)
        {
            return;
        }

        Update(
            f => f with
            {
                Background = (f.Background ?? CurrentBackground) with { Kind = FrameBackgroundKind.Image, Image = asset },
                Padding = f.HasPadding ? f.Padding : DefaultPadding(),
            },
            Strings.Frame_Background);
    }

    /// <summary>Würfelt das Muster der abgerissenen Kante neu.</summary>
    [RelayCommand]
    private void RerollTorn()
    {
        var seed = _random.Next(1, int.MaxValue);
        Update(f => f with { TornSeed = seed == f.TornSeed ? seed + 1 : seed }, Strings.Frame_TornEdge);
    }

    partial void OnBorderPerSideChanged(bool value) => OnPropertyChanged(nameof(IsBorderPerSideVisible));

    private int DefaultPadding() => _context.Frame.Defaults.HasPadding ? _context.Frame.Defaults.Padding : FrameToolState.DefaultPadding;

    private void SetAspectPart(double? value, Func<FrameAspect, int, FrameAspect> change)
    {
        if (value is { } part)
        {
            var clamped = (int)Math.Clamp(Math.Round(part), 1, FrameAspect.MaxPart);
            Update(f => f with { AspectRatio = change(f.AspectRatio ?? new FrameAspect { Kind = FrameAspectKind.Custom }, clamped) }, Strings.Frame_Aspect);
        }
    }

    private static int ToBorder(double width) => (int)Math.Clamp(Math.Round(width), 0, FrameStyle.MaxBorder);

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private void SetEffect(FrameEffect effect, bool? on)
    {
        if (on is not { } value)
        {
            return;
        }

        var current = Working;
        var updated = value
            ? FrameToolState.TurnOn(current, _context.Frame.Defaults, effect)
            : FrameToolState.TurnOff(current, effect);
        Commit(updated, EffectName(effect));
    }

    private void SetSide(double? value, Func<FrameStyle, int, FrameStyle> change)
    {
        if (value is { } width)
        {
            var w = ToBorder(width);
            Update(f => change(f, w), Strings.Frame_Border);
        }
    }

    private void SetTornSide(FrameSides side, bool on) => Update(
        f =>
        {
            var sides = on ? f.TornSides | side : f.TornSides & ~side;
            var updated = f with { TornSides = sides };

            // Die erste Seite schaltet den Effekt ein: Zackenmaß und Muster stammen aus den gemerkten Werten.
            return sides != FrameSides.None && !f.HasTorn
                ? updated with { TornHeight = _context.Frame.Defaults.TornHeight, TornWidth = _context.Frame.Defaults.TornWidth, TornSeed = _context.Frame.Defaults.TornSeed }
                : updated;
        },
        Strings.Frame_TornEdge);

    private void Update(Func<FrameStyle, FrameStyle> change, string description) => Commit(change(Working), description);

    private void Commit(FrameStyle updated, string description)
    {
        _context.Frame.Set(updated, description);

        // Auch bei unveränderten oder abgelehnten Werten: Anzeige auf den Modellstand zurücksetzen.
        Refresh();
    }

    private static string EffectName(FrameEffect effect) => effect switch
    {
        FrameEffect.Border => Strings.Frame_Border,
        FrameEffect.Shadow => Strings.Section_Shadow,
        FrameEffect.Corners => Strings.Frame_Corners,
        FrameEffect.Torn => Strings.Frame_TornEdge,
        FrameEffect.Background => Strings.Frame_Background,
        _ => Strings.Frame_Vignette,
    };
}

/// <summary>Eintrag der Auswahl der Hintergrundart.</summary>
public sealed record BackgroundKindChoice(FrameBackgroundKind Value, string Label);

/// <summary>Eintrag der Auswahl des Seitenverhältnisses.</summary>
public sealed record AspectChoice(FrameAspectKind Value, string Label);
