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
}
