using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Ergebnis der Auswahl: Rechteck (immer), optional das gewählte Fenster oder das Freiform-Polygon (virtuelle Pixel).</summary>
public sealed record SelectionResult(PixelRect Bounds, WindowInfo? Window = null, IReadOnlyList<PointD>? Polygon = null);

/// <summary>
/// Steuert die Auswahl über alle Overlays; liefert das Ergebnis (<c>null</c> = abgebrochen).
/// Modi: Rechteck, Fenster (Hover + Klick, Ziehen wechselt zur Rechteckauswahl) und Freiform (Lasso).
/// </summary>
public sealed class SelectionOverlayViewModel
{
    /// <summary>Ab dieser Mausbewegung (Pixel) wird im Fenstermodus eine Rechteckauswahl aufgezogen.</summary>
    public const double WindowDragThreshold = 4;

    private readonly IReadOnlyList<WindowInfo> _windows;
    private PointD? _pendingWindowDown;
    private bool _dragSelecting;
    private bool _freeformActive;
    private bool _finished;

    public SelectionOverlayViewModel(SelectionState state, CaptureMode mode = CaptureMode.Rectangle, IReadOnlyList<WindowInfo>? windows = null)
    {
        State = state;
        Mode = mode;
        _windows = windows ?? [];
        Path = new FreeformPath(state.DesktopBounds);
        state.Changed += (_, _) => Changed?.Invoke();
    }

    /// <summary>Wird genau einmal ausgelöst: Ergebnis oder <c>null</c> bei Abbruch.</summary>
    public event Action<SelectionResult?>? Finished;

    /// <summary>Darstellung muss aktualisiert werden (Auswahl, Hover oder Pfad geändert).</summary>
    public event Action? Changed;

    public SelectionState State { get; }

    public CaptureMode Mode { get; }

    public FreeformPath Path { get; }

    /// <summary>Fenster unter dem Mauszeiger (nur im Fenstermodus ohne Ziehen).</summary>
    public WindowInfo? HoveredWindow { get; private set; }

    /// <summary>Es wird gerade eine Rechteckauswahl aufgezogen (Rechteckmodus oder Ziehen im Fenstermodus).</summary>
    public bool IsRectangleSelection => Mode == CaptureMode.Rectangle || _dragSelecting;

    public bool IsFreeform => Mode == CaptureMode.Freeform;

    /// <summary>Bereich, der im Overlay ungedimmt dargestellt wird.</summary>
    public PixelRect HighlightBounds
    {
        get
        {
            if (IsFreeform)
            {
                return Path.HasPath ? Path.Bounds : PixelRect.Empty;
            }

            if (IsRectangleSelection)
            {
                return State.HasSelection ? State.Selection : PixelRect.Empty;
            }

            return HoveredWindow?.Bounds.Intersect(State.DesktopBounds) ?? PixelRect.Empty;
        }
    }

    /// <summary>Größenanzeige, z. B. "320 × 200".</summary>
    public string SizeText
    {
        get
        {
            var bounds = HighlightBounds;
            return $"{bounds.Width} × {bounds.Height}";
        }
    }

    public void PointerDown(PointD virtualPoint)
    {
        switch (Mode)
        {
            case CaptureMode.Freeform:
                _freeformActive = true;
                Path.Begin(virtualPoint);
                State.MoveCursor(virtualPoint);
                Changed?.Invoke();
                break;
            case CaptureMode.Window:
                _pendingWindowDown = virtualPoint;
                break;
            default:
                State.Begin(virtualPoint);
                break;
        }
    }

    public void PointerMove(PointD virtualPoint)
    {
        if (Mode == CaptureMode.Window && !_dragSelecting)
        {
            if (_pendingWindowDown is { } down && Distance(down, virtualPoint) >= WindowDragThreshold)
            {
                _dragSelecting = true;
                HoveredWindow = null;
                State.Begin(down);
            }
            else
            {
                State.MoveCursor(virtualPoint);
                HoveredWindow = WindowHitTest.Find(_windows, (int)State.Cursor.X, (int)State.Cursor.Y);
                Changed?.Invoke();
                return;
            }
        }

        State.MoveCursor(virtualPoint);
        if (_freeformActive)
        {
            Path.Add(virtualPoint);
            Changed?.Invoke();
        }
    }

    /// <summary>Loslassen übernimmt die Auswahl, wenn sie gültig ist; sonst wird sie verworfen.</summary>
    public void PointerUp()
    {
        if (_freeformActive)
        {
            _freeformActive = false;
            if (Path.IsValid)
            {
                Finish(new SelectionResult(Path.Bounds, Polygon: Path.Points.ToList()));
            }
            else
            {
                Path.Clear();
                Changed?.Invoke();
            }

            return;
        }

        if (Mode == CaptureMode.Window && !_dragSelecting)
        {
            _pendingWindowDown = null;
            ConfirmWindow();
            return;
        }

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
            _dragSelecting = false;
            _pendingWindowDown = null;
        }
    }

    public void Nudge(int dx, int dy)
    {
        if (IsRectangleSelection)
        {
            State.NudgeEnd(dx, dy);
        }
    }

    public void Confirm()
    {
        if (IsFreeform)
        {
            return;
        }

        if (IsRectangleSelection)
        {
            if (State.IsValid)
            {
                Finish(new SelectionResult(State.Selection));
            }

            return;
        }

        ConfirmWindow();
    }

    public void Cancel() => Finish(null);

    private void ConfirmWindow()
    {
        if (HoveredWindow is { } window && window.Bounds.Intersect(State.DesktopBounds) is { IsEmpty: false } bounds)
        {
            Finish(new SelectionResult(bounds, window));
        }
    }

    private static double Distance(PointD a, PointD b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void Finish(SelectionResult? result)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Finished?.Invoke(result);
    }
}
