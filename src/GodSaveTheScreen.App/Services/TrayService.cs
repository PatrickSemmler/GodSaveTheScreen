using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GodSaveTheScreen.App.Capture;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Settings;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GodSaveTheScreen.App.Services;

/// <summary>Tray-Symbol mit Kontextmenü, Klickaktion und „Gespeichert unter …“-Benachrichtigung.</summary>
public sealed class TrayService : IHostedService, INotificationService, IDisposable
{
    private static readonly int[] PresetTimers = [3, 5, 10];

    private readonly ISettingsService _settings;
    private readonly IAppShell _shell;
    private readonly IServiceProvider _services;
    private readonly ILogger<TrayService> _logger;
    private TaskbarIcon? _icon;
    private string? _lastSavedPath;

    public TrayService(ISettingsService settings, IAppShell shell, IServiceProvider services, ILogger<TrayService> logger)
    {
        _settings = settings;
        _shell = shell;
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        RunOnUi(CreateIcon);
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        RunOnUi(DisposeIcon);
        return Task.CompletedTask;
    }

    public void Dispose() => DisposeIcon();

    public void ShowSaved(string path)
    {
        _lastSavedPath = path;
        RunOnUi(() => _icon?.ShowNotification(
            Strings.Tray_SavedTitle,
            string.Format(CultureInfo.CurrentUICulture, Strings.Tray_SavedMessage, path),
            NotificationIcon.Info));
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    /// <summary>Hell/dunkel passend zum Taskleisten-Theme: helle Taskleiste → dunkles Symbol.</summary>
    private static string IconPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var lightTaskbar = key?.GetValue("SystemUsesLightTheme") is 1;
        return lightTaskbar ? "tray-dark.png" : "tray-light.png";
    }

    private void CreateIcon()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = Strings.App_Name,
            Icon = LoadIcon(),
            ContextMenu = BuildMenu(),
            NoLeftClickDelay = true,
            MenuActivation = PopupActivationMode.RightClick,
        };
        _icon.TrayLeftMouseUp += (_, _) => OnLeftClick();
        _icon.TrayBalloonTipClicked += (_, _) => OpenSavedFolder();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void DisposeIcon()
    {
        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>Lädt das PNG als <see cref="System.Drawing.Icon"/> (ein PNG kann nicht direkt als <c>IconSource</c> dienen).</summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{IconPath()}", UriKind.Absolute));
        using var stream = resource.Stream;
        using var bitmap = new System.Drawing.Bitmap(stream);
        var handle = bitmap.GetHicon();
        try
        {
            // Clone, damit das Symbol nach DestroyIcon weiter gültig bleibt.
            using var temporary = System.Drawing.Icon.FromHandle(handle);
            return (System.Drawing.Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_icon is not null)
                {
                    _icon.Icon = LoadIcon();
                }
            });
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item(Strings.Mode_Rectangle, () => Capture(CaptureMode.Rectangle)));
        menu.Items.Add(Item(Strings.Mode_Window, () => Capture(CaptureMode.Window)));
        menu.Items.Add(Item(Strings.Mode_Freeform, () => Capture(CaptureMode.Freeform)));
        menu.Items.Add(Item(Strings.Mode_Fullscreen, () => Capture(CaptureMode.FullScreen)));

        var timer = new MenuItem { Header = Strings.Tray_WithTimer };
        menu.Items.Add(timer);
        timer.SubmenuOpened += (_, _) => FillTimerMenu(timer);
        FillTimerMenu(timer);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.Tray_OpenImage, OpenFiles));
        var recent = new MenuItem { Header = Strings.Recent_Title };
        menu.Items.Add(recent);
        recent.SubmenuOpened += (_, _) => FillRecentMenu(recent);
        FillRecentMenu(recent);
        menu.Items.Add(Item(Strings.Tray_StartWindow, _shell.ShowStartWindow));
        menu.Items.Add(Item(Strings.Start_Settings, _shell.ShowSettings));
        menu.Items.Add(Item(Strings.Help_Title, () => _shell.ShowHelp()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.Tray_Exit, _shell.ExitApplication));
        return menu;
    }

    private void OpenFiles()
    {
        var files = _services.GetRequiredService<GodSaveTheScreen.Core.Services.IDialogService>().PickImageFiles();
        if (files.Count > 0)
        {
            _ = _services.GetRequiredService<IEditorService>().OpenFilesAsync(files);
        }
    }

    private void FillRecentMenu(MenuItem parent)
    {
        parent.Items.Clear();
        var items = _services.GetRequiredService<IRecentFilesService>().Items;
        if (items.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = Strings.Recent_Empty, IsEnabled = false });
            return;
        }

        foreach (var file in items)
        {
            var entry = new MenuItem { Header = file.Name.Replace("_", "__", StringComparison.Ordinal), ToolTip = file.Path, IsEnabled = !file.IsMissing };
            var path = file.Path;
            entry.Click += (_, _) => _ = _services.GetRequiredService<IEditorService>().OpenFilesAsync([path]);
            parent.Items.Add(entry);
        }

        parent.Items.Add(new Separator());
        parent.Items.Add(Item(Strings.Recent_Clear, () => _services.GetRequiredService<IRecentFilesService>().Clear()));
    }

    private void FillTimerMenu(MenuItem parent)
    {
        parent.Items.Clear();
        var seconds = PresetTimers.ToList();
        var custom = _settings.Current.CustomTimerSeconds;
        if (custom > 0 && !seconds.Contains(custom))
        {
            seconds.Add(custom);
        }

        foreach (var s in seconds)
        {
            var delay = s;
            parent.Items.Add(Item(
                string.Format(CultureInfo.CurrentUICulture, Strings.Tray_Seconds, s),
                () => Capture(_settings.Current.DefaultMode, TimeSpan.FromSeconds(delay))));
        }
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnLeftClick()
    {
        switch (_settings.Current.TrayClickAction)
        {
            case TrayClickActionSetting.CaptureRectangle:
                Capture(CaptureMode.Rectangle);
                break;
            case TrayClickActionSetting.CaptureWindow:
                Capture(CaptureMode.Window);
                break;
            case TrayClickActionSetting.CaptureFreeform:
                Capture(CaptureMode.Freeform);
                break;
            case TrayClickActionSetting.CaptureFullScreen:
                Capture(CaptureMode.FullScreen);
                break;
            case TrayClickActionSetting.ShowStartWindow:
                _shell.ShowStartWindow();
                break;
        }
    }

    private void Capture(CaptureMode mode, TimeSpan? delay = null)
    {
        var request = delay is { } d ? new CaptureRequest(mode, d) : CaptureRequest.FromSettings(_settings.Current, mode);
        _ = StartCaptureAsync(request);
    }

    private async Task StartCaptureAsync(CaptureRequest request)
    {
        try
        {
            await _services.GetRequiredService<CaptureCoordinator>().StartAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aufnahme über das Tray-Menü fehlgeschlagen");
        }
    }

    private void OpenSavedFolder()
    {
        if (_lastSavedPath is not { } path)
        {
            return;
        }

        try
        {
            var arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{Path.GetDirectoryName(path)}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Ordner {Path} konnte nicht geöffnet werden", path);
        }
    }
}
