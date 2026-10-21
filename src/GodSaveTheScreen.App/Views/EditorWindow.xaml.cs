using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
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
    private readonly IAppShell _shell;
    private MonitorInfo? _targetMonitor;
    private bool _closeConfirmed;
    private Point _dragStart;

    /// <summary>Das eigene Bild wird gerade hinausgezogen (ein Drop auf die eigene Zeichenfläche fügt es nicht als Objekt ein).</summary>
    private bool _draggingOut;

    public EditorWindow(EditorViewModel viewModel, IEditorService editors, IThemeService themes, IAppShell shell)
    {
        _viewModel = viewModel;
        _editors = editors;
        _shell = shell;
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

    /// <summary>Menü „Zuletzt verwendet“ unter dem Öffnen-Knopf; fehlende Dateien sind ausgegraut und lassen sich entfernen.</summary>
    private void OnRecentClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = RecentButton, Placement = PlacementMode.Bottom };
        var items = _viewModel.RecentItems;
        if (items.Count == 0)
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = Strings.Recent_Empty, IsEnabled = false });
        }

        foreach (var file in items)
        {
            var path = file;
            var item = new System.Windows.Controls.MenuItem
            {
                Header = file.Name.Replace("_", "__", StringComparison.Ordinal),
                ToolTip = file.IsMissing ? $"{file.Path} – {Strings.Recent_Missing}" : file.Path,
                IsEnabled = !file.IsMissing,
            };
            item.Click += (_, _) => _viewModel.OpenRecentCommand.Execute(path);
            menu.Items.Add(item);
        }

        if (items.Count > 0)
        {
            menu.Items.Add(new Separator());
            if (items.Any(i => i.IsMissing))
            {
                var remove = new System.Windows.Controls.MenuItem { Header = Strings.Recent_RemoveMissing };
                remove.Click += (_, _) => _viewModel.RemoveMissingRecentsCommand.Execute(null);
                menu.Items.Add(remove);
            }

            var clear = new System.Windows.Controls.MenuItem { Header = Strings.Recent_Clear };
            clear.Click += (_, _) => _viewModel.ClearRecentsCommand.Execute(null);
            menu.Items.Add(clear);
        }

        menu.IsOpen = true;
    }

    private void OnShortcutsClick(object sender, RoutedEventArgs e) => _shell.ShowShortcuts(_viewModel.Tools);

    private void OnHelpClick(object sender, RoutedEventArgs e) => _shell.ShowHelp();

    /// <summary>Abschnitt für Umschalt+F1: Ebenenliste (Fokus darin), sonst ausgewählte Objekte bzw. aktives Werkzeug.</summary>
    private string ContextHelpTopic()
    {
        if (Keyboard.FocusedElement is DependencyObject focused && IsInside<Controls.LayersPanel>(focused))
        {
            return HelpTopics.Layers;
        }

        var selection = _viewModel.Context.Selection.Items;
        return (_viewModel.ActiveTool is SelectTool && selection.Count > 0 ? HelpTopics.ForSelection(selection) : null)
            ?? HelpTopics.ForTool(_viewModel.ActiveTool.Id);
    }

    // ---- Tastatur ----

    /// <summary>Eingabefelder und die Emoji-Auswahl (Pfeiltasten, Enter, Tippen gehören dem Raster) bekommen die Tasten selbst.</summary>
    private static bool IsTyping =>
        Keyboard.FocusedElement is TextBoxBase or System.Windows.Controls.PasswordBox or ComboBox { IsEditable: true }
        || (Keyboard.FocusedElement is DependencyObject focused && IsInside<Controls.EmojiPicker>(focused));

    private static bool IsInside<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var current = element; current is not null; current = current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is T)
            {
                return true;
            }
        }

        return false;
    }

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

        // F1 = Hilfe, Umschalt+F1 = Hilfe zum aktuellen Werkzeug/Objekt, Strg+F1 (oder Strg+?) = Tastenkürzel.
        if (key == Key.F1 && !ctrl)
        {
            _shell.ShowHelp(shift ? ContextHelpTopic() : null);
            e.Handled = true;
            return;
        }

        if (ctrl && key is Key.F1 or Key.OemQuestion)
        {
            _shell.ShowShortcuts(_viewModel.Tools);
            e.Handled = true;
            return;
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

        if (key == Key.Tab && !Canvas.HasSurfaceFocus)
        {
            // Außerhalb der Zeichenfläche bleibt Tab die normale Fokusnavigation.
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
                (shift ? vm.PasteAsImageCommand : vm.PasteCommand).Execute(null);
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

    /// <summary>
    /// Bilder auf der Zeichenfläche werden Bildobjekte an der Drop-Position; Projekte, andere Dateien, ein Drop außerhalb
    /// der Zeichenfläche oder mit gedrückter <c>Strg</c>-Taste öffnen wie bisher ein neues Editorfenster.
    /// </summary>
    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var asObject = !_draggingOut && !_viewModel.IsCropping && (e.KeyStates & DragDropKeyStates.ControlKey) == 0;
        var point = asObject ? Canvas.DropPoint(e) : null;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            if (point is { } at)
            {
                var images = files.Where(ImageInsert.IsInsertable).ToList();
                if (images.Count > 0)
                {
                    _viewModel.InsertImageFiles(images, at);
                    Activate();
                }

                files = [.. files.Except(images)];
            }

            if (files.Length > 0)
            {
                await _editors.OpenFilesAsync(files);
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.Bitmap) && e.Data.GetData(DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            if (point is { } at)
            {
                _viewModel.InsertImage(ImageImport.FromBitmap(bitmap), at);
                Activate();
            }
            else
            {
                _editors.Open(new Core.Imaging.ImageDocument(PixelBufferExtensions.FromBitmapSource(bitmap)) { IsDirty = true });
            }
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
        _draggingOut = true;
        try
        {
            DragDrop.DoDragDrop(DragHandle, data, DragDropEffects.Copy);
        }
        finally
        {
            _draggingOut = false;
        }
    }
}
