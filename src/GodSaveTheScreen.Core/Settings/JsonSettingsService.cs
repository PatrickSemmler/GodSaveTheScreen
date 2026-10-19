using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.Core.Settings;

public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private readonly IAppPaths _paths;
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private AppSettings _current;

    public JsonSettingsService(IAppPaths paths, ILogger<JsonSettingsService> logger)
    {
        _paths = paths;
        _logger = logger;
        _current = Load();
    }

    public event EventHandler? Changed;

    public AppSettings Current
    {
        get => _current;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _current = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _saveLock.Dispose();

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            var target = _paths.SettingsFilePath;
            var temp = target + ".tmp";
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, _current, SettingsJsonContext.Default.AppSettings, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (File.Exists(target))
            {
                File.Replace(temp, target, null);
            }
            else
            {
                File.Move(temp, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die meisten Aufrufer starten das Speichern ohne zu warten: der Fehler darf nicht als unbeobachtete Task-Ausnahme enden.
            _logger.LogError(ex, "Einstellungen konnten nicht gespeichert werden");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private AppSettings Load()
    {
        var path = _paths.SettingsFilePath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(path);
            return (JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings) ?? new AppSettings()).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Einstellungsdatei {Path} nicht lesbar, Standardwerte werden verwendet", path);
            return new AppSettings();
        }
    }
}
