using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Werkzeug-Standardwerte und zuletzt verwendete Farben (in den Einstellungen gespeichert).</summary>
public interface IToolDefaultsService
{
    int MaxRecentColors { get; }

    ShapeStyle GetShape(string toolId, ShapeStyle fallback);

    void SetShape(string toolId, ShapeStyle style);

    TextStyle GetText();

    void SetText(TextStyle style);

    MarkerStyle GetMarker();

    void SetMarker(MarkerStyle style);

    BlurStyle GetBlur();

    void SetBlur(BlurStyle style);

    MagnifierStyle GetMagnifier();

    void SetMagnifier(MagnifierStyle style);

    FreehandOptions GetFreehand();

    void SetFreehand(FreehandOptions options);

    int MarkerStartNumber { get; set; }

    /// <summary>Zuletzt verwendete Schriften (Familienname), neueste zuerst.</summary>
    IReadOnlyList<string> RecentFonts { get; }

    void AddRecentFont(string familyName);

    IReadOnlyList<ArgbColor> RecentColors { get; }

    void AddRecentColor(ArgbColor color);

    event EventHandler? RecentColorsChanged;
}

public sealed class ToolDefaultsService : IToolDefaultsService
{
    private readonly ISettingsService _settings;

    public ToolDefaultsService(ISettingsService settings) => _settings = settings;

    public event EventHandler? RecentColorsChanged;

    private const int MaxRecentFonts = 6;

    public int MaxRecentColors => 10;

    public IReadOnlyList<ArgbColor> RecentColors =>
        [.. _settings.Current.RecentColors.Select(c => ArgbColor.TryParse(c, out var color) ? color : (ArgbColor?)null).Where(c => c is not null).Select(c => c!.Value)];

    public ShapeStyle GetShape(string toolId, ShapeStyle fallback) =>
        _settings.Current.ShapeDefaults.TryGetValue(toolId, out var style) ? style : fallback;

    public void SetShape(string toolId, ShapeStyle style)
    {
        var defaults = new ShapeDefaultsMap(_settings.Current.ShapeDefaults) { [toolId] = style };
        _settings.Current = _settings.Current with { ShapeDefaults = defaults };
        _ = _settings.SaveAsync();
    }

    public TextStyle GetText() => _settings.Current.TextDefaults;

    public void SetText(TextStyle style)
    {
        _settings.Current = _settings.Current with { TextDefaults = style };
        _ = _settings.SaveAsync();
    }

    public MarkerStyle GetMarker() => _settings.Current.MarkerDefaults;

    public void SetMarker(MarkerStyle style)
    {
        _settings.Current = _settings.Current with { MarkerDefaults = style };
        _ = _settings.SaveAsync();
    }

    public BlurStyle GetBlur() => _settings.Current.BlurDefaults;

    public void SetBlur(BlurStyle style)
    {
        if (style == _settings.Current.BlurDefaults)
        {
            return;
        }

        _settings.Current = _settings.Current with { BlurDefaults = style };
        _ = _settings.SaveAsync();
    }

    public MagnifierStyle GetMagnifier() => _settings.Current.MagnifierDefaults;

    public void SetMagnifier(MagnifierStyle style)
    {
        if (style == _settings.Current.MagnifierDefaults)
        {
            return;
        }

        _settings.Current = _settings.Current with { MagnifierDefaults = style };
        _ = _settings.SaveAsync();
    }

    public FreehandOptions GetFreehand() => new(_settings.Current.FreehandDefaults.Smoothing, _settings.Current.FreehandDefaults.UsePressure);

    public void SetFreehand(FreehandOptions options)
    {
        _settings.Current = _settings.Current with { FreehandDefaults = new FreehandStyle { Smoothing = options.Smoothing, UsePressure = options.UsePressure } };
        _ = _settings.SaveAsync();
    }

    public int MarkerStartNumber
    {
        get => _settings.Current.MarkerStartNumber;
        set
        {
            _settings.Current = _settings.Current with { MarkerStartNumber = Math.Clamp(value, 0, 9999) };
            _ = _settings.SaveAsync();
        }
    }

    public IReadOnlyList<string> RecentFonts => _settings.Current.RecentFonts;

    public void AddRecentFont(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return;
        }

        var list = new StringList(_settings.Current.RecentFonts);
        list.RemoveAll(f => string.Equals(f, familyName, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, familyName);
        if (list.Count > MaxRecentFonts)
        {
            list.RemoveRange(MaxRecentFonts, list.Count - MaxRecentFonts);
        }

        _settings.Current = _settings.Current with { RecentFonts = list };
        _ = _settings.SaveAsync();
    }

    public void AddRecentColor(ArgbColor color)
    {
        if (color.IsTransparent)
        {
            return;
        }

        var text = color.ToString();
        var list = new StringList(_settings.Current.RecentColors);
        list.RemoveAll(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, text);
        if (list.Count > MaxRecentColors)
        {
            list.RemoveRange(MaxRecentColors, list.Count - MaxRecentColors);
        }

        _settings.Current = _settings.Current with { RecentColors = list };
        _ = _settings.SaveAsync();
        RecentColorsChanged?.Invoke(this, EventArgs.Empty);
    }
}
