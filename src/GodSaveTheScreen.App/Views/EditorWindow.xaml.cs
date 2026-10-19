using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GodSaveTheScreen.App.Services;
using GodSaveTheScreen.App.ViewModels;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Services;
using GodSaveTheScreen.Rendering;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Views;

public partial class EditorWindow : FluentWindow
{
    private readonly EditorViewModel _viewModel;

    public EditorWindow(
        ImageDocument document,
        IDialogService dialogs,
        IClipboardService clipboard,
        ILoggerFactory loggerFactory,
        IThemeService themes)
    {
        var viewModel = new EditorViewModel(document, dialogs, clipboard, loggerFactory.CreateLogger<EditorViewModel>());
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        themes.Attach(this);

        Preview.Source = viewModel.Document.BaseImage.ToBitmapSource();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Loaded += (_, _) => UpdateZoom();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateZoom();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.ZoomMode))
        {
            UpdateZoom();
        }
    }

    private void UpdateZoom()
    {
        var image = _viewModel.Document.BaseImage;
        if (_viewModel.ZoomMode == ZoomMode.Fit)
        {
            Scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Preview.Width = double.NaN;
            Preview.Height = double.NaN;
            Preview.Stretch = Stretch.Uniform;
            Preview.StretchDirection = StretchDirection.DownOnly;
        }
        else
        {
            // 100 %: ein Bildpixel pro physischem Bildschirmpixel.
            var dpi = VisualTreeHelper.GetDpi(this);
            Scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            Preview.Stretch = Stretch.Fill;
            Preview.StretchDirection = StretchDirection.Both;
            Preview.Width = image.Width / dpi.DpiScaleX;
            Preview.Height = image.Height / dpi.DpiScaleY;
        }
    }
}
