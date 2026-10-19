using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Controls;

/// <summary>
/// Zeichenfläche: Scrollen/Zoomen/Schwenken, Ebenen (Bild, Objekte, Interaktion) und Weiterleitung der Mausereignisse
/// in Bildpixeln an das aktive Werkzeug. Die Umrechnung Bildschirm → Bildpixel erledigt <see cref="ToImagePoint"/>.
/// </summary>
public partial class EditorCanvas : UserControl
{
    private const double SurfaceMargin = 24;
    private const double CheckerSize = 8;

    private EditorViewModel? _viewModel;
    private bool _panning;
    private Point _panStart;
    private double _panHorizontal;
    private double _panVertical;
    private bool _toolCaptured;
    private object? _lastHidden;
    private TextBox? _textEditor;
    private bool _committingText;
    private bool _rulerDrag;
    private VectorD _rulerGrab;

    public EditorCanvas() => InitializeComponent();

    /// <summary>Bildschirmposition → Bildpixel (die Layout-Transformation und das Scrollen sind eingerechnet).</summary>
    public PointD ToImagePoint(MouseEventArgs e)
    {
        var p = e.GetPosition(Surface);
        return new PointD(p.X, p.Y);
    }

    public void Initialize(EditorViewModel viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel.Context.InteractionInvalidated -= OnInteractionInvalidated;
            _viewModel.Context.TextEditStarted -= OnTextEditStarted;
            _viewModel.Context.TextEditEnded -= OnTextEditEnded;
        }

        _viewModel = viewModel;
        viewModel.Context.TextEditStarted += OnTextEditStarted;
        viewModel.Context.TextEditEnded += OnTextEditEnded;
        var document = viewModel.Document;
        var source = document.BaseImage.ToBitmapSource();
        BaseImage.Source = source;
        Surface.Width = document.BaseImage.Width;
        Surface.Height = document.BaseImage.Height;
        Checker.Fill = CreateCheckerBrush();

