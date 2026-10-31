using System.Windows;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Capture;

/// <summary>Oberfläche der Scroll-Aufnahme (Steuerleiste und Rahmen); für Tests durch eine Attrappe ersetzbar.</summary>
public interface IScrollCaptureView : IDisposable
{
    event Action? ModeChanged;

    event Action? FinishRequested;

    event Action? CancelRequested;

    /// <summary>Wunsch „Zuerst zum Anfang scrollen“ (aus den Einstellungen vorbelegt).</summary>
    bool ToStartRequested { get; }

    /// <summary>Der Benutzer scrollt selbst.</summary>
    bool ManualMode { get; }

    /// <summary>Zeigt Leiste und Rahmen; <c>false</c>, wenn sie sich nicht von der Aufnahme ausschließen lassen (dann wird nichts angezeigt).</summary>
    bool TryShow(PixelRect region, MonitorInfo monitor);

    void SetManual(bool manual);

    void SetStatus(string text);

    void SetProgress(int height, int maxHeight);

    void SetPreview(PixelBuffer? preview);

    /// <summary>Sperrt „Zuerst zum Anfang scrollen“, sobald die Aufnahme läuft.</summary>
    void LockStartOption();
}

public interface IScrollCaptureViewFactory
{
    IScrollCaptureView Create();
}

public sealed class ScrollCaptureViewFactory : IScrollCaptureViewFactory
{
    private readonly IWindowFactory _windows;
    private readonly IWindowExclusionService _exclusion;
    private readonly ISettingsService _settings;

    public ScrollCaptureViewFactory(IWindowFactory windows, IWindowExclusionService exclusion, ISettingsService settings)
    {
        _windows = windows;
        _exclusion = exclusion;
        _settings = settings;
    }

    public IScrollCaptureView Create() =>
        new ScrollCaptureView(_windows.Create<ScrollCaptureBar>(), _windows.Create<ScrollCaptureFrame>(), _exclusion, _settings);
}

/// <summary>Verbindet <see cref="ScrollCaptureBar"/> und <see cref="ScrollCaptureFrame"/> zu einer Ansicht.</summary>
public sealed class ScrollCaptureView : IScrollCaptureView
{
    private readonly ScrollCaptureBar _bar;
    private readonly ScrollCaptureFrame _frame;
    private readonly IWindowExclusionService _exclusion;
    private readonly ISettingsService _settings;

    public ScrollCaptureView(ScrollCaptureBar bar, ScrollCaptureFrame frame, IWindowExclusionService exclusion, ISettingsService settings)
    {
        _bar = bar;
        _frame = frame;
        _exclusion = exclusion;
        _settings = settings;
        _bar.SetToStart(settings.Current.ScrollCaptureToStart);
        _bar.ToStartChanged += OnToStartChanged;
    }

    public event Action? ModeChanged
    {
        add => _bar.ModeChanged += value;
        remove => _bar.ModeChanged -= value;
    }

    public event Action? FinishRequested
    {
        add => _bar.FinishRequested += value;
        remove => _bar.FinishRequested -= value;
    }

    public event Action? CancelRequested
    {
        add => _bar.CancelRequested += value;
        remove => _bar.CancelRequested -= value;
    }

    public bool ToStartRequested => _bar.ToStartRequested;

    public bool ManualMode => _bar.ManualMode;

    public bool TryShow(PixelRect region, MonitorInfo monitor)
    {
        _bar.Place(region, monitor);
        _frame.Place(region, monitor.Scale);
        if (!_exclusion.RegisterAlwaysExcluded(_bar) || !_exclusion.RegisterAlwaysExcluded(_frame))
        {
            return false;
        }

        _frame.Show();
        _bar.Show();
        return true;
    }

    public void SetManual(bool manual) => _bar.SetManual(manual);

    public void SetStatus(string text) => _bar.SetStatus(text);

    public void SetProgress(int height, int maxHeight) => _bar.SetProgress(height, maxHeight);

    public void SetPreview(PixelBuffer? preview) => _bar.SetPreview(preview);

    public void LockStartOption() => _bar.LockStartOption();

    public void Dispose()
    {
        _bar.ToStartChanged -= OnToStartChanged;
        _bar.Close();
        _frame.Close();
    }

    private void OnToStartChanged(bool value)
    {
        _settings.Current = _settings.Current with { ScrollCaptureToStart = value };
        _ = _settings.SaveAsync();
    }
}
