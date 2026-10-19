using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Capture;
using CaptureMode = GodSaveTheScreen.Core.Capture.CaptureMode;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace GodSaveTheScreen.App.Capture;

/// <summary>
/// Randloses Auswahl-Overlay für genau einen Monitor. Der eingefrorene Desktop ist der Hintergrund;
/// alle Overlays teilen sich den <see cref="SelectionState"/> in virtuellen Pixelkoordinaten.
/// </summary>
public partial class SelectionOverlayWindow : Window
{
    private const int MagnifierPixels = 15;
    private const int MagnifierZoom = 8;
    private const double MagnifierWidth = 138;
    private const double MagnifierHeight = 168;

    private readonly MonitorInfo _monitor;
    private readonly BitmapSource _snapshotBitmap;
    private readonly ScreenSnapshot _snapshot;
    private readonly SelectionOverlayViewModel _viewModel;
    private readonly bool _showMagnifier;
    private CoordinateMapper _mapper;

    public SelectionOverlayWindow(
        MonitorInfo monitor,
        BitmapSource snapshotBitmap,
        ScreenSnapshot snapshot,
        SelectionOverlayViewModel viewModel,
        bool showMagnifier = true)
    {
        _showMagnifier = showMagnifier;
        _monitor = monitor;
        _snapshotBitmap = snapshotBitmap;
        _snapshot = snapshot;
        _viewModel = viewModel;
        _mapper = new CoordinateMapper(monitor.Bounds, monitor.Scale);

        InitializeComponent();

        var local = monitor.Bounds.Offset(-snapshot.VirtualLeft, -snapshot.VirtualTop);
        BackgroundImage.Source = new CroppedBitmap(snapshotBitmap, new Int32Rect(local.X, local.Y, local.Width, local.Height));

        HintText.Text = viewModel.Mode switch
        {
            CaptureMode.Window => Strings.Overlay_HintWindow,
            CaptureMode.Freeform => Strings.Overlay_HintFreeform,
            _ => Strings.Overlay_Hint,
        };

        _viewModel.Changed += Refresh;
        Closed += (_, _) => _viewModel.Changed -= Refresh;
        Loaded += (_, _) => UpdateScale();

        MouseLeftButtonDown += OnLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnLeftButtonUp;
        MouseRightButtonDown += (_, e) =>
        {
            _viewModel.Cancel();
            e.Handled = true;
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public MonitorInfo Monitor => _monitor;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyPhysicalBounds();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // WPF übernimmt beim Monitorwechsel die vorgeschlagene Größe; hier wird sie wieder exakt gesetzt.
        ApplyPhysicalBounds();
        UpdateScale();
    }

    private void ApplyPhysicalBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var b = _monitor.Bounds;
        PInvoke.SetWindowPos(
            new HWND(hwnd),
            HWND.HWND_TOPMOST,
            b.X,
            b.Y,
            b.Width,
            b.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }

    private void UpdateScale()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        _mapper = new CoordinateMapper(_monitor.Bounds, dpi.DpiScaleX);

        var size = _mapper.LocalSizeDips;
        Canvas.SetLeft(BackgroundImage, 0);
        Canvas.SetTop(BackgroundImage, 0);
        BackgroundImage.Width = size.Width;
        BackgroundImage.Height = size.Height;
        Refresh();
    }

    private PointD ToVirtual(Point dips) => _mapper.LocalDipsToVirtual(new PointD(dips.X, dips.Y));