        var renderContext = new RenderContext
        {
            BaseImage = source,
            BasePixels = document.BaseImage,
            Annotations = document.Annotations,
            HighlightersBehind = document.HighlightersBehind,
        };
        Annotations.Attach(document, viewModel.Context.Interaction, renderContext);
        Interaction.RenderContext = renderContext;
        Interaction.Attach(viewModel.Context);
        viewModel.Context.VisibleCenterProvider = VisibleCenter;
        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Context.InteractionInvalidated += OnInteractionInvalidated;
        ApplyZoom();
        UpdateCursor();
    }

    public void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel.Context.InteractionInvalidated -= OnInteractionInvalidated;
            _viewModel.Context.TextEditStarted -= OnTextEditStarted;
            _viewModel.Context.TextEditEnded -= OnTextEditEnded;
            _viewModel.Context.CancelTextEdit();
            _viewModel = null;
        }

        Annotations.Detach();
        Interaction.Detach();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyZoom();
    }

    private static DrawingBrush CreateCheckerBrush()
    {
        var light = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var dark = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 2, 2))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(1, 1, 1, 1))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, CheckerSize * 2, CheckerSize * 2),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        return brush;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorViewModel.Zoom):
            case nameof(EditorViewModel.IsFit):
                ApplyZoom();
                break;
            case nameof(EditorViewModel.ActiveTool):
                UpdateCursor();
                break;
        }
    }

    private void OnInteractionInvalidated(object? sender, EventArgs e)
    {
        var hidden = _viewModel?.Context.Interaction.Hidden;
        if (!ReferenceEquals(hidden, _lastHidden))
        {
            _lastHidden = hidden;
            Annotations.InvalidateAll();
        }
    }

    private double DpiScale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void ApplyZoom()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.IsFit)
        {
            Scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            var fit = ComputeFit();
            if (Math.Abs(fit - _viewModel.Zoom) > 1e-6)
            {
                _viewModel.SetZoom(fit, keepFit: true);
                return;
            }
        }
        else
        {
            Scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        var scale = _viewModel.Zoom / DpiScale;
        ZoomTransform.ScaleX = scale;
        ZoomTransform.ScaleY = scale;
        _viewModel.Context.Scale = scale;
        BaseImage.SetValue(RenderOptions.BitmapScalingModeProperty, _viewModel.Zoom >= 2 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        if (Checker.Fill is DrawingBrush brush)
        {
            // Konstante Kästchengröße auf dem Bildschirm.
            var size = CheckerSize / Math.Max(scale, 0.01);
            brush.Viewport = new Rect(0, 0, size * 2, size * 2);
        }

        Interaction.InvalidateVisual();
    }

    private double ComputeFit()
    {
        if (_viewModel is null || Scroller.ActualWidth <= 0 || Scroller.ActualHeight <= 0)
        {
            return _viewModel?.Zoom ?? 1;
        }

        var image = _viewModel.Document.BaseImage;
        var dpi = DpiScale;
        var availableWidth = Math.Max(1, Scroller.ActualWidth - (2 * SurfaceMargin)) * dpi;
        var availableHeight = Math.Max(1, Scroller.ActualHeight - (2 * SurfaceMargin)) * dpi;
        var fit = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
        return Math.Clamp(Math.Min(fit, 1), EditorViewModel.MinZoom, EditorViewModel.MaxZoom);
    }

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewModel is { IsFit: true })
        {
            ApplyZoom();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel is not null && IsOnRuler(new PointD(e.GetPosition(Surface).X, e.GetPosition(Surface).Y)))
        {
            var modifiers = Keyboard.Modifiers;
            var step = (modifiers & ModifierKeys.Shift) != 0 ? 15 : (modifiers & ModifierKeys.Control) != 0 ? 0.1 : 1;
            _viewModel.Context.Ruler.Rotate(Math.Sign(e.Delta) * step);
            e.Handled = true;
            return;
        }

        if (_viewModel is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        e.Handled = true;
        var anchorImage = e.GetPosition(Surface);
        var anchorViewport = e.GetPosition(Scroller);
        var zoom = _viewModel.Zoom * Math.Pow(1.1, e.Delta / 120.0);
        _viewModel.SetZoom(zoom);
        UpdateLayout();

        // Der Bildpunkt unter dem Mauszeiger bleibt an derselben Stelle.
        var now = Surface.TranslatePoint(anchorImage, Scroller);
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + (now.X - anchorViewport.X));
        Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + (now.Y - anchorViewport.Y));
    }

    private EditorPointerArgs Args(MouseEventArgs e, int clickCount = 1, MouseButton button = MouseButton.Left)
    {
        var pressure = 1.0;
        if (e.StylusDevice is { } stylus)
        {
            var points = stylus.GetStylusPoints(Surface);
            if (points.Count > 0)
            {
                pressure = points[^1].PressureFactor;
            }
        }

        return new EditorPointerArgs(ToImagePoint(e), Keyboard.Modifiers, pressure, clickCount, button);
    }

    /// <summary>Aktualisiert den Mauszeiger (z. B. Hand, solange die Leertaste gedrückt ist).</summary>
    public void RefreshCursor() => UpdateCursor();

    private void UpdateCursor()
    {
        if (_viewModel is null)
        {
            return;
        }

        Surface.Cursor = _panning || Keyboard.IsKeyDown(Key.Space) ? Cursors.Hand : _viewModel.ActiveTool.Cursor;
    }

    private void BeginPan(MouseEventArgs e)
    {
        _panning = true;
        _panStart = e.GetPosition(Scroller);
        _panHorizontal = Scroller.HorizontalOffset;
        _panVertical = Scroller.VerticalOffset;
        Surface.CaptureMouse();
        UpdateCursor();
    }

    private void EndPan()
    {
        _panning = false;
        Surface.ReleaseMouseCapture();
        UpdateCursor();
    }

    private bool IsFromOverlay(object? source)
    {
        for (var node = source as DependencyObject; node is not null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, Overlay))
            {
                return true;
            }
        }

        return false;
    }

    // ---- Textbearbeitung direkt auf der Zeichenflaeche ----

    private void OnTextEditStarted(object? sender, TextEditSession session)
    {
        RemoveTextEditor();
        var annotation = session.Annotation;
        var pad = annotation.EffectivePadding;
        var scale = Math.Max(_viewModel?.Context.Scale ?? 1, 0.01);
        var fill = BrushCache.GetBrush(annotation.Fill) ?? Brushes.Black;
        var editor = new TextBox
        {
            Style = null,
            Text = annotation.Text,
            AcceptsReturn = true,
            FontFamily = new FontFamily(annotation.FontFamilyName),
            FontSize = annotation.FontSize,
            FontWeight = FontWeight.FromOpenTypeWeight(Math.Clamp(annotation.FontWeight, 1, 999)),
            FontStyle = annotation.FontStyle switch
            {
                FontStyleKind.Italic => FontStyles.Italic,
                FontStyleKind.Oblique => FontStyles.Oblique,
                _ => FontStyles.Normal,
            },
            FontStretch = FontStretch.FromOpenTypeStretch(Math.Clamp(annotation.FontStretch, 1, 9)),
            Foreground = fill,
            CaretBrush = fill,
            SelectionBrush = Brushes.DodgerBlue,
            Background = annotation.HasBackground ? BrushCache.GetBrush(annotation.Background) : Brushes.Transparent,
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(1 / scale),
            Padding = new Thickness(pad),
            MinWidth = Math.Max(annotation.FontSize, 24),
            TextWrapping = annotation.WrapWidth is null ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextAlignment = annotation.TextAlignment switch
            {
                TextAlignmentKind.Center => TextAlignment.Center,
                TextAlignmentKind.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FocusVisualStyle = null,
            SnapsToDevicePixels = false,
        };
        if (annotation.WrapWidth is { } wrap)
        {
            editor.Width = wrap + (2 * pad) + (2 / scale);
        }

        System.Windows.Controls.Canvas.SetLeft(editor, annotation.Position.X - pad - (1 / scale));
        System.Windows.Controls.Canvas.SetTop(editor, annotation.Position.Y - pad - (1 / scale));
        editor.LostKeyboardFocus += (_, _) => CommitTextEditor();
        editor.KeyDown += OnTextEditorKeyDown;
        _textEditor = editor;
        Overlay.Children.Add(editor);
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            () =>
            {
                editor.Focus();
                Keyboard.Focus(editor);
                editor.CaretIndex = editor.Text.Length;
            });
    }

    private void OnTextEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0))
        {
            e.Handled = true;
            CommitTextEditor();
            Surface.Focus();
        }
    }

    private void CommitTextEditor()
    {
        if (_textEditor is null || _committingText || _viewModel is null)
        {
            return;
        }

        _committingText = true;
        try
        {
            _viewModel.Context.CommitTextEdit(_textEditor.Text);
        }
        finally
        {
            _committingText = false;
        }
    }

    private void OnTextEditEnded(object? sender, EventArgs e) => RemoveTextEditor();

    private void RemoveTextEditor()
    {
        if (_textEditor is null)
        {
            return;
        }

        var editor = _textEditor;
        _textEditor = null;
        Overlay.Children.Remove(editor);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || IsFromOverlay(e.OriginalSource))
        {
            return;
        }

        Surface.Focus();
        if (Keyboard.IsKeyDown(Key.Space))
        {
            BeginPan(e);
            e.Handled = true;
            return;
        }

        if (TryBeginRulerDrag(e))
        {
            e.Handled = true;
            return;
        }

        Surface.CaptureMouse();
        _toolCaptured = true;
        _viewModel.ActiveTool.OnPointerDown(Args(e, e.ClickCount));
        UpdateCursor();
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            BeginPan(e);
            e.Handled = true;
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _panning)
        {
            EndPan();
            e.Handled = true;
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || IsFromOverlay(e.OriginalSource))
        {
            return;
        }

        Surface.Focus();
        if (IsOnRuler(ToImagePoint(e)))
        {
            return;
        }

        _viewModel.SelectAt(ToImagePoint(e));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null || IsFromOverlay(e.OriginalSource))
        {
            return;
        }

        if (_panning)
        {
            var current = e.GetPosition(Scroller);
            Scroller.ScrollToHorizontalOffset(_panHorizontal - (current.X - _panStart.X));
            Scroller.ScrollToVerticalOffset(_panVertical - (current.Y - _panStart.Y));
            return;
        }

        if (_rulerDrag)
        {
            var position = ToImagePoint(e);
            _viewModel.Context.Ruler.Center = new PointD(position.X + _rulerGrab.X, position.Y + _rulerGrab.Y);
            return;
        }

        var args = Args(e);
        _viewModel.SetPointer(args.Position);
        _viewModel.ActiveTool.OnPointerMove(args);
        UpdateCursor();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || IsFromOverlay(e.OriginalSource))
        {
            return;
        }

        if (_panning)
        {
            EndPan();
            e.Handled = true;
            return;
        }

        if (_rulerDrag)
        {
            _rulerDrag = false;
            Surface.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_toolCaptured)
        {
            _toolCaptured = false;
            Surface.ReleaseMouseCapture();
            _viewModel.ActiveTool.OnPointerUp(Args(e));
            UpdateCursor();
            e.Handled = true;
        }
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        // Capture ging verloren (z. B. Alt+Tab): laufende Aktion abbrechen.
        if (_toolCaptured && _viewModel is not null)
        {
            _toolCaptured = false;
            _viewModel.ActiveTool.Cancel();
        }

        _rulerDrag = false;
        _panning = false;
    }

    private bool IsOnRuler(PointD position)
    {
        var context = _viewModel?.Context;
        return context is { Ruler.IsVisible: true } && RulerGeometry.Contains(position, context.Ruler.Placement(context.Scale));
    }

    /// <summary>Ziehen am Lineal verschiebt es – außer ein Freihandwerkzeug beginnt nahe der Kante (dort rastet der Strich ein).</summary>
    private bool TryBeginRulerDrag(MouseEventArgs e)
    {
        var context = _viewModel!.Context;
        var position = ToImagePoint(e);
        if (!IsOnRuler(position))
        {
            return false;
        }

        var placement = context.Ruler.Placement(context.Scale);
        if (_viewModel.ActiveTool is FreehandToolBase && RulerGeometry.NearestEdge(position, placement, context.ToImageUnits(RulerViewModel.SnapDistanceDip)) is not null)
        {
            return false;
        }

        _rulerDrag = true;
        _rulerGrab = context.Ruler.Center - position;
        Surface.CaptureMouse();
        return true;
    }

    /// <summary>Mitte des sichtbaren Bildausschnitts in Bildpixeln.</summary>
    private PointD VisibleCenter()
    {
        var image = _viewModel!.Document.BaseImage;
        var viewportCenter = new Point(Scroller.ViewportWidth / 2, Scroller.ViewportHeight / 2);
        var point = Scroller.TranslatePoint(viewportCenter, Surface);
        return new PointD(Math.Clamp(point.X, 0, image.Width), Math.Clamp(point.Y, 0, image.Height));
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var p = Mouse.GetPosition(Surface);
        if (!IsOnRuler(new PointD(p.X, p.Y)))
        {
            return;
        }

        e.Handled = true;
        var ruler = _viewModel.Context.Ruler;
        var menu = new ContextMenu { PlacementTarget = Surface };
        foreach (var angle in new[] { 0, 45, 90, 135 })
        {
            var value = angle;
            var item = new MenuItem { Header = string.Format(System.Globalization.CultureInfo.CurrentUICulture, Strings.Ruler_SetAngle, value) };
            item.Click += (_, _) => ruler.Angle = value;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var hide = new MenuItem { Header = Strings.Ruler_Hide };
        hide.Click += (_, _) => ruler.IsVisible = false;
        menu.Items.Add(hide);
        menu.IsOpen = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e) => _viewModel?.SetPointer(null);
}
