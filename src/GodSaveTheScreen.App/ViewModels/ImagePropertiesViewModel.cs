using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Eigenschaften der ausgewählten Bilder (Größe, Drehung, Spiegelung, Kontur; gemischte Werte = <c>null</c>); ohne Auswahl die
/// Vorgaben des Bild-Werkzeugs (Kontur, Deckkraft). Schatten stehen im allgemeinen Schatten-Abschnitt, die Deckkraft
/// ausgewählter Objekte im allgemeinen Panel. Änderungen laufen über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class ImagePropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;
    private readonly Action _insertImage;
    private readonly EffectSwitch<double> _outlineSwitch = new(t => t > 0, 0, 2);

    /// <summary>Breite und Höhe ändern sich gemeinsam (nur Panel-Zustand, nicht gespeichert).</summary>
    [ObservableProperty]
    private bool _keepAspectRatio = true;

    public ImagePropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool, Action insertImage)
    {
        _context = context;
        _activeTool = activeTool;
        _insertImage = insertImage;
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Image.Changed += OnStateChanged;
    }

    private IReadOnlyList<ImageAnnotation> Images => [.. _context.Selection.Items.OfType<ImageAnnotation>()];

    private bool IsDefaultsMode => _context.Selection.IsEmpty && _activeTool() is ImageTool;

    /// <summary>Der Bild-Abschnitt ist sichtbar: Bild-Werkzeug aktiv oder Bilder ausgewählt.</summary>
    public bool IsApplicable => Images.Count > 0 || IsDefaultsMode;

    public bool HasImages => Images.Count > 0;

    public bool ShowDefaults => IsDefaultsMode;

    /// <summary>Mindestens ein ausgewähltes Bild lässt sich nicht darstellen (beschädigte Datei).</summary>
    public bool HasPlaceholder => Images.Any(i => Rendering.AnnotationRenderer.GetImageBitmap(i) is null);

    public double? Width
    {
        get => StyleEditing.TryCommon(Images, i => Math.Round(i.Rect.Normalize().Width, 1), out var v) ? v : null;
        set => Resize(value, null);
    }

    public double? Height
    {
        get => StyleEditing.TryCommon(Images, i => Math.Round(i.Rect.Normalize().Height, 1), out var v) ? v : null;
        set => Resize(null, value);
    }

    /// <summary>Drehung in Grad (−180 bis 180).</summary>
    public double? Rotation
    {
        get => StyleEditing.TryCommon(Images, i => Math.Round(Signed(i.Rotation), 1), out var v) ? v : null;
        set
        {
            if (value is { } degrees)
            {
                StyleEditing.Apply(_context, nameof(Rotation), Images, RotationMath.Normalize(Math.Clamp(degrees, -180, 180)), i => i.Rotation, (i, v) => i.Rotation = v, Strings.Props_Rotation);
                Refresh();
            }
        }
    }

    public double RotationValue
    {
        get => Rotation ?? 0;
        set => Rotation = Math.Round(value);
    }

    public bool FlipHorizontal
    {
        get => Images.Count > 0 && Images.All(i => i.FlipHorizontal);
        set => Flip(nameof(FlipHorizontal), value, i => i.FlipHorizontal, (i, v) => i.FlipHorizontal = v);
    }

    public bool FlipVertical
    {
        get => Images.Count > 0 && Images.All(i => i.FlipVertical);
        set => Flip(nameof(FlipVertical), value, i => i.FlipVertical, (i, v) => i.FlipVertical = v);
    }

    public ArgbColor? OutlineColor
    {
        get => Get(i => i.Outline.Color, s => s.Outline.Color);
        set => Set(nameof(OutlineColor), value, i => i.Outline.Color, (i, v) => i.Outline = i.Outline with { Color = v }, (s, v) => s with { Outline = s.Outline with { Color = v } });
    }

    public double? OutlineThickness
    {
        get => Get(i => i.Outline.Thickness, s => s.Outline.Thickness);
        set => Set(nameof(OutlineThickness), value is { } v ? Math.Clamp(v, 0, ImageAnnotation.MaxOutlineThickness) : null, i => i.Outline.Thickness, (i, v) => i.Outline = i.Outline with { Thickness = v }, (s, v) => s with { Outline = s.Outline with { Thickness = v } });
    }

    /// <summary>Ein/Aus-Schalter der Kontur: aus = Stärke 0, ein = zuletzt verwendete Stärke (sonst 2).</summary>
    public bool? IsOutlineOn
    {
        get => _outlineSwitch.IsOn(OutlineThickness);
        set
        {
            if (value is { } on)
            {
                OutlineThickness = _outlineSwitch.Switch(OutlineThickness, on);
            }
        }
    }

    public double? OutlineBlur
    {
        get => Get(i => i.OutlineBlur, s => s.OutlineBlur);
        set => Set(nameof(OutlineBlur), value is { } v ? Math.Clamp(v, 0, ImageAnnotation.MaxOutlineBlur) : null, i => i.OutlineBlur, (i, v) => i.OutlineBlur = v, (s, v) => s with { OutlineBlur = v });
    }

    /// <summary>Deckkraft neuer Bilder in Prozent (Werkzeug-Vorgabe).</summary>
    public double? DefaultOpacityPercent
    {
        get => Math.Round(_context.Image.Style.Opacity * 100);
        set
        {
            if (value is { } percent)
            {
                _context.Image.Style = _context.Image.Style with { Opacity = Math.Clamp(percent, 0, 100) / 100 };
            }
        }
    }

    public double DefaultOpacityValue
    {
        get => DefaultOpacityPercent ?? 100;
        set => DefaultOpacityPercent = Math.Round(value);
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Image.Changed -= OnStateChanged;
    }

    [RelayCommand]
    private void Insert() => _insertImage();

    [RelayCommand]
    private void ResetRotation() => Rotation = 0;

    /// <summary>Setzt die ausgewählten Bilder auf ihre Größe beim Einfügen zurück (Mitte bleibt).</summary>
    [RelayCommand]
    private void ResetSize()
    {
        var changes = Images
            .Where(i => !i.IsLocked && !i.OriginalSize.IsEmpty)
            .Select(i => (Target: i, Old: i.Rect, New: Centered(i.Rect.Normalize(), i.OriginalSize.Width, i.OriginalSize.Height)))
            .Where(c => c.Old != c.New)
            .ToList();
        Commit(changes, Strings.Image_ResetSize);
    }

    /// <summary>Stellt das Seitenverhältnis der Originaldatei wieder her (Breite bleibt, Mitte bleibt).</summary>
    [RelayCommand]
    private void ResetAspect()
    {
        var changes = Images
            .Where(i => !i.IsLocked && !i.OriginalSize.IsEmpty)
            .Select(i => (Target: i, Old: i.Rect, New: Centered(i.Rect.Normalize(), i.Rect.Normalize().Width, i.Rect.Normalize().Width / i.AspectRatio)))
            .Where(c => c.Old != c.New)
            .ToList();
        Commit(changes, Strings.Image_ResetAspect);
    }

    private static double Signed(double degrees) => degrees > 180 ? degrees - 360 : degrees;

    private static RectD Centered(RectD rect, double width, double height)
    {
        var center = rect.Center;
        return new RectD(center.X - (width / 2), center.Y - (height / 2), width, height);
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Ändert Breite und/oder Höhe (Mitte bleibt; mit <see cref="KeepAspectRatio"/> wächst die andere Seite mit).</summary>
    private void Resize(double? width, double? height)
    {
        if ((width ?? height) is not { } requested)
        {
            return;
        }

        requested = Math.Max(ImageAnnotation.MinSize, requested);
        var changes = Images.Where(i => !i.IsLocked).Select(i =>
        {
            var rect = i.Rect.Normalize();
            var aspect = rect.Height > 0 ? rect.Width / rect.Height : 1;
            var (w, h) = width is not null
                ? (requested, KeepAspectRatio ? requested / aspect : rect.Height)
                : (KeepAspectRatio ? requested * aspect : rect.Width, requested);
            return (Target: i, Old: i.Rect, New: Centered(rect, Math.Max(ImageAnnotation.MinSize, w), Math.Max(ImageAnnotation.MinSize, h)));
        })
            .Where(c => c.Old != c.New)
            .ToList();
        Commit(changes, Strings.Image_Size);
    }

    private void Commit(List<(ImageAnnotation Target, RectD Old, RectD New)> changes, string description)
    {
        if (changes.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction(description))
        {
            foreach (var (image, old, resized) in changes)
            {
                var target = image;
                _context.Undo.Execute(new PropertyChangeAction<RectD>(target, nameof(ImageAnnotation.Rect), old, resized, v => target.Rect = v, description));
            }
        }

        Refresh();
    }

    private void Flip(string key, bool value, Func<ImageAnnotation, bool> get, Action<ImageAnnotation, bool> set)
    {
        StyleEditing.Apply(_context, key, Images, value, get, set, Strings.Emoji_Flip);
        Refresh();
    }

    private TV? Get<TV>(Func<ImageAnnotation, TV> get, Func<ImageStyle, TV> getDefault)
        where TV : struct
    {
        if (IsDefaultsMode)
        {
            return getDefault(_context.Image.Style);
        }

        return StyleEditing.TryCommon(Images, get, out var value) ? value : null;
    }

    private void Set<TV>(string key, TV? value, Func<ImageAnnotation, TV> get, Action<ImageAnnotation, TV> set, Func<ImageStyle, TV, ImageStyle> setDefault)
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (IsDefaultsMode)
        {
            _context.Image.Style = setDefault(_context.Image.Style, v);
        }
        else
        {
            StyleEditing.Apply(_context, key, Images, v, get, set, Strings.Image_Outline);
        }

        Refresh();
    }
}
