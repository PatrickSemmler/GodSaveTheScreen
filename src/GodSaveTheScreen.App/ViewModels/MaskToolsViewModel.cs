using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Panel für Bereichsauswahl und Hintergrundradierer: Form, weiche Kante, Toleranz, Pinsel und Maskenbefehle.</summary>
public sealed partial class MaskToolsViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly BackgroundEraserTool? _eraser;

    public MaskToolsViewModel(EditorContext context, Func<ToolBase?> activeTool, BackgroundEraserTool? eraser = null)
    {
        _context = context;
        _activeTool = activeTool;
        _eraser = eraser;
        ShapeChoices =
        [
            new Choice<AreaSelectShape>(Strings.Mask_Rectangle, AreaSelectShape.Rectangle),
            new Choice<AreaSelectShape>(Strings.Mask_Ellipse, AreaSelectShape.Ellipse),
            new Choice<AreaSelectShape>(Strings.Mask_Lasso, AreaSelectShape.Lasso),
            new Choice<AreaSelectShape>(Strings.Mask_MagicWand, AreaSelectShape.MagicWand),
        ];
        _context.AreaSelection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        if (_eraser is not null)
        {
            _eraser.SizeChanged += OnStateChanged;
        }
    }

    public IReadOnlyList<Choice<AreaSelectShape>> ShapeChoices { get; }

    public bool ShowSelect => _activeTool() is AreaSelectTool;

    public bool ShowEraser => _activeTool() is BackgroundEraserTool;

    public bool IsApplicable => ShowSelect || ShowEraser;

    public AreaSelectShape Shape
    {
        get => _context.MaskTool.Shape;
        set => Update(_context.MaskTool with { Shape = value });
    }

    public bool ShowTolerance => Shape == AreaSelectShape.MagicWand;

    public double? Tolerance
    {
        get => _context.MaskTool.Tolerance;
        set => Update(value, v => _context.MaskTool with { Tolerance = Math.Clamp(v, 0, 100) });
    }

    public double? Feather
    {
        get => _context.MaskTool.Feather;
        set => Update(value, v => _context.MaskTool with { Feather = Math.Clamp(v, 0, 100) });
    }

    public double? BrushSize
    {
        get => _context.MaskTool.BrushSize;
        set => Update(value, v => _context.MaskTool with { BrushSize = Math.Clamp(v, MaskBrush.MinSize, MaskBrush.MaxSize) });
    }

    public double? BrushHardness
    {
        get => _context.MaskTool.BrushHardness;
        set => Update(value, v => _context.MaskTool with { BrushHardness = Math.Clamp(v, 0, 100) });
    }

    public bool HasAreaSelection => !_context.AreaSelection.IsEmpty;

    /// <summary>Teile des Screenshots sind entfernt.</summary>
    public bool HasRemovedAreas => !_context.Document.Mask.IsOpaque;

    [RelayCommand(CanExecute = nameof(HasAreaSelection))]
    private void RemoveArea() => MaskCommands.RemoveSelection(_context);

    [RelayCommand]
    private void InvertSelection()
    {
        _context.AreaSelection.Invert();
        _context.InvalidateInteraction();
    }

    [RelayCommand]
    private void SelectAll()
    {
        _context.AreaSelection.SelectAll();
        _context.InvalidateInteraction();
    }

    [RelayCommand(CanExecute = nameof(HasAreaSelection))]
    private void Deselect()
    {
        _context.AreaSelection.Clear();
        _context.InvalidateInteraction();
    }

    [RelayCommand(CanExecute = nameof(HasRemovedAreas))]
    private void ResetMask() => MaskCommands.Reset(_context);

    [RelayCommand(CanExecute = nameof(HasRemovedAreas))]
    private void CropToVisible() => MaskCommands.CropToVisible(_context);

    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
        RemoveAreaCommand.NotifyCanExecuteChanged();
        DeselectCommand.NotifyCanExecuteChanged();
        ResetMaskCommand.NotifyCanExecuteChanged();
        CropToVisibleCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _context.AreaSelection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        if (_eraser is not null)
        {
            _eraser.SizeChanged -= OnStateChanged;
        }
    }

    /// <summary>Leere Eingabe wird ignoriert; Werte sind ganze Zahlen.</summary>
    private void Update(double? value, Func<double, MaskToolStyle> change)
    {
        if (value is { } v && double.IsFinite(v))
        {
            Update(change(Math.Round(v)));
        }
    }

    private void Update(MaskToolStyle style)
    {
        if (style == _context.MaskTool)
        {
            return;
        }

        _context.MaskTool = style;
        OnPropertyChanged(string.Empty);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (IsApplicable)
        {
            Refresh();
        }
    }
}