    private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        _viewModel.PointerDown(ToVirtual(e.GetPosition(Root)));
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e) =>
        _viewModel.PointerMove(ToVirtual(e.GetPosition(Root)));

    private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        _viewModel.PointerUp();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.Cancel();
                break;
            case Key.Enter:
                _viewModel.Confirm();
                break;
            case Key.Left:
                _viewModel.Nudge(-step, 0);
                break;
            case Key.Right:
                _viewModel.Nudge(step, 0);
                break;
            case Key.Up:
                _viewModel.Nudge(0, -step);
                break;
            case Key.Down:
                _viewModel.Nudge(0, step);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private static readonly Brush WindowBorderBrush = CreateFrozenBrush(0xFF, 0x3A, 0x96, 0xDD);

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static PathGeometry BuildPolygon(List<PointD> points)
    {
        var geometry = new PathGeometry { FillRule = FillRule.EvenOdd };
        if (points.Count > 1)
        {
            var figure = new PathFigure { StartPoint = new Point(points[0].X, points[0].Y), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new PolyLineSegment(points.Skip(1).Select(p => new Point(p.X, p.Y)), true));
            geometry.Figures.Add(figure);
        }

        return geometry;
    }

    private void Refresh()
    {
        var size = _mapper.LocalSizeDips;
        var state = _viewModel.State;
        var full = new RectangleGeometry(new Rect(0, 0, size.Width, size.Height));

        var highlight = _viewModel.HighlightBounds;
        var showHighlight = !highlight.IsEmpty;
        var freeform = _viewModel.IsFreeform && _viewModel.Path.HasPath;
        var windowMode = _viewModel.Mode == CaptureMode.Window && !_viewModel.IsRectangleSelection;

        FreeformShape.Visibility = freeform ? Visibility.Visible : Visibility.Collapsed;
        if (freeform)
        {
            var points = _viewModel.Path.Points
                .Select(p => _mapper.VirtualToLocalDips(new PointD(p.X + 0.5, p.Y + 0.5)))
                .ToList();
            FreeformShape.Points = new PointCollection(points.Select(p => new Point(p.X, p.Y)));
            Dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, BuildPolygon(points));
        }
        else if (showHighlight)
        {
            var r = _mapper.VirtualToLocalDips(highlight.ToRectD());
            var hole = new RectangleGeometry(new Rect(r.X, r.Y, r.Width, r.Height));
            Dim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
        }
        else
        {
            Dim.Data = full;
        }

        if (showHighlight && !freeform)
        {
            var r = _mapper.VirtualToLocalDips(highlight.ToRectD());
            SelectionBorder.Visibility = Visibility.Visible;
            Canvas.SetLeft(SelectionBorder, r.X - 0.5);
            Canvas.SetTop(SelectionBorder, r.Y - 0.5);
            SelectionBorder.Width = r.Width + 1;
            SelectionBorder.Height = r.Height + 1;
            SelectionBorder.Stroke = windowMode ? WindowBorderBrush : Brushes.White;
            SelectionBorder.StrokeThickness = windowMode ? 3 : 1;
        }
        else
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
        }

        if (showHighlight)
        {
            PositionSizeLabel(_mapper.VirtualToLocalDips(highlight.ToRectD()), size);
        }
        else
        {
            SizeLabel.Visibility = Visibility.Collapsed;
        }

        UpdateWindowBadge(windowMode && showHighlight ? highlight : PixelRect.Empty, size);

        Hint.Visibility = state.HasSelection || showHighlight || freeform ? Visibility.Collapsed : Visibility.Visible;
        Hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(Hint, Math.Max(0, (size.Width - Hint.DesiredSize.Width) / 2));
        Canvas.SetTop(Hint, 24);

        // Fadenkreuz + Lupe nur auf dem Monitor, auf dem der Mauszeiger steht
        var cursor = state.Cursor;
        var cursorHere = _monitor.Bounds.Contains((int)cursor.X, (int)cursor.Y);
        var crosshair = cursorHere && !windowMode ? Visibility.Visible : Visibility.Collapsed;
        CrossHorizontal.Visibility = crosshair;
        CrossVertical.Visibility = crosshair;
        Magnifier.Visibility = cursorHere && _showMagnifier ? Visibility.Visible : Visibility.Collapsed;
        if (!cursorHere)
        {
            return;
        }

        var c = _mapper.VirtualToLocalDips(new PointD(cursor.X + 0.5, cursor.Y + 0.5));
        CrossHorizontal.X1 = 0;
        CrossHorizontal.X2 = size.Width;
        CrossHorizontal.Y1 = c.Y;
        CrossHorizontal.Y2 = c.Y;
        CrossVertical.X1 = c.X;
        CrossVertical.X2 = c.X;
        CrossVertical.Y1 = 0;
        CrossVertical.Y2 = size.Height;

        if (_showMagnifier)
        {
            UpdateMagnifier(cursor, c, size);
        }
    }

    private void UpdateWindowBadge(PixelRect windowBounds, SizeD monitorSize)
    {
        var window = _viewModel.HoveredWindow;
        var cursor = _viewModel.State.Cursor;
        if (windowBounds.IsEmpty || window is null || !_monitor.Bounds.Contains((int)cursor.X, (int)cursor.Y))
        {
            WindowBadge.Visibility = Visibility.Collapsed;
            return;
        }

        WindowBadgeText.Text = string.IsNullOrEmpty(window.Title) ? window.ProcessName : window.Title;
        WindowBadge.Visibility = Visibility.Visible;
        WindowBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var badge = WindowBadge.DesiredSize;
        var r = _mapper.VirtualToLocalDips(windowBounds.ToRectD());
        var x = Math.Clamp(r.X, 0, Math.Max(0, monitorSize.Width - badge.Width));
        var y = r.Y - badge.Height - 4 >= 0 ? r.Y - badge.Height - 4 : r.Y + 4;
        Canvas.SetLeft(WindowBadge, x);
        Canvas.SetTop(WindowBadge, y);
    }

    private void PositionSizeLabel(RectD selection, SizeD monitorSize)
    {
        SizeText.Text = _viewModel.SizeText;
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var label = SizeLabel.DesiredSize;

        var x = Math.Clamp(selection.Right - label.Width, 0, Math.Max(0, monitorSize.Width - label.Width));
        var y = selection.Bottom + 6;
        if (y + label.Height > monitorSize.Height)
        {
            y = Math.Max(0, selection.Y - label.Height - 6);
        }

        Canvas.SetLeft(SizeLabel, x);
        Canvas.SetTop(SizeLabel, y);
    }

    private void UpdateMagnifier(PointD cursorVirtual, PointD cursorLocal, SizeD monitorSize)
    {
        var image = _snapshot.Image;
        var snap = CoordinateMapper.VirtualToSnapshot(cursorVirtual, _snapshot.VirtualLeft, _snapshot.VirtualTop);
        var px = Math.Clamp((int)snap.X, 0, image.Width - 1);
        var py = Math.Clamp((int)snap.Y, 0, image.Height - 1);

        var half = MagnifierPixels / 2;
        var x0 = Math.Clamp(px - half, 0, Math.Max(0, image.Width - MagnifierPixels));
        var y0 = Math.Clamp(py - half, 0, Math.Max(0, image.Height - MagnifierPixels));
        var w = Math.Min(MagnifierPixels, image.Width);
        var h = Math.Min(MagnifierPixels, image.Height);
        MagnifierImage.Source = new CroppedBitmap(_snapshotBitmap, new Int32Rect(x0, y0, w, h));
        Canvas.SetLeft(MagnifierMarker, (px - x0) * MagnifierZoom);
        Canvas.SetTop(MagnifierMarker, (py - y0) * MagnifierZoom);

        ColorText.Text = ColorHex(image, px, py);

        var left = cursorLocal.X + 24;
        var top = cursorLocal.Y + 24;
        if (left + MagnifierWidth > monitorSize.Width)
        {
            left = cursorLocal.X - 24 - MagnifierWidth;
        }

        if (top + MagnifierHeight > monitorSize.Height)
        {
            top = cursorLocal.Y - 24 - MagnifierHeight;
        }

        Canvas.SetLeft(Magnifier, left);
        Canvas.SetTop(Magnifier, top);
    }

    internal static string ColorHex(PixelBuffer image, int x, int y)
    {
        var i = ((y * image.Width) + x) * PixelBuffer.BytesPerPixel;
        return $"#{image.Pixels[i + 2]:X2}{image.Pixels[i + 1]:X2}{image.Pixels[i]:X2}";
    }
}
