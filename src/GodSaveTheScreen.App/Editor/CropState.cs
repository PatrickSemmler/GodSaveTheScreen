using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Seitenverhältnis des Zuschnittrahmens.</summary>
public enum CropAspectKind
{
    Free,
    Original,
    Square,
    Ratio4x3,
    Ratio3x2,
    Ratio16x9,
    Ratio9x16,
    Custom,
}

/// <summary>Flüchtiger Zustand des Zuschnittmodus (Arbeitsrahmen, Seitenverhältnis); nicht Teil des Dokuments.</summary>
public sealed class CropState
{
    private readonly ImageDocument _document;
    private PixelRect _rect;
    private CropAspectKind _kind;
    private double _customWidth = 1;
    private double _customHeight = 1;
    private bool _isActive;
    private bool _isDragging;

    public CropState(ImageDocument document) => _document = document;

    public event EventHandler? Changed;

    /// <summary>Der gesamte Bildbereich, in dem der Rahmen liegen darf.</summary>
    public PixelRect FullBounds => _document.BaseImage.Bounds;

    public bool IsActive => _isActive;

    /// <summary>Während des Ziehens: Drittel-Raster anzeigen.</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging != value)
            {
                _isDragging = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Aktueller Arbeitsrahmen in Pixeln des Basisbilds.</summary>
    public PixelRect Rect => _rect;

    public CropAspectKind Kind => _kind;

    public double CustomWidth => _customWidth;

    public double CustomHeight => _customHeight;

    /// <summary>Breite/Höhe oder <c>null</c> bei freiem Verhältnis.</summary>
    public double? Aspect => _kind switch
    {
        CropAspectKind.Original => (double)_document.BaseImage.Width / _document.BaseImage.Height,
        CropAspectKind.Square => 1,
        CropAspectKind.Ratio4x3 => 4.0 / 3,
        CropAspectKind.Ratio3x2 => 3.0 / 2,
        CropAspectKind.Ratio16x9 => 16.0 / 9,
        CropAspectKind.Ratio9x16 => 9.0 / 16,
        CropAspectKind.Custom => _customWidth > 0 && _customHeight > 0 ? _customWidth / _customHeight : null,
        _ => null,
    };

    /// <summary>Beginnt den Modus mit dem aktuellen Zuschnitt (bzw. der vollen Fläche).</summary>
    public void Begin()
    {
        _rect = _document.EffectiveBounds;
        _isActive = true;
        _isDragging = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        _isActive = false;
        _isDragging = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Setzt den Rahmen (an die Bildfläche geklemmt).</summary>
    public void SetRect(PixelRect rect)
    {
        var clamped = rect.Intersect(FullBounds);
        if (clamped.IsEmpty || clamped == _rect)
        {
            return;
        }

        _rect = clamped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Wählt das Seitenverhältnis und passt den Rahmen daran an.</summary>
    public void SetKind(CropAspectKind kind)
    {
        _kind = kind;
        ApplyAspect();
    }

    public void SetCustom(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _customWidth = width;
        _customHeight = height;
        if (_kind == CropAspectKind.Custom)
        {
            ApplyAspect();
        }
    }

    private void ApplyAspect()
    {
        if (Aspect is { } aspect)
        {
            SetRect(CropGeometry.ApplyAspect(_rect, aspect, FullBounds));
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
