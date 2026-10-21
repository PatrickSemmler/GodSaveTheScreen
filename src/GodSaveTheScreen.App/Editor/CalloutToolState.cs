using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Letzte Konfiguration des Sprechblasen-Werkzeugs (<see cref="AppSettings.CalloutDefaults"/>); jede Änderung wird gespeichert.</summary>
public sealed class CalloutToolState
{
    private readonly ISettingsService _settings;

    public CalloutToolState(ISettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    public CalloutStyle Style
    {
        get => _settings.Current.CalloutDefaults ?? new CalloutStyle();
        set
        {
            if (value == Style)
            {
                return;
            }

            _settings.Current = _settings.Current with { CalloutDefaults = value };
            _ = _settings.SaveAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Körpergröße bei einem Klick ohne Aufziehen (abhängig von der Schriftgröße).</summary>
    public static SizeD DefaultBodySize(double fontSize) =>
        new(Math.Max(CalloutAnnotation.MinBodySize, fontSize * 9), Math.Max(CalloutAnnotation.MinBodySize, fontSize * 3.5));

    /// <summary>Spitze unter dem Körper, im linken Viertel – zum Verschieben am Griff.</summary>
    public static PointD DefaultTail(RectD body) =>
        new(body.X + (body.Width * 0.25), body.Bottom + Math.Max(30, body.Height * 0.6));

    /// <summary>Neue Sprechblase mit den Standardwerten über <paramref name="body"/>.</summary>
    public CalloutAnnotation Create(RectD body)
    {
        var callout = new CalloutAnnotation { Rect = body.Normalize(), Tail = DefaultTail(body.Normalize()) };
        StyleApplier.Apply(callout, Style.Normalized());
        return callout;
    }
}
