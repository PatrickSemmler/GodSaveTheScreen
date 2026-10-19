using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Views;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Hosting;

/// <summary>Wertet Kommandozeilen aus (eigener Start und Weiterleitung weiterer Instanzen).</summary>
public sealed class CommandLineHandler : IRecipient<CommandLineReceivedMessage>
{
    private readonly CaptureCoordinator _coordinator;
    private readonly ILogger<CommandLineHandler> _logger;

    public CommandLineHandler(CaptureCoordinator coordinator, ILogger<CommandLineHandler> logger)
    {
        _coordinator = coordinator;
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
            _logger.LogInformation("Dateiargumente werden ab M4 geöffnet: {Files}", string.Join(", ", options.Files));
        }

        if (options.Capture is { } mode)
        {
            _ = StartCaptureAsync(mode);
            return;
        }

        ActivateStartWindow();
    }

    private async Task StartCaptureAsync(CaptureMode mode)
    {
        try
        {
            await _coordinator.StartAsync(mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme über Kommandozeile fehlgeschlagen");
        }
    }

    private static void ActivateStartWindow()
    {
        var window = Application.Current.Windows.OfType<StartWindow>().FirstOrDefault();
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
    }
}
