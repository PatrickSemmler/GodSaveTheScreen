using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Letzte Konfiguration des Stempelwerkzeugs (<see cref="AppSettings.StampDefaults"/>); jede Änderung wird gespeichert.</summary>
public sealed class StampToolState
{
    private readonly ISettingsService _settings;

    public StampToolState(ISettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    public StampStyle Style
    {
        get => _settings.Current.StampDefaults ?? new StampStyle();
        set
        {
            if (value == Style)
            {
                return;
            }

            _settings.Current = _settings.Current with { StampDefaults = value };
            _ = _settings.SaveAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Text des nächsten Stempels (zuletzt gestempelter Text bzw. Schnellauswahl).</summary>
    public string Text
    {
        get => string.IsNullOrWhiteSpace(Style.Text) ? Strings.Stamp_Text_Approved : Style.Text;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Style = Style with { Text = value };
            }
        }
    }

    /// <summary>Neuer Stempel mit den Standardwerten und einem neuen Abnutzungsmuster, mittig auf <paramref name="center"/>.</summary>
    public StampAnnotation Create(PointD center)
    {
        var style = Style;
        return new StampAnnotation
        {
            Text = Text,
            Center = center,
            FontFamilyName = style.FontFamilyName,
            FontWeight = style.FontWeight,
            FontStyle = style.FontStyle,
            FontStretch = style.FontStretch,
            Decorations = style.Decorations,
            FontSize = Math.Clamp(style.FontSize, StampAnnotation.MinFontSize, StampAnnotation.MaxFontSize),
            Color = style.Color,
            FrameShape = style.FrameShape,
            FrameThickness = Math.Clamp(style.FrameThickness, 0, StampAnnotation.MaxFrameThickness),
            Wear = Math.Clamp(style.Wear, 0, StampAnnotation.MaxWear),
            Rotation = style.Rotation,
            InkBlend = style.InkBlend,
            Opacity = Math.Clamp(style.Opacity, 0, 1),
            Seed = NewSeed(),
        };
    }

    /// <summary>Startwert für ein neues Abnutzungsmuster.</summary>
    public static int NewSeed() => Random.Shared.Next(1, int.MaxValue);
}
