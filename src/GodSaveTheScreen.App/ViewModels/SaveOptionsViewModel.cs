using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Formatoptionen beim Speichern; Werte werden sofort in den Einstellungen gemerkt.</summary>
public sealed partial class SaveOptionsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public SaveOptionsViewModel(ISettingsService settings) => _settings = settings;

    public IReadOnlyList<Choice<ImageFormatSetting>> Formats { get; } =
        [.. ImageFormats.All.Select(f => new Choice<ImageFormatSetting>(ImageFormats.DisplayName(f), f))];

    public IReadOnlyList<Choice<TiffCompressionSetting>> TiffCompressions { get; } =
    [
        new(Strings.Tiff_None, TiffCompressionSetting.None),
        new("LZW", TiffCompressionSetting.Lzw),
        new("ZIP", TiffCompressionSetting.Zip),
        new("RLE", TiffCompressionSetting.Rle),
    ];

    public ImageFormatSetting Format
    {
        get => _settings.Current.LastSaveFormat;
        set => Update(s => s with { LastSaveFormat = value });
    }

    public bool HasJpegQuality => Format == ImageFormatSetting.Jpeg;

    public bool HasPng => Format == ImageFormatSetting.Png;

    public bool HasTiff => Format == ImageFormatSetting.Tiff;

    public bool HasJxr => Format == ImageFormatSetting.JpegXr;

    public bool HasBackground => !ImageFormats.SupportsAlpha(Format);

    public double JpegQuality
    {
        get => _settings.Current.JpegQuality;
        set => Update(s => s with { JpegQuality = (int)Math.Clamp(Math.Round(value), 1, 100) });
    }

    public bool PngInterlace
    {
        get => _settings.Current.PngInterlace;
        set => Update(s => s with { PngInterlace = value });
    }

    public TiffCompressionSetting TiffCompression
    {
        get => _settings.Current.TiffCompression;
        set => Update(s => s with { TiffCompression = value });
    }

    public double JxrQuality
    {
        get => _settings.Current.JxrQuality;
        set => Update(s => s with { JxrQuality = (int)Math.Clamp(Math.Round(value), 1, 100) });
    }

    public bool JxrLossless
    {
        get => _settings.Current.JxrLossless;
        set => Update(s => s with { JxrLossless = value });
    }

    public ArgbColor? Background
    {
        get => _settings.Current.FlattenBackground;
        set
        {
            if (value is { } color)
            {
                Update(s => s with { FlattenBackground = color with { A = 255 } });
            }
        }
    }

    private void Update(Func<AppSettings, AppSettings> change)
    {
        _settings.Current = change(_settings.Current);
        _ = _settings.SaveAsync();
        OnPropertyChanged(string.Empty);
    }
}
