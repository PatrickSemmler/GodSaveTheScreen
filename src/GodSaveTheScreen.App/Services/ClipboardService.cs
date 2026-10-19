using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Services;

/// <summary>Setzt das Bild als <c>CF_DIB</c> (BitmapSource) und als Format "PNG" (Alpha-fähige Ziele).</summary>
public sealed class ClipboardService : IClipboardService
{
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public void SetImage(PixelBuffer image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, image.ToBitmapSource());
        data.SetData("PNG", new MemoryStream(image.EncodePng()));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (COMException) when (attempt < Attempts)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }
}
