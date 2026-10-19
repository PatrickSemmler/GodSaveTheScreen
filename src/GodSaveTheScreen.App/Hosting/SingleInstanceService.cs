using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace GodSaveTheScreen.App.Hosting;

/// <summary>Wird ausgelöst, wenn eine weitere Instanz ihre Kommandozeile an die laufende übergeben hat.</summary>
public sealed record CommandLineReceivedMessage(string[] Args);

/// <summary>Mutex + Named Pipe: der zweite Start übergibt seine Argumente an die erste Instanz und beendet sich.</summary>
public sealed class SingleInstanceService : IDisposable
{
    private const int ConnectTimeoutMs = 3000;

    private readonly string _sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
    private readonly CancellationTokenSource _cts = new();
    private Mutex? _mutex;
    private Task? _serverTask;

    private string MutexName => $"Local\\GodSaveTheScreen-{_sid}";

    private string PipeName => $"GodSaveTheScreen-{_sid}";

    /// <summary>
    /// <c>true</c>: erste Instanz. <c>false</c>: es läuft bereits eine Instanz; die Argumente wurden an sie gesendet.
    /// </summary>
    public bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        SendToRunningInstance(args);
        return false;
    }

    /// <summary>Startet die Pipe-Schleife der ersten Instanz.</summary>
    public void StartServer(ILogger logger)
    {
        _serverTask = Task.Run(() => ServerLoopAsync(logger, _cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Abbruch beim Beenden ist erwartet.
        }

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex gehört einem anderen Thread: beim Prozessende wird er ohnehin freigegeben.
            }

            _mutex.Dispose();
        }

        _cts.Dispose();
    }

    private void SendToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(ConnectTimeoutMs);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            foreach (var arg in args)
            {
                writer.WriteLine(arg);
            }
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // Die erste Instanz ist nicht erreichbar (z. B. gerade am Beenden); der zweite Start beendet sich trotzdem.
        }
    }

    private async Task ServerLoopAsync(ILogger logger, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                var args = new List<string>();
                using var reader = new StreamReader(server, Encoding.UTF8);
                while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    args.Add(line);
                }

                WeakReferenceMessenger.Default.Send(new CommandLineReceivedMessage([.. args]));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Fehler in der Single-Instance-Pipe");
            }
        }
    }
}
