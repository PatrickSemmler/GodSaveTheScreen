using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.App.Services;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Hosting;

/// <summary>Wertet Kommandozeilen aus (eigener Start und Weiterleitung weiterer Instanzen).</summary>
public sealed class CommandLineHandler : IRecipient<CommandLineReceivedMessage>
{
    private readonly CaptureCoordinator _coordinator;
    private readonly IAppShell _shell;
    private readonly IEditorService _editors;
    private readonly ILogger<CommandLineHandler> _logger;

    public CommandLineHandler(CaptureCoordinator coordinator, IAppShell shell, IEditorService editors, ILogger<CommandLineHandler> logger)
    {
        _coordinator = coordinator;
        _shell = shell;
        _editors = editors;
        _logger = logger;
    }

    public void Receive(CommandLineReceivedMessage message)
    {
        // Die Pipe liefert auf einem Threadpool-Thread.
        Application.Current.Dispatcher.BeginInvoke(() => Handle(message.Args));
    }

    public void Handle(string[] args)
    {
        _logger.LogInformation("Kommandozeile empfangen: [{Args}]", string.Join(' ', args.Select(a => $"\"{a}\"")));

        var options = CommandLineParser.Parse(args);
        foreach (var error in options.Errors)
        {
            _logger.LogWarning("Kommandozeile: {Error}", error);
        }

        if (options.Files.Count > 0)
        {
            _ = OpenFilesAsync(options.Files);
        }

        if (options.Capture is { } mode)
        {
            _ = StartCaptureAsync(new CaptureRequest(mode, TimeSpan.FromSeconds(options.DelaySeconds ?? 0)));
            return;
        }

        // Ein reiner Start mit --tray soll nur im Infobereich laufen; Dateien öffnen direkt den Editor.
        if (!options.Tray && options.Files.Count == 0)
        {
            _shell.ShowStartWindow();
        }
    }

    private async Task OpenFilesAsync(IReadOnlyList<string> files)
    {
        try
        {
            await _editors.OpenFilesAsync(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dateien über Kommandozeile konnten nicht geöffnet werden");
        }
    }

    private async Task StartCaptureAsync(CaptureRequest request)
    {
        try
        {
            await _coordinator.StartAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme über Kommandozeile fehlgeschlagen");
        }
    }
}
