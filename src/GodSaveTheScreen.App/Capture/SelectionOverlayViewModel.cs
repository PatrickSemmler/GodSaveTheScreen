using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Steuert die Auswahl über alle Overlays; liefert das Ergebnis (<c>null</c> = abgebrochen).</summary>
public sealed class SelectionOverlayViewModel
{
    private bool _finished;

    public SelectionOverlayViewModel(SelectionState state) => State = state;

    /// <summary>Wird genau einmal ausgelöst: gewählter Bereich in virtuellen Pixeln oder <c>null</c> bei Abbruch.</summary>
    public event Action<PixelRect?>? Finished;

    public SelectionState State { get; }

    /// <summary>Größenanzeige, z. B. "320 × 200".</summary>
    public string SizeText => $"{State.Selection.Width} × {State.Selection.Height}";

    public void PointerDown(PointD virtualPoint) => State.Begin(virtualPoint);

    public void PointerMove(PointD virtualPoint) => State.MoveCursor(virtualPoint);

    /// <summary>Loslassen übernimmt die Auswahl, wenn sie die Mindestgröße hat; sonst wird sie verworfen.</summary>
    public void PointerUp()
    {
        if (!State.IsSelecting)
        {
            return;
        }

        State.End();
        if (State.IsValid)
        {
            Confirm();
        }
        else
        {
            State.Reset();
        }
    }

    public void Nudge(int dx, int dy) => State.NudgeEnd(dx, dy);

    public void Confirm()
    {
        if (State.IsValid)
        {
            Finish(State.Selection);
        }
    }

    public void Cancel() => Finish(null);

    private void Finish(PixelRect? result)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Finished?.Invoke(result);
    }
}
