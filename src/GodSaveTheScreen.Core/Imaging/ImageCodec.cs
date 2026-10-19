using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Ergebnis des Ladens: erster Frame als Pixelpuffer plus Metadaten.</summary>
public sealed record LoadedImage(PixelBuffer Image, double DpiX, double DpiY, int FrameCount);

/// <summary>Formatoptionen beim Speichern.</summary>
public sealed record ImageSaveOptions(
    ImageFormatSetting Format,
    int Quality = 90,
    bool PngInterlace = false,
    TiffCompressionSetting TiffCompression = TiffCompressionSetting.Lzw,
    bool JxrLossless = false,
    ArgbColor Background = default)
{
    /// <summary>Hintergrund für Formate ohne Alpha; Standard Weiß.</summary>
    public ArgbColor EffectiveBackground => Background == default ? ArgbColor.White : Background with { A = 255 };

    public static ImageSaveOptions FromSettings(AppSettings settings, ImageFormatSetting format) => new(
        format,
        format == ImageFormatSetting.JpegXr ? settings.JxrQuality : settings.JpegQuality,
        settings.PngInterlace,
        settings.TiffCompression,
        settings.JxrLossless,
        settings.FlattenBackground);
}

public interface IImageCodecService
{
    /// <summary>Lädt den ersten Frame (EXIF-Orientierung berücksichtigt).</summary>
    Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Speichert atomar (temporäre Datei, dann Ersetzen).</summary>
    Task SaveAsync(PixelBuffer image, double dpiX, double dpiY, string path, ImageSaveOptions options, CancellationToken cancellationToken = default);

    byte[] Encode(PixelBuffer image, double dpiX, double dpiY, ImageSaveOptions options);
}

/// <summary>Rendert das Dokument (Basisbild + sichtbare Annotationen) in einen Pixelpuffer.</summary>
public interface IDocumentExporter
{
    PixelBuffer Render(ImageDocument document);

    /// <summary>Nur die angegebenen Objekte (zugeschnitten auf deren Ausdehnung) auf transparentem Grund.</summary>
    PixelBuffer? RenderObjects(IReadOnlyList<Annotation> annotations);
}

/// <summary>Bekannte Formate: Endungen, Anzeigenamen.</summary>
public static class ImageFormats
{
    public static IReadOnlyList<ImageFormatSetting> All { get; } =
    [
        ImageFormatSetting.Png,
        ImageFormatSetting.Jpeg,
        ImageFormatSetting.Bmp,
        ImageFormatSetting.Gif,
        ImageFormatSetting.Tiff,
        ImageFormatSetting.JpegXr,
    ];

    public static string DisplayName(ImageFormatSetting format) => format switch
    {
        ImageFormatSetting.Jpeg => "JPEG",
        ImageFormatSetting.Bmp => "BMP",
        ImageFormatSetting.Gif => "GIF",
        ImageFormatSetting.Tiff => "TIFF",
        ImageFormatSetting.JpegXr => "JPEG XR",
        _ => "PNG",
    };

    public static IReadOnlyList<string> Extensions(ImageFormatSetting format) => format switch
    {
        ImageFormatSetting.Jpeg => [".jpg", ".jpeg"],
        ImageFormatSetting.Bmp => [".bmp"],
        ImageFormatSetting.Gif => [".gif"],
        ImageFormatSetting.Tiff => [".tif", ".tiff"],
        ImageFormatSetting.JpegXr => [".jxr", ".wdp"],
        _ => [".png"],
    };

    public static ImageFormatSetting? FromExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        foreach (var format in All)
        {
            if (Extensions(format).Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return format;
            }
        }

        return null;
    }

    /// <summary>Formate ohne Alpha-Kanal (Transparenz wird auf den Hintergrund verrechnet).</summary>
    public static bool SupportsAlpha(ImageFormatSetting format) =>
        format is not (ImageFormatSetting.Jpeg or ImageFormatSetting.Bmp or ImageFormatSetting.Gif);
}
