using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;

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
    private readonly Random _random = new();

    /// <summary>Randbreite je Seite getrennt einstellen (nur Panel-Zustand, nicht gespeichert).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBorderPerSide))]
    [NotifyPropertyChangedFor(nameof(IsBorderUniform))]
    private bool _borderPerSide;

    public FramePropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
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

    /// <summary>Würfelt das Muster der abgerissenen Kante neu.</summary>
    [RelayCommand]
    private void RerollTorn()
    {
        var seed = _random.Next(1, int.MaxValue);
        Update(f => f with { TornSeed = seed == f.TornSeed ? seed + 1 : seed }, Strings.Frame_TornEdge);
    }

    partial void OnBorderPerSideChanged(bool value) => OnPropertyChanged(nameof(IsBorderPerSideVisible));

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
        _ => Strings.Frame_Vignette,
    };
}
