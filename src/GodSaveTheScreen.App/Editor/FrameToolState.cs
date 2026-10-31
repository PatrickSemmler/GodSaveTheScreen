using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Die einzeln schaltbaren Effekte des Rahmens.</summary>
public enum FrameEffect
{
    Border,
    Shadow,
    Corners,
    Torn,
    Vignette,
}

/// <summary>
/// Rahmen des Dokuments und die zuletzt genutzten Werte je Effekt (<see cref="AppSettings.FrameDefaults"/>). Jede
/// Änderung am Rahmen ist eine Undo-Aktion; die Werte eingeschalteter Effekte werden zugleich gemerkt, damit das
/// Wiedereinschalten (auch in einem anderen Bild) sie wiederherstellt.
/// </summary>
public sealed class FrameToolState
{
    /// <summary>Schlüssel der Undo-Aktionen: Regler und Schalter fassen sich im Zeitfenster zusammen.</summary>
    public const string UndoKey = "Frame";

    /// <summary>Eigene Schlüssel für „Rahmen entfernen“ und Vorlagen: sie sollen sich nicht mit Reglerfolgen zusammenfassen.</summary>
    public const string RemoveKey = "FrameRemove";

    public const string PresetKey = "FramePreset";

    private readonly EditorContext _context;
    private readonly ISettingsService _settings;

    public FrameToolState(EditorContext context, ISettingsService settings)
    {
        _context = context;
        _settings = settings;
    }

    /// <summary>Der Rahmen des Dokuments oder eine Änderung der gemerkten Werte.</summary>
    public event EventHandler? Changed;

