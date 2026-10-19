using System.Windows;
using System.Windows.Shell;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Project;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>
/// Windows-Sprungliste der Taskleiste: Aufgaben „Rechteck aufnehmen“ usw. (<c>--capture …</c>) und die zuletzt verwendeten Dateien
/// als eigene Kategorie (funktioniert ohne Dateizuordnung im System).
/// </summary>
public sealed class JumpListService : IHostedService
{
    private readonly IRecentFilesService _recents;
    private readonly ILogger<JumpListService> _logger;

    public JumpListService(IRecentFilesService recents, ILogger<JumpListService> logger)
    {
        _recents = recents;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Build();
            _recents.Changed += OnRecentsChanged;
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _recents.Changed -= OnRecentsChanged;
        return Task.CompletedTask;
    }

    /// <summary>Baut die Sprungliste; Aufgaben aus <paramref name="exePath"/>, Dateien aus den zuletzt verwendeten.</summary>
    public static JumpList Create(string exePath, IEnumerable<RecentFile> recents)
    {
        var list = new JumpList { ShowRecentCategory = false, ShowFrequentCategory = false };
        (string Title, string Code)[] tasks =
        [
            (Strings.Mode_Rectangle, "rect"),
            (Strings.Mode_Window, "window"),
            (Strings.Mode_Freeform, "freeform"),
            (Strings.Mode_Fullscreen, "full"),
        ];
        foreach (var (title, code) in tasks)
        {
            list.JumpItems.Add(new JumpTask
            {
                Title = title,
                Arguments = $"--capture {code}",
                ApplicationPath = exePath,
                IconResourcePath = exePath,
                Description = title,
            });
        }

        foreach (var item in recents.Where(r => !r.IsMissing))
        {
            list.JumpItems.Add(new JumpTask
            {
                Title = item.Name,
                Description = item.Path,
                Arguments = $"\"{item.Path}\"",
                ApplicationPath = exePath,
                IconResourcePath = exePath,
                CustomCategory = Strings.Recent_Title,
            });
        }

        return list;
    }

    private void OnRecentsChanged(object? sender, EventArgs e) => Application.Current?.Dispatcher.BeginInvoke(Build);

    private void Build()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            var list = Create(exe, _recents.Items);
            JumpList.SetJumpList(Application.Current, list);
            list.Apply();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Sprungliste konnte nicht aktualisiert werden");
        }
    }
}
