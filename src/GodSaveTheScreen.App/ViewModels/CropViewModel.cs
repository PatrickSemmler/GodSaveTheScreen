using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Eintrag der Seitenverhältnis-Auswahl.</summary>
public sealed record CropAspectOption(string Label, CropAspectKind Kind);

/// <summary>Eigenschaftenpanel des Zuschnittmodus: Seitenverhältnis, Pixelmaße, Übernehmen/Abbrechen/Zurücksetzen.</summary>
public sealed partial class CropViewModel : ObservableObject, IDisposable
{
    /// <summary>Rand um die Objekte bei „Auf Objekte zuschneiden“ in Pixeln.</summary>
    public const int ObjectMargin = 12;

    private readonly EditorContext _context;
    private readonly Func<CropTool> _tool;

    public CropViewModel(EditorContext context, Func<CropTool> tool)
    {
        _context = context;
        _tool = tool;
        AspectOptions =
        [
            new CropAspectOption(Strings.Crop_Free, CropAspectKind.Free),
            new CropAspectOption(Strings.Crop_Original, CropAspectKind.Original),
            new CropAspectOption("1:1", CropAspectKind.Square),
            new CropAspectOption("4:3", CropAspectKind.Ratio4x3),
            new CropAspectOption("3:2", CropAspectKind.Ratio3x2),
            new CropAspectOption("16:9", CropAspectKind.Ratio16x9),
            new CropAspectOption("9:16", CropAspectKind.Ratio9x16),
            new CropAspectOption(Strings.Crop_Custom, CropAspectKind.Custom),
        ];
        _selectedAspect = AspectOptions[0];
        _context.Crop.Changed += OnCropChanged;
    }

    public IReadOnlyList<CropAspectOption> AspectOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomAspect))]
    private CropAspectOption _selectedAspect;

    /// <summary>Kurzer Hinweis (z. B. kein Rand gefunden).</summary>
    [ObservableProperty]
    private string? _hint;

    public bool IsCustomAspect => SelectedAspect.Kind == CropAspectKind.Custom;

    public double? X
    {
        get => Rect.X;
        set => SetFromInput(value, v => CropGeometry.Move(Rect, v - Rect.X, 0, Bounds));
    }

    public double? Y
    {
        get => Rect.Y;
        set => SetFromInput(value, v => CropGeometry.Move(Rect, 0, v - Rect.Y, Bounds));
    }

    public double? Width
    {
        get => Rect.Width;
        set => SetFromInput(value, v => CropGeometry.Resize(Rect, CropHandle.Right, v - Rect.Width, 0, Bounds, _context.Crop.Aspect));
    }

    public double? Height
    {
        get => Rect.Height;
        set => SetFromInput(value, v => CropGeometry.Resize(Rect, CropHandle.Bottom, 0, v - Rect.Height, Bounds, _context.Crop.Aspect));
    }

    public double? CustomWidth
    {
        get => _context.Crop.CustomWidth;
        set => SetCustom(value, _context.Crop.CustomHeight, true);
    }

    public double? CustomHeight
    {
        get => _context.Crop.CustomHeight;
        set => SetCustom(value, _context.Crop.CustomWidth, false);
    }

    public int MaxWidth => Bounds.Width;

    public int MaxHeight => Bounds.Height;

    private PixelRect Rect => _context.Crop.Rect;

    private PixelRect Bounds => _context.Crop.FullBounds;

    partial void OnSelectedAspectChanged(CropAspectOption value)
    {
        _context.Crop.SetKind(value.Kind);
        Hint = null;
    }

    [RelayCommand]
    private void Apply() => _tool().Commit();

    [RelayCommand]
    private void Cancel() => _tool().CancelMode();

    [RelayCommand]
    private void Reset()
    {
        var bounds = Bounds;
        _context.Crop.SetRect(_context.Crop.Aspect is { } aspect ? CropGeometry.ApplyAspect(bounds, aspect, bounds) : bounds);
        Hint = null;
    }

    [RelayCommand]
    private void CropToObjects()
    {
        if (CropGeometry.BoundsOfObjects(_context.Document.Annotations, ObjectMargin, Bounds) is { } rect)
        {
            SetRect(rect);
            Hint = null;
        }
        else
        {
            Hint = Strings.Crop_NoObjects;
        }
    }

    [RelayCommand]
    private void RemoveBorder()
    {
        var content = CropGeometry.DetectContent(_context.Document.BaseImage);
        if (content is not { } rect)
        {
            Hint = Strings.Crop_NoBorder;
            return;
        }

        // Objekte, die in den Rand hineinragen, bleiben vollständig sichtbar.
        if (CropGeometry.BoundsOfObjects(_context.Document.Annotations, 0, Bounds) is { } objects)
        {
            var union = rect.ToRectD().Union(objects.ToRectD());
            rect = PixelRect.FromLtrb((int)union.X, (int)union.Y, (int)Math.Ceiling(union.Right), (int)Math.Ceiling(union.Bottom));
        }

        SetRect(rect);
        Hint = null;
    }

    public void Dispose() => _context.Crop.Changed -= OnCropChanged;

    private void SetRect(PixelRect rect)
    {
        // Ein gewähltes Seitenverhältnis gilt für die Befehle nicht – sie liefern bewusst exakte Bereiche.
        if (_context.Crop.Kind != CropAspectKind.Free)
        {
            SelectedAspect = AspectOptions[0];
        }

        _context.Crop.SetRect(rect);
    }

    private void SetFromInput(double? value, Func<int, PixelRect> compute)
    {
        if (value is not { } v || double.IsNaN(v))
        {
            return;
        }

        var next = compute((int)Math.Round(v));
        if (next == Rect)
        {
            // Eingabe wurde geklemmt: Anzeige zurücksetzen.
            OnCropChanged(this, EventArgs.Empty);
            return;
        }

        _context.Crop.SetRect(next);
    }

    private void SetCustom(double? value, double other, bool isWidth)
    {
        if (value is not { } v || double.IsNaN(v) || v <= 0)
        {
            return;
        }

        if (isWidth)
        {
            _context.Crop.SetCustom(v, other);
        }
        else
        {
            _context.Crop.SetCustom(other, v);
        }
    }

    private void OnCropChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(CustomWidth));
        OnPropertyChanged(nameof(CustomHeight));
        OnPropertyChanged(nameof(MaxWidth));
        OnPropertyChanged(nameof(MaxHeight));
    }
}
