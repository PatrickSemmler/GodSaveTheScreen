namespace GodSaveTheScreen.Core.Capture;

public enum CaptureMode
{
    Rectangle,
    Window,
    FullScreen,
    Freeform,

    /// <summary>Text aus einem Bereich erkennen (Ergebnisfenster statt Editor); steht am Ende, damit gespeicherte Werte gültig bleiben.</summary>
    Text,
}
