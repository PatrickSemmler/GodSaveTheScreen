using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Services;

public interface IDialogService
{
    void ShowError(string title, string message);

    /// <summary>Fragt nach einem Zielpfad. Gibt <c>null</c> bei Abbruch zurück.</summary>
    string? PickSavePath(string suggestedFileName, string filter);
}

public interface IClipboardService
{
    void SetImage(PixelBuffer image);
}
