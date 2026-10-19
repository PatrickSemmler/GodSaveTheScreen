using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Project;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Zeile der Liste „Zuletzt verwendet“ im Startfenster (Vorschaubild wird nachgeladen).</summary>
public sealed partial class RecentItemViewModel : ObservableObject
{
    private const int ThumbnailWidth = 160;

    public RecentItemViewModel(RecentFile file)
    {
        File = file;
        IsMissing = file.IsMissing;
    }

    public RecentFile File { get; }

    public string Name => File.Name;

    public string Path => File.Path;

    public string Directory => System.IO.Path.GetDirectoryName(File.Path) ?? string.Empty;

    public bool IsProject => File.Kind == RecentFileKind.Project;

    public bool IsMissing { get; }

    [ObservableProperty]
    private ImageSource? _thumbnail;

    /// <summary>Lädt die Vorschau: <c>preview.png</c> aus Projekten, sonst das auf Listengröße dekodierte Bild.</summary>
    public async Task LoadThumbnailAsync(IProjectService projects)
    {
        if (IsMissing)
        {
            return;
        }

        try
        {
            if (IsProject)
            {
                if (await projects.ReadPreviewAsync(File.Path) is { Length: > 0 } bytes)
                {
                    Thumbnail = Decode(bytes);
                }
            }
            else
            {
                Thumbnail = await Task.Run(() => LoadImage(File.Path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException or FileFormatException)
        {
            // Ohne Vorschau anzeigen.
        }
    }

    private static BitmapImage Decode(byte[] bytes)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = ThumbnailWidth;
        image.StreamSource = new MemoryStream(bytes);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapImage LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = ThumbnailWidth;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
