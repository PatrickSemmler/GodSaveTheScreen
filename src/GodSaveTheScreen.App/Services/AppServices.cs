using System.IO;
using System.Windows;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.Views;
using GodSaveTheScreen.Core.Imaging;
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

    public void ShowError(string title, string message) =>
        Show(title, message, DialogKind.Error, [new DialogButton(Strings.Dialog_Ok, Primary, IsPrimary: true, IsCancel: true)]);

    public bool Confirm(string title, string message) =>
        Show(title, message, DialogKind.Question,
            [new DialogButton(Strings.Dialog_Yes, Primary, IsPrimary: true), new DialogButton(Strings.Dialog_No, Secondary, IsCancel: true)]) == Primary;

    public SaveChangesAnswer AskSaveChanges(string title, string message) =>
        Show(title, message, DialogKind.Warning,
        [
            new DialogButton(Strings.Dialog_Save, Primary, IsPrimary: true),
            new DialogButton(Strings.Dialog_DontSave, Secondary),
            new DialogButton(Strings.Dialog_Cancel, Cancelled, IsCancel: true),
        ]) switch
        {
            Primary => SaveChangesAnswer.Save,
            Secondary => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel,
        };

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

    public IReadOnlyList<string> PickImageFiles()
    {
        var all = string.Join(';', ImageFileFilter.GetOpenExtensions().Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Filter = $"{Strings.Open_AllImages} ({all})|{all}|{Strings.Open_AllFiles} (*.*)|*.*",
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
