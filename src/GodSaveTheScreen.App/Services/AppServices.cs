using System.IO;
using System.Windows;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

public interface IWindowFactory
{
    /// <summary>Erzeugt ein Fenster über DI; <paramref name="args"/> ergänzen nicht registrierte Konstruktorparameter.</summary>
    TWindow Create<TWindow>(params object[] args)
        where TWindow : Window;
}

public sealed class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _services;
    private readonly IWindowExclusionService _exclusion;

    public WindowFactory(IServiceProvider services, IWindowExclusionService exclusion)
    {
        _services = services;
        _exclusion = exclusion;
    }

    public TWindow Create<TWindow>(params object[] args)
        where TWindow : Window
    {
        var window = ActivatorUtilities.CreateInstance<TWindow>(_services, args);
        _exclusion.Register(window);
        return window;
    }
}

public sealed class DialogService : IDialogService
{
    private const int Primary = 0;
    private const int Secondary = 1;
    private const int Cancelled = 2;
    private const int SaveProject = 3;

    public void ShowError(string title, string message) =>
        Show(title, message, DialogKind.Error, [new DialogButton(Strings.Dialog_Ok, Primary, IsPrimary: true, IsCancel: true)]);

    public void ShowInfo(string title, string message) =>
        Show(title, message, DialogKind.Info, [new DialogButton(Strings.Dialog_Ok, Primary, IsPrimary: true, IsCancel: true)]);

    public void ShowCrash(string title, string message, string logDirectory) =>
        Show(title, message, DialogKind.Error,
        [
            new DialogButton(Strings.Dialog_OpenLogFolder, Secondary, Invoke: () => OpenFolder(logDirectory)),
            new DialogButton(Strings.Dialog_Ok, Primary, IsPrimary: true, IsCancel: true),
        ]);

