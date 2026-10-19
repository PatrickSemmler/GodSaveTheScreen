using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

/// <summary>Autostart über <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> (Aufruf mit <c>--tray</c>).</summary>
public interface IAutostartService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    /// <summary>Korrigiert einen vorhandenen Eintrag, wenn die EXE verschoben wurde (portable Nutzung).</summary>
    void RepairPath();
}

/// <summary>Zugriff auf den Run-Schlüssel (austauschbar für Tests).</summary>
public interface IAutostartRegistry
{
    string? Read(string name);

    void Write(string name, string value);

    void Remove(string name);
}

public sealed class RunKeyAutostartRegistry : IAutostartRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(name) as string;
    }

    public void Write(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Remove(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class AutostartService : IAutostartService
{
    public const string ValueName = "GodSaveTheScreen";

    private readonly IAutostartRegistry _registry;
    private readonly string _command;

    public AutostartService(IAutostartRegistry registry)
        : this(registry, Environment.ProcessPath ?? string.Empty)
    {
    }

    public AutostartService(IAutostartRegistry registry, string executablePath)
    {
        _registry = registry;
        _command = $"\"{executablePath}\" --tray";
    }

    public bool IsEnabled => !string.IsNullOrEmpty(_registry.Read(ValueName));

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _registry.Write(ValueName, _command);
        }
        else
        {
            _registry.Remove(ValueName);
        }
    }

    public void RepairPath()
    {
        var current = _registry.Read(ValueName);
        if (!string.IsNullOrEmpty(current) && !string.Equals(current, _command, StringComparison.OrdinalIgnoreCase))
        {
            _registry.Write(ValueName, _command);
        }
    }
}
