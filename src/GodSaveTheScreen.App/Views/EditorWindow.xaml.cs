using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Capture;
using GodSaveTheScreen.Rendering;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class EditorWindow : FluentWindow
{
    // Platz neben dem Bild: Werkzeugleiste + Eigenschaftenpanel + Ränder; Kopfbereich: Titel + Befehlsleiste + Statuszeile.
    private const double SideChrome = 56 + 316 + 48;
    private const double VerticalChrome = 150;
    private const double WorkAreaFraction = 0.85;

    private readonly EditorViewModel _viewModel;
    private readonly IEditorService _editors;
    private MonitorInfo? _targetMonitor;
    private bool _closeConfirmed;
    private Point _dragStart;

    public EditorWindow(EditorViewModel viewModel, IEditorService editors, IThemeService themes)
    {
        _viewModel = viewModel;
        _editors = editors;
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);
        Canvas.Initialize(viewModel);
        Closed += (_, _) => Canvas.Detach();
    }

    /// <summary>Wählt Größe (Bild bei 100 % + Ränder, höchstens 85 % des Arbeitsbereichs) und Position auf dem Monitor.</summary>
    public void PlaceOn(MonitorInfo monitor)
    {
        _targetMonitor = monitor;
        var scale = monitor.Scale;
        var image = _viewModel.Document.EffectiveBounds;
        var maxWidth = monitor.WorkArea.Width / scale * WorkAreaFraction;
        var maxHeight = monitor.WorkArea.Height / scale * WorkAreaFraction;
        Width = Math.Clamp((image.Width / scale) + SideChrome, MinWidth, Math.Max(MinWidth, maxWidth));
        Height = Math.Clamp((image.Height / scale) + VerticalChrome, MinHeight, Math.Max(MinHeight, maxHeight));
        WindowStartupLocation = WindowStartupLocation.Manual;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_targetMonitor is not { } monitor)
        {
            return;
        }

        var scale = monitor.Scale;
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var x = monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2);
        var y = monitor.WorkArea.Y + ((monitor.WorkArea.Height - height) / 2);
        PInvoke.SetWindowPos(
            new HWND(new WindowInteropHelper(this).Handle),
            HWND.Null,
            x,
            y,
            width,
            height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || _closeConfirmed || !_viewModel.Document.IsDirty || Application.Current?.Dispatcher.HasShutdownStarted == true)
        {
            return;
        }

        // Die Rückfrage ist asynchron (Speichern): Schließen vorerst verhindern und danach erneut auslösen.
        e.Cancel = true;
        _ = ConfirmAndCloseAsync();
    }

    private async Task ConfirmAndCloseAsync()
    {
        // Erst nach Rückkehr aus OnClosing fragen: ein modaler Dialog mit diesem Fenster als Besitzer
        // darf nicht erscheinen, solange das Fenster noch im Schließvorgang steckt.
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (await _viewModel.ConfirmCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    // ---- Werkzeuge, Zoom ----

    private void OnToolSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ToolList.SelectedItem is ToolBase tool)
        {
            _viewModel.Activate(tool);
        }
    }

    private void OnZoomSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ZoomBox.SelectedItem is ZoomChoice choice)
        {
            _viewModel.SelectZoomChoiceCommand.Execute(choice);
            ZoomBox.SelectedItem = null;
        }
    }

    private void OnNewClick(object sender, RoutedEventArgs e)
    {
        if (NewButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = NewButton;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    // ---- Tastatur ----

    private static bool IsTyping => Keyboard.FocusedElement is TextBoxBase or System.Windows.Controls.PasswordBox or ComboBox { IsEditable: true };

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            Canvas.RefreshCursor();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        var typing = IsTyping;
        if (key == Key.Space && !typing)
        {
            Canvas.RefreshCursor();
        }

        if (ctrl && TryGlobalShortcut(key, shift, typing))
        {
            e.Handled = true;
            return;
        }

        if (typing)
        {
            return;
        }

        if ((modifiers & ~ModifierKeys.Shift) == 0 && !shift && _viewModel.Tools.FindByKey(key) is { } tool && _viewModel.ActiveTool is not PolygonTool && !_viewModel.IsCropping)
        {
            _viewModel.Activate(tool);
            e.Handled = true;
            return;
        }

        if (_viewModel.ActiveTool.OnKeyDown(key, modifiers))
        {
            e.Handled = true;
        }
    }

    private bool TryGlobalShortcut(Key key, bool shift, bool typing)
    {
        var vm = _viewModel;
        switch (key)
        {
            case Key.S:
                (shift ? vm.SaveAsCommand : vm.SaveCommand).Execute(null);
                return true;
            case Key.O:
                vm.OpenCommand.Execute(null);
                return true;
        }

        if (typing)
        {
            return false;
        }

        switch (key)
        {
            case Key.Z:
                (shift ? vm.RedoCommand : vm.UndoCommand).Execute(null);
                return true;
            case Key.Y:
                vm.RedoCommand.Execute(null);
                return true;
            case Key.C:
                (shift ? vm.CopyImageCommand : vm.CopyCommand).Execute(null);
                return true;
            case Key.V:
                vm.PasteCommand.Execute(null);
                return true;
            case Key.X:
                vm.CutCommand.Execute(null);
                return true;
            case Key.D:
                vm.DuplicateCommand.Execute(null);
                return true;
            case Key.R:
                vm.ToggleRulerCommand.Execute(null);
                return true;
            case Key.D0 or Key.NumPad0:
                vm.FitCommand.Execute(null);
                return true;
            case Key.D1 or Key.NumPad1:
                vm.ActualSizeCommand.Execute(null);
                return true;
            case Key.OemPlus or Key.Add:
                vm.ZoomInCommand.Execute(null);
                return true;
            case Key.OemMinus or Key.Subtract:
                vm.ZoomOutCommand.Execute(null);
                return true;
            case Key.OemCloseBrackets:
                (shift ? vm.BringToFrontCommand : vm.BringForwardCommand).Execute(null);
                return true;
            case Key.OemOpenBrackets:
                (shift ? vm.SendToBackCommand : vm.SendBackwardCommand).Execute(null);
                return true;
            default:
                return false;
        }
    }

    // ---- Drag & Drop ----

    private static bool HasDroppableData(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.Bitmap);

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasDroppableData(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            await _editors.OpenFilesAsync(files);
        }
        else if (e.Data.GetDataPresent(DataFormats.Bitmap) && e.Data.GetData(DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            _editors.Open(new Core.Imaging.ImageDocument(PixelBufferExtensions.FromBitmapSource(bitmap)) { IsDirty = true });
        }
    }

    private void OnDragHandleDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);

    private void OnDragHandleMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (_viewModel.CreateDragPayload() is not { } payload)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { payload.TempFile });
        data.SetData(DataFormats.Bitmap, payload.Image.ToBitmapSource());
        DragDrop.DoDragDrop(DragHandle, data, DragDropEffects.Copy);
    }
}
