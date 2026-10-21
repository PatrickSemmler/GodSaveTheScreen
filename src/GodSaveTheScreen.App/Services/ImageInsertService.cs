using System.IO;
using System.Windows;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Rendering;
using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

/// <summary>Dialoge beim Einfügen von Bildobjekten: Dateiauswahl und Wahl des Einzelbilds einer Mehrbilddatei.</summary>
public interface IImageInsertDialogs
{
    /// <summary>Dateiauswahl (mehrere, Rasterformate und SVG); leer bei Abbruch.</summary>
    IReadOnlyList<string> PickImageFiles();

    /// <summary>Einzelbild einer Mehrbilddatei (0-basiert); <c>null</c> bei Abbruch.</summary>
    int? PickFrame(ImageImport import, string fileName);
}

public sealed class ImageInsertDialogs : IImageInsertDialogs
{
    public IReadOnlyList<string> PickImageFiles()
    {
        var images = string.Join(';', ImageInsert.Extensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Filter = $"{Strings.Open_AllImages} ({images})|{images}|SVG (*.svg)|*.svg|{Strings.Open_AllFiles} (*.*)|*.*",
            Title = Strings.Image_InsertTitle,
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public int? PickFrame(ImageImport import, string fileName)
    {
        var dialog = new FramePickerDialog(import, fileName);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible && w != dialog);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }
}

/// <summary>Bilddateien für Bildobjekte vorbereiten (Dateien, Zwischenablage).</summary>
public static class ImageInsert
{
    /// <summary>Dateiendungen, die als Bildobjekt eingefügt werden können (WIC-Formate und SVG).</summary>
    public static IReadOnlyList<string> Extensions => [.. ImageFileFilter.GetOpenExtensions(), ".svg"];

    public static bool IsInsertable(string path) =>
        Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Liest Dateien; bei Mehrbilddateien fragt <paramref name="dialogs"/> nach dem Einzelbild (Abbruch überspringt die Datei).
    /// Nicht lesbare Dateien landen in <paramref name="failed"/>.
    /// </summary>
    public static List<(ImportedImage Image, string? Name)> Load(IEnumerable<string> paths, IImageInsertDialogs dialogs, ICollection<string> failed)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(failed);
        var result = new List<(ImportedImage, string?)>();
        foreach (var path in paths)
        {
            try
            {
                var import = ImageImport.FromFile(path);
                var frame = 0;
                if (import.FrameCount > 1)
                {
                    if (dialogs.PickFrame(import, Path.GetFileName(path)) is not { } chosen)
                    {
                        continue;
                    }

                    frame = chosen;
                }

                result.Add((import.Create(frame), Path.GetFileNameWithoutExtension(path)));
            }
            catch (ImageImportException)
            {
                failed.Add(Path.GetFileName(path));
            }
        }

        return result;
    }
}
