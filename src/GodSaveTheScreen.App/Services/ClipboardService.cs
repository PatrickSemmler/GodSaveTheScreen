using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Services;

/// <summary>
/// Setzt das Bild als <c>CF_DIB</c> (BitmapSource) und als Format "PNG" (Alpha-fähige Ziele);
/// Objekte zusätzlich im eigenen Format <see cref="AnnotationFormat"/>.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public const string AnnotationFormat = "GodSaveTheScreen.Annotations";

    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public void SetImage(PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(image);
        SetData(CreateImageData(image));
    }

    public void SetAnnotations(IReadOnlyList<Annotation> annotations, PixelBuffer? renderedFallback)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var data = renderedFallback is null ? new DataObject() : CreateImageData(renderedFallback);
        data.SetData(AnnotationFormat, AnnotationSerializer.Serialize(annotations));
        SetData(data);
    }

    public IReadOnlyList<Annotation> GetAnnotations()
    {
        return Retry(() =>
        {
            if (!Clipboard.ContainsData(AnnotationFormat) || Clipboard.GetData(AnnotationFormat) is not string json)
            {
                return (IReadOnlyList<Annotation>)[];
            }

            try
            {
                return AnnotationSerializer.DeserializeAsCopies(json);
            }
            catch (JsonException)
            {
                return [];
            }
        });
    }

    public bool ContainsImage() =>
        Retry(() => Clipboard.ContainsData("PNG") || Clipboard.ContainsImage() || ContainsImageFile());

    public PixelBuffer? GetImage()
    {
        return Retry(() =>
        {
            if (Clipboard.GetData("PNG") is Stream png)
            {
                using (png)
                {
                    var decoder = BitmapDecoder.Create(png, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    return PixelBufferExtensions.FromBitmapSource(decoder.Frames[0]);
                }
            }

            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap)
            {
                return PixelBufferExtensions.FromBitmapSource(bitmap);
            }

            if (Clipboard.ContainsFileDropList())
            {
                var file = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault(IsImageFile);
                if (file is not null)
                {
                    return ImageCodecService.Load(file).Image;
                }
            }

            return null;
        });
    }

    private static bool ContainsImageFile() =>
        Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Cast<string>().Any(IsImageFile);

    private static bool IsImageFile(string path) => ImageFileFilter.GetOpenExtensions().Contains(Path.GetExtension(path).ToLowerInvariant());

    private static DataObject CreateImageData(PixelBuffer image)
    {
        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, image.ToBitmapSource());
        data.SetData("PNG", new MemoryStream(image.EncodePng()));
        return data;
    }

    private static void SetData(DataObject data) => Retry<object?>(() =>
    {
        Clipboard.SetDataObject(data, copy: true);
        return null;
    });

    private static T Retry<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException) when (attempt < Attempts)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
