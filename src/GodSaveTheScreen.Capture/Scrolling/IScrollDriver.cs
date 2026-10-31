using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Capture.Scrolling;

/// <summary>Was gescrollt wird: das Fenster und der Bereich darin (virtuelle Desktop-Pixel).</summary>
public sealed record ScrollTarget(nint Window, PixelRect Region)
{
    /// <summary>Mitte des Bereichs, dort greifen Mausrad und Fenstersuche an.</summary>
    public (int X, int Y) Center => (Region.X + (Region.Width / 2), Region.Y + (Region.Height / 2));
}

/// <summary>Eine Möglichkeit, den Inhalt eines fremden Programms senkrecht zu scrollen.</summary>
public interface IScrollDriver : IDisposable
{
    string Name { get; }

    /// <summary>Bewegt der Treiber den Mauszeiger (kurz) beim Scrollen?</summary>
    bool MovesCursor { get; }

    /// <summary>Braucht der Treiber den Eingabefokus im Zielfenster?</summary>
    bool NeedsFocus { get; }

    /// <summary>Ist das Ende erreicht? <c>null</c>, wenn der Treiber das nicht weiß.</summary>
    bool? IsAtEnd { get; }

    /// <summary>Prüft, ob der Treiber für das Ziel grundsätzlich in Frage kommt (ohne zu scrollen).</summary>
    bool TryPrepare(ScrollTarget target);

    /// <summary>Scrollt um <paramref name="units"/> Einheiten (&gt; 0 nach unten, &lt; 0 nach oben); <c>false</c>, wenn der Aufruf nicht möglich war. Wie weit eine Einheit geht, bestimmt das Programm.</summary>
    bool Scroll(int units);

    /// <summary>Scrollt (so weit der Treiber kann) ganz nach oben.</summary>
    bool ScrollToStart();
}