    /// <summary>Zuletzt genutzte Werte je Effekt; neutrale Werte bedeuten „noch nie genutzt“ (eingebauter Standard).</summary>
    public FrameStyle Defaults
    {
        get => (_settings.Current.FrameDefaults ?? new FrameStyle()).Clamped();
        set
        {
            var clamped = value.Clamped();
            if (clamped == Defaults)
            {
                return;
            }

            _settings.Current = _settings.Current with { FrameDefaults = clamped };
            _ = _settings.SaveAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public FrameStyle? Current => _context.Document.Frame;

    public static bool IsOn(FrameStyle? frame, FrameEffect effect) => frame is not null && effect switch
    {
        FrameEffect.Border => frame.HasBorder,
        FrameEffect.Shadow => frame.HasShadow,
        FrameEffect.Corners => frame.HasCorners,
        FrameEffect.Torn => frame.HasTorn,
        FrameEffect.Vignette => frame.HasVignette,
        _ => false,
    };

    /// <summary>
    /// Schaltet einen Effekt ein: mit den gemerkten Werten aus <paramref name="remembered"/>, wo diese einen genutzten
    /// Effekt enthalten, sonst mit dem eingebauten Standard.
    /// </summary>
    public static FrameStyle TurnOn(FrameStyle frame, FrameStyle remembered, FrameEffect effect) => effect switch
    {
        FrameEffect.Border => remembered.HasBorder
            ? frame with
            {
                BorderColor = remembered.BorderColor,
                BorderLeft = remembered.BorderLeft,
                BorderTop = remembered.BorderTop,
                BorderRight = remembered.BorderRight,
                BorderBottom = remembered.BorderBottom,
            }
            : frame with { BorderColor = remembered.BorderColor.IsTransparent ? ArgbColor.White : remembered.BorderColor, BorderLeft = 16, BorderTop = 16, BorderRight = 16, BorderBottom = 16 },
        FrameEffect.Shadow => frame with { Shadow = remembered.HasShadow ? remembered.Shadow : new ShadowStyle(new ArgbColor(110, 0, 0, 0), 8, 16) },
        FrameEffect.Corners => frame with { CornerRadius = remembered.HasCorners ? remembered.CornerRadius : 16 },
        FrameEffect.Torn => frame with
        {
            TornSides = remembered.HasTorn ? remembered.TornSides : FrameSides.Bottom,
            TornHeight = remembered.TornHeight,
            TornWidth = remembered.TornWidth,
            TornSeed = remembered.TornSeed,
        },
        _ => frame with
        {
            VignetteStrength = remembered.HasVignette ? remembered.VignetteStrength : 0.5,
            VignetteSize = remembered.VignetteSize,
        },
    };

    /// <summary>Schaltet einen Effekt aus (neutrale Werte).</summary>
    public static FrameStyle TurnOff(FrameStyle frame, FrameEffect effect) => effect switch
    {
        FrameEffect.Border => frame with { BorderLeft = 0, BorderTop = 0, BorderRight = 0, BorderBottom = 0 },
        FrameEffect.Shadow => frame with { Shadow = frame.Shadow with { Color = ArgbColor.Transparent } },
        FrameEffect.Corners => frame with { CornerRadius = 0 },
        FrameEffect.Torn => frame with { TornSides = FrameSides.None },
        _ => frame with { VignetteStrength = 0 },
    };

    /// <summary>Übernimmt die Werte der eingeschalteten Effekte von <paramref name="frame"/> in die gemerkten Werte.</summary>
    public static FrameStyle Remember(FrameStyle remembered, FrameStyle? frame)
    {
        if (frame is null)
        {
            return remembered;
        }

        var result = remembered;
        if (frame.HasBorder)
        {
            result = result with { BorderColor = frame.BorderColor, BorderLeft = frame.BorderLeft, BorderTop = frame.BorderTop, BorderRight = frame.BorderRight, BorderBottom = frame.BorderBottom };
        }

        if (frame.HasShadow)
        {
            result = result with { Shadow = frame.Shadow };
        }

        if (frame.HasCorners)
        {
            result = result with { CornerRadius = frame.CornerRadius };
        }

        if (frame.HasTorn)
        {
            result = result with { TornSides = frame.TornSides, TornHeight = frame.TornHeight, TornWidth = frame.TornWidth, TornSeed = frame.TornSeed };
        }

        if (frame.HasVignette)
        {
            result = result with { VignetteStrength = frame.VignetteStrength, VignetteSize = frame.VignetteSize };
        }

        return result;
    }

    /// <summary>
    /// Setzt den Rahmen des Dokuments als Undo-Schritt (aufeinanderfolgende Änderungen im 1,5-s-Fenster werden zusammengefasst)
    /// und merkt die Werte der eingeschalteten Effekte. Gibt <c>false</c> zurück, wenn sich nichts ändert.
    /// </summary>
    public bool Set(FrameStyle? frame, string description, string key = UndoKey)
    {
        var document = _context.Document;
        var old = document.Frame;
        var normalized = frame?.Normalized();
        if (Equals(old, normalized))
        {
            return false;
        }

        _context.Undo.Execute(new PropertyChangeAction<FrameStyle?>(document, key, old, normalized, value => document.Frame = value, description));
        Defaults = Remember(Defaults, normalized);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

/// <summary>Eingebaute Schnellvorlagen des Rahmen-Werkzeugs (Code, nicht löschbar).</summary>
public static class FramePresets
{
    /// <summary>Eingebaute Vorlagen; die Namen kommen lokalisiert aus den Strings.</summary>
    public static IReadOnlyList<ToolPreset> BuiltIn => Create();

    /// <summary>Der Name gehört zu einer eingebauten Vorlage.</summary>
    public static bool IsBuiltInName(string? name) => BuiltIn.Any(p => PresetNames.AreSame(p.Name, name));

    private static IReadOnlyList<ToolPreset> Create() =>
    [
        new ToolPreset
        {
            Name = Strings.Frame_Preset_Shadow,
            Frame = new FrameStyle { Shadow = new ShadowStyle(new ArgbColor(100, 0, 0, 0), 6, 14) },
        },
        new ToolPreset
        {
            Name = Strings.Frame_Preset_Border,
            Frame = new FrameStyle { BorderColor = ArgbColor.FromRgb(0x33, 0x33, 0x33), BorderLeft = 3, BorderTop = 3, BorderRight = 3, BorderBottom = 3 },
        },
        new ToolPreset
        {
            Name = Strings.Frame_Preset_Polaroid,
            Frame = new FrameStyle
            {
                BorderColor = ArgbColor.White,
                BorderLeft = 24,
                BorderTop = 24,
                BorderRight = 24,
                BorderBottom = 80,
                Shadow = new ShadowStyle(new ArgbColor(110, 0, 0, 0), 6, 14),
            },
        },
        new ToolPreset
        {
            Name = Strings.Frame_Preset_Torn,
            Frame = new FrameStyle
            {
                TornSides = FrameSides.Bottom,
                TornHeight = 10,
                TornWidth = 18,
                Shadow = new ShadowStyle(new ArgbColor(90, 0, 0, 0), 4, 10),
            },
        },
        new ToolPreset
        {
            Name = Strings.Frame_Preset_Vignette,
            Frame = new FrameStyle { VignetteStrength = 0.55, VignetteSize = 0.6 },
        },
    ];
}
