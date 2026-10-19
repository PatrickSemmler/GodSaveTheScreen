using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Gemeinsamer Auswahlzustand aller Overlays in virtuellen Pixelkoordinaten.
/// Unabhängig von WPF-Fenstern testbar.
/// </summary>
public sealed class SelectionState
{
    private PointD _anchor;
    private PointD _end;

    public SelectionState(PixelRect desktopBounds, int minimumSize = 3)
    {
        DesktopBounds = desktopBounds;
        MinimumSize = minimumSize;
        Cursor = new PointD(desktopBounds.X, desktopBounds.Y);
    }

    public event EventHandler? Changed;

    public PixelRect DesktopBounds { get; }

    public int MinimumSize { get; }

    /// <summary>Maus ist gedrückt, die Auswahl wird gerade aufgezogen.</summary>
    public bool IsSelecting { get; private set; }

    /// <summary>Es existiert ein Startpunkt (Auswahl wird aufgezogen oder ist abgeschlossen).</summary>
    public bool HasSelection { get; private set; }

    /// <summary>Mausposition in virtuellen Pixeln (an den Desktop geklemmt).</summary>
    public PointD Cursor { get; private set; }

    /// <summary>Normalisierte, an den Desktop geklemmte Auswahl (Endkante exklusiv).</summary>
    public PixelRect Selection
    {
        get
        {
            if (!HasSelection)
            {
                return PixelRect.Empty;
            }

            var left = (int)Math.Min(_anchor.X, _end.X);
            var top = (int)Math.Min(_anchor.Y, _end.Y);
            var right = (int)Math.Max(_anchor.X, _end.X);
            var bottom = (int)Math.Max(_anchor.Y, _end.Y);
            return PixelRect.FromLtrb(left, top, right, bottom);
        }
    }

    public bool IsValid => Selection.Width >= MinimumSize && Selection.Height >= MinimumSize;

    public void Begin(PointD point)
    {
        var p = ClampSelectionPoint(point);
        _anchor = p;
        _end = p;
        Cursor = ClampCursor(point);
        HasSelection = true;
        IsSelecting = true;
        OnChanged();
    }

    public void MoveCursor(PointD point)
    {
        Cursor = ClampCursor(point);
        if (IsSelecting)
        {
            _end = ClampSelectionPoint(point);
        }

        OnChanged();
    }

    /// <summary>Verschiebt den Endpunkt pixelgenau (Pfeiltasten).</summary>
    public void NudgeEnd(int dx, int dy)
    {
        if (!HasSelection)
        {
            return;
        }

        _end = ClampSelectionPoint(new PointD(_end.X + dx, _end.Y + dy));
        Cursor = ClampCursor(_end);
        OnChanged();
    }

    public void End()
    {
        if (IsSelecting)
        {
            IsSelecting = false;
            OnChanged();
        }
    }

    public void Reset()
    {
        HasSelection = false;
        IsSelecting = false;
        _anchor = default;
        _end = default;
        OnChanged();
    }

    private static PointD Floor(PointD p) => new(Math.Floor(p.X), Math.Floor(p.Y));

    // Auswahlkanten dürfen auf der rechten/unteren Desktopkante liegen (exklusives Ende).
    private PointD ClampSelectionPoint(PointD p)
    {
        var f = Floor(p);
        return new PointD(
            Math.Clamp(f.X, DesktopBounds.X, DesktopBounds.Right),
            Math.Clamp(f.Y, DesktopBounds.Y, DesktopBounds.Bottom));
    }

    private PointD ClampCursor(PointD p)
    {
        var f = Floor(p);
        return new PointD(
            Math.Clamp(f.X, DesktopBounds.X, DesktopBounds.Right - 1),
            Math.Clamp(f.Y, DesktopBounds.Y, DesktopBounds.Bottom - 1));
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
