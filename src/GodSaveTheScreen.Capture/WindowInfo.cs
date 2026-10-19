using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Capture;

/// <summary>Sichtbares Top-Level-Fenster. <see cref="Bounds"/> in virtuellen Pixeln, ohne unsichtbare Resize-Ränder.</summary>
public sealed record WindowInfo(
    nint Handle,
    string Title,
    string ProcessName,
    PixelRect Bounds,
    bool IsMaximized,
    int ZIndex);

public interface IWindowEnumerator
{
    /// <summary>Sichtbare Top-Level-Fenster von oben nach unten (Z-Order).</summary>
    IReadOnlyList<WindowInfo> GetTopLevelWindows();
}

public interface IWindowContentCapture
{
    /// <summary>
    /// Rendert das Fenster per <c>PrintWindow</c> (auch verdeckte Teile). Liefert <c>null</c>, wenn das Ergebnis
    /// leer oder schwarz ist. Das Bild entspricht <see cref="WindowInfo.Bounds"/>.
    /// </summary>
    Core.Imaging.PixelBuffer? TryCapture(WindowInfo window);
}

/// <summary>Treffertest gegen eine gespeicherte Fensterliste (nicht live), damit Overlay-Fenster nie getroffen werden.</summary>
public static class WindowHitTest
{
    /// <summary>Oberstes Fenster (kleinster Z-Index) an der virtuellen Position oder <c>null</c>.</summary>
    public static WindowInfo? Find(IReadOnlyList<WindowInfo> windows, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(windows);
        WindowInfo? best = null;
        foreach (var window in windows)
        {
            if (window.Bounds.Contains(x, y) && (best is null || window.ZIndex < best.ZIndex))
            {
                best = window;
            }
        }

        return best;
    }
}
