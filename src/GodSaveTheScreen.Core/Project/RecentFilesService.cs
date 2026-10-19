using System.Text.Json;
using GodSaveTheScreen.Core.Settings;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.Core.Project;

/// <summary>Speichert die Liste als <c>recent.json</c> im Datenordner (fehlertolerant: eine defekte Datei ergibt eine leere Liste).</summary>
public sealed class RecentFilesService : IRecentFilesService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<RecentFilesService> _logger;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();
    private List<RecentFile> _items;

    public RecentFilesService(IAppPaths paths, ILogger<RecentFilesService> logger)
        : this(paths, logger, () => DateTimeOffset.Now)
    {
    }

    public RecentFilesService(IAppPaths paths, ILogger<RecentFilesService> logger, Func<DateTimeOffset> clock)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = paths.RecentFilesPath;
        _logger = logger;
        _clock = clock;
        _items = Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<RecentFile> Items
    {
        get
        {
            lock (_gate)
            {
                return [.. _items];
            }
        }
    }

    public static RecentFileKind KindOf(string path) =>
        string.Equals(Path.GetExtension(path), ProjectSerializer.Extension, StringComparison.OrdinalIgnoreCase) ? RecentFileKind.Project : RecentFileKind.Image;

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var full = Normalize(path);
        lock (_gate)
        {
            _items.RemoveAll(i => SamePath(i.Path, full));
            _items.Insert(0, new RecentFile(full, KindOf(full), _clock()));
            if (_items.Count > IRecentFilesService.MaxEntries)
            {
                _items.RemoveRange(IRecentFilesService.MaxEntries, _items.Count - IRecentFilesService.MaxEntries);
            }

            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (_items.RemoveAll(i => SamePath(i.Path, full)) == 0)
            {
                return;
            }

            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private List<RecentFile> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var items = JsonSerializer.Deserialize<List<RecentFile>>(File.ReadAllText(_path), Options) ?? [];
            return [.. items
                .Where(i => !string.IsNullOrWhiteSpace(i.Path))
                .DistinctBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(i => i.LastUsed)
                .Take(IRecentFilesService.MaxEntries)];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Liste der zuletzt verwendeten Dateien konnte nicht gelesen werden");
            return [];
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_items, Options));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Liste der zuletzt verwendeten Dateien konnte nicht gespeichert werden");
        }
    }
}
