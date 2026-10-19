using CommunityToolkit.Mvvm.ComponentModel;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Grundform des Dokuments: Basisbild plus Metadaten. Annotationen folgen in späteren Meilensteinen.</summary>
public sealed partial class ImageDocument : ObservableObject
{
    public ImageDocument(PixelBuffer baseImage, double dpiX = 96, double dpiY = 96)
    {
        BaseImage = baseImage ?? throw new ArgumentNullException(nameof(baseImage));
        DpiX = dpiX;
        DpiY = dpiY;
        CreatedAt = DateTimeOffset.Now;
    }

    public PixelBuffer BaseImage { get; }

    public double DpiX { get; }

    public double DpiY { get; }

    public DateTimeOffset CreatedAt { get; }

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private bool _isDirty;
}