    private static void OpenFolder(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // Der Ordner lässt sich nicht öffnen; die Meldung bleibt stehen.
        }
    }

    public bool Confirm(string title, string message) =>
        Show(title, message, DialogKind.Question,
            [new DialogButton(Strings.Dialog_Yes, Primary, IsPrimary: true), new DialogButton(Strings.Dialog_No, Secondary, IsCancel: true)]) == Primary;

    public SaveChangesAnswer AskSaveChanges(string title, string message, bool offerProject = false)
    {
        List<DialogButton> buttons =
        [
            new DialogButton(Strings.Dialog_Save, Primary, IsPrimary: true),
            new DialogButton(Strings.Dialog_DontSave, Secondary),
            new DialogButton(Strings.Dialog_Cancel, Cancelled, IsCancel: true),
        ];
        if (offerProject)
        {
            buttons.Insert(1, new DialogButton(Strings.Dialog_SaveAsProject, SaveProject));
        }

        return Show(title, message, DialogKind.Warning, buttons) switch
        {
            Primary => SaveChangesAnswer.Save,
            SaveProject => SaveChangesAnswer.SaveAsProject,
            Secondary => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel,
        };
    }

    public bool AskRecovery(string title, string message) =>
        Show(title, message, DialogKind.Question,
            [new DialogButton(Strings.Dialog_Recover, Primary, IsPrimary: true), new DialogButton(Strings.Dialog_Discard, Secondary, IsCancel: true)]) == Primary;

    /// <summary>Zeigt den themenkonformen Dialog modal; Besitzer ist das aktive App-Fenster.</summary>
    private static int Show(string title, string message, DialogKind kind, IReadOnlyList<DialogButton> buttons)
    {
        var dialog = new MessageDialog(title, message, kind, buttons);
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

    public string? AskPresetName(string title, string prompt, IReadOnlyList<string> existingNames, string initialName)
    {
        var dialog = new PresetNameDialog(title, prompt, existingNames, initialName);
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

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { InitialDirectory = initialDirectory ?? string.Empty };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickSavePath(string suggestedFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true,
            Title = Strings.Editor_SaveAs,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public (string Path, ImageFormatSetting Format)? PickImageSavePath(string suggestedName, string? initialDirectory, ImageFormatSetting format)
    {
        var formats = ImageFormats.All;
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            InitialDirectory = initialDirectory is not null && Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            Filter = string.Join('|', formats.Select(f =>
            {
                var patterns = string.Join(';', ImageFormats.Extensions(f).Select(e => "*" + e));
                return $"{ImageFormats.DisplayName(f)} ({patterns})|{patterns}";
            })),
            FilterIndex = formats.ToList().IndexOf(format) + 1,
            AddExtension = true,
            OverwritePrompt = true,
            Title = Strings.Editor_SaveAs,
        };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var chosen = ImageFormats.FromExtension(System.IO.Path.GetExtension(dialog.FileName))
            ?? formats[Math.Clamp(dialog.FilterIndex - 1, 0, formats.Count - 1)];
        return (dialog.FileName, chosen);
    }

    public (string Path, ImageFormatSetting? Format)? PickDocumentSavePath(string suggestedName, string? initialDirectory, ImageFormatSetting format, bool preferProject)
    {
        var formats = ImageFormats.All;
        var project = $"{Strings.Open_Projects} (*{ProjectSerializer.Extension})|*{ProjectSerializer.Extension}";
        var dialog = new SaveFileDialog
        {
            FileName = System.IO.Path.GetFileNameWithoutExtension(suggestedName),
            InitialDirectory = initialDirectory is not null && Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            Filter = project + "|" + string.Join('|', formats.Select(f =>
            {
                var patterns = string.Join(';', ImageFormats.Extensions(f).Select(e => "*" + e));
                return $"{ImageFormats.DisplayName(f)} ({patterns})|{patterns}";
            })),
            FilterIndex = preferProject ? 1 : formats.ToList().IndexOf(format) + 2,
            AddExtension = true,
            OverwritePrompt = true,
            Title = Strings.Editor_SaveAs,
        };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var extension = System.IO.Path.GetExtension(dialog.FileName);
        if (string.Equals(extension, ProjectSerializer.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return (dialog.FileName, null);
        }

        var chosen = ImageFormats.FromExtension(extension);
        if (chosen is null)
        {
            // Filter 1 = Projekt, 2… = Bildformate.
            return dialog.FilterIndex == 1
                ? (dialog.FileName + ProjectSerializer.Extension, null)
                : (dialog.FileName, formats[Math.Clamp(dialog.FilterIndex - 2, 0, formats.Count - 1)]);
        }

        return (dialog.FileName, chosen);
    }

    public IReadOnlyList<string> PickImageFiles()
    {
        var images = string.Join(';', ImageFileFilter.GetOpenExtensions().Select(e => "*" + e));
        var all = images + ";*" + ProjectSerializer.Extension;
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Filter = $"{Strings.Open_AllSupported} ({all})|{all}"
                + $"|{Strings.Open_Projects} (*{ProjectSerializer.Extension})|*{ProjectSerializer.Extension}"
                + $"|{Strings.Open_AllImages} ({images})|{images}"
                + $"|{Strings.Open_AllFiles} (*.*)|*.*",
            Title = Strings.Start_Open,
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }
}

/// <summary>Dateiendungen, die geöffnet werden können: bekannte Liste plus installierte WIC-Decoder (Registry).</summary>
public static class ImageFileFilter
{
    private const string DecoderCategory = @"CLSID\{7ED96837-96F0-4812-B211-551B5C98A7AE}\Instance";

    private static readonly string[] KnownExtensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".dib", ".gif", ".tif", ".tiff", ".ico", ".jxr", ".wdp", ".webp", ".heic", ".heif", ".avif",
    ];

    private static IReadOnlyList<string>? _cache;

    public static IReadOnlyList<string> GetOpenExtensions() => _cache ??= Load();

    private static IReadOnlyList<string> Load()
    {
        var result = new List<string>(KnownExtensions);
        try
        {
            using var instances = Registry.ClassesRoot.OpenSubKey(DecoderCategory);
            foreach (var clsid in instances?.GetSubKeyNames() ?? [])
            {
                using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
                if (key?.GetValue("FileExtensions") is string extensions)
                {
                    result.AddRange(extensions.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Die bekannte Liste genügt.
        }

        return [.. result.Where(e => e.StartsWith('.')).Select(e => e.ToLowerInvariant()).Distinct()];
    }
}
