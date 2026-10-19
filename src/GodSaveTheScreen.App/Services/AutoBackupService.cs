using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Services;

/// <summary>Sichert ungespeicherte Dokumente automatisch und bietet sie nach einem Absturz zur Wiederherstellung an.</summary>
public interface IAutoBackupService
{
    /// <summary>Sucht verwaiste Sicherungen und fragt, ob sie wiederhergestellt werden sollen; liefert die Zahl der geöffneten Dokumente.</summary>
    Task<int> RecoverAsync();

    /// <summary>Sichert alle geänderten, ungespeicherten Dokumente sofort.</summary>
    Task BackupAllAsync();
}

/// <summary>
/// Alle <see cref="Interval"/> und kurz nach der letzten Änderung (<see cref="IdleDelay"/>) wird jedes ungespeicherte Dokument als
/// <c>{DocumentId}.gsts</c> im Sicherungsordner abgelegt. Der Schnappschuss entsteht auf dem UI-Thread, das Kodieren und Schreiben im Hintergrund.
/// Speichern oder Schließen löscht die Sicherung.
/// </summary>
public sealed class AutoBackupService : IHostedService, IAutoBackupService, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);

    private readonly IEditorService _editors;
    private readonly IProjectService _projects;
    private readonly IAppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly ILogger<AutoBackupService> _logger;
    private readonly Dictionary<ImageDocument, State> _states = [];
    private DispatcherTimer? _interval;
    private DispatcherTimer? _idle;
    private bool _running;

    public AutoBackupService(IEditorService editors, IProjectService projects, IAppPaths paths, IDialogService dialogs, ILogger<AutoBackupService> logger)
    {
        _editors = editors;
        _projects = projects;
        _paths = paths;
        _dialogs = dialogs;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _interval = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = Interval };
            _interval.Tick += async (_, _) => await BackupAllAsync();
            _interval.Start();
            _idle = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = IdleDelay };
            _idle.Tick += async (_, _) =>
            {
                _idle.Stop();
                await BackupAllAsync();
            };
            _editors.DocumentClosed += OnDocumentClosed;
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _interval?.Stop();
            _idle?.Stop();
            _editors.DocumentClosed -= OnDocumentClosed;
        });
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _interval?.Stop();
        _idle?.Stop();
    }

    public string BackupPath(ImageDocument document) => Path.Combine(_paths.AutosaveDirectory, $"{document.DocumentId}{ProjectSerializer.Extension}");

    public async Task BackupAllAsync()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        try
        {
            foreach (var document in _editors.OpenDocuments)
            {
                Track(document);
                var state = _states[document];
                if (!document.IsDirty)
                {
                    DeleteBackup(document);
                    state.Changed = false;
                    continue;
                }

                if (state.Changed || !state.HasBackup)
                {
                    await BackupAsync(document, state);
                }
            }
        }
        finally
        {
            _running = false;
        }
    }

    private async Task BackupAsync(ImageDocument document, State state)
    {
        try
        {
            state.Changed = false;
            state.BasePng ??= await Task.Run(() => _projects.EncodeBase(document));
            var content = _projects.Snapshot(document, state.BasePng);
            await _projects.WriteAsync(content, BackupPath(document));
            state.HasBackup = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or OutOfMemoryException)
        {
            // Eine fehlgeschlagene Sicherung darf die Arbeit nicht stören; beim nächsten Durchlauf erneut versuchen.
            state.Changed = true;
            _logger.LogWarning(ex, "Automatische Sicherung fehlgeschlagen");
        }
    }

    private void Track(ImageDocument document)
    {
        if (_states.ContainsKey(document))
        {
            return;
        }

        var state = new State { Changed = document.IsDirty };
        _states[document] = state;
        document.Undo.Changed += (_, _) =>
        {
            state.Changed = true;
            RestartIdleTimer();
        };
        document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ImageDocument.IsDirty) && !document.IsDirty)
            {
                // Gespeichert: die Sicherung wird nicht mehr gebraucht.
                DeleteBackup(document);
                state.HasBackup = false;
            }
        };
    }

    private void RestartIdleTimer()
    {
        if (_idle is null)
        {
            return;
        }

        _idle.Stop();
        _idle.Start();
    }

    private void OnDocumentClosed(object? sender, ImageDocument document)
    {
        DeleteBackup(document);
        _states.Remove(document);
    }

    private void DeleteBackup(ImageDocument document)
    {
        try
        {
            var path = BackupPath(document);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sicherung konnte nicht gelöscht werden");
        }
    }

    public async Task<int> RecoverAsync()
    {
        string[] files;
        try
        {
            files = Directory.Exists(_paths.AutosaveDirectory)
                ? Directory.GetFiles(_paths.AutosaveDirectory, "*" + ProjectSerializer.Extension)
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sicherungsordner konnte nicht gelesen werden");
            return 0;
        }

        if (files.Length == 0)
        {
            return 0;
        }

        var recover = _dialogs.AskRecovery(
            Strings.Recovery_Title,
            string.Format(CultureInfo.CurrentUICulture, Strings.Recovery_Message, files.Length));
        var opened = 0;
        foreach (var file in files)
        {
            if (recover && await TryOpenBackupAsync(file))
            {
                opened++;
            }

            TryDelete(file);
        }

        return opened;
    }

    private async Task<bool> TryOpenBackupAsync(string file)
    {
        try
        {
            var loaded = await _projects.LoadAsync(file);
            var document = loaded.Document;
            // Die Sicherung ist keine Projektdatei des Anwenders: ungespeichert und unbenannt öffnen.
            document.ProjectPath = null;
            document.IsDirty = true;
            _editors.Open(document, Strings.Recovery_Info);
            return true;
        }
        catch (Exception ex) when (ex is ProjectFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Sicherung {File} konnte nicht wiederhergestellt werden", file);
            return false;
        }
    }

    private void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Sicherung {File} konnte nicht gelöscht werden", file);
        }
    }

    private sealed class State
    {
        public bool Changed { get; set; }

        public bool HasBackup { get; set; }

        public byte[]? BasePng { get; set; }
    }
}
