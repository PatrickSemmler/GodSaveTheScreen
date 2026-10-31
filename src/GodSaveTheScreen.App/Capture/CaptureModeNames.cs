using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Capture;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Lokalisierte Kurznamen der Aufnahmemodi (z. B. für Dateinamen und Anzeige).</summary>
public static class CaptureModeNames
{
    public static string Get(CaptureMode mode) => mode switch
    {
        CaptureMode.Rectangle => Strings.Mode_Rectangle,
        CaptureMode.Window => Strings.Mode_Window,
        CaptureMode.Freeform => Strings.Mode_Freeform,
        CaptureMode.Text => Strings.Mode_Text,
        _ => Strings.Mode_Fullscreen,
    };
}
