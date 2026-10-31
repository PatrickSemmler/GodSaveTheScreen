namespace GodSaveTheScreen.Capture;

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>Monitor unter dem Mauszeiger (bzw. nächstgelegener).</summary>
    MonitorInfo GetMonitorAtCursor();
}

public interface IScreenCaptureService
{
    /// <summary>Friert den gesamten virtuellen Desktop ein.</summary>
    ScreenSnapshot CaptureVirtualScreen(bool includeCursor = false);

    /// <summary>Nimmt nur ein Rechteck des virtuellen Desktops auf (physische Pixel); ohne Monitor- und Fensterliste, daher schnell genug für Aufnahmeschleifen.</summary>
    Core.Imaging.PixelBuffer CaptureRegion(Core.Geometry.PixelRect region, bool includeCursor = false);
}
