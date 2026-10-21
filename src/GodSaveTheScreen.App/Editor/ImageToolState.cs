using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Letzte Konfiguration des Bild-Werkzeugs (<see cref="AppSettings.ImageDefaults"/>); jede Änderung wird gespeichert.</summary>
public sealed class ImageToolState
{
    /// <summary>Versatz in Bildpixeln, wenn mehrere Bilder auf einmal eingefügt werden.</summary>
    public const double CascadeOffset = 24;

    private readonly ISettingsService _settings;

    public ImageToolState(ISettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    public ImageStyle Style
    {
        get => (_settings.Current.ImageDefaults ?? new ImageStyle()).Normalized();
        set
        {
            if (value == Style)
            {
                return;
            }

            _settings.Current = _settings.Current with { ImageDefaults = value };
            _ = _settings.SaveAsync();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Überträgt Deckkraft, Schatten und Kontur eines Stils auf ein Bild.</summary>
    public static void Apply(ImageAnnotation image, ImageStyle style)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(style);
        var normalized = style.Normalized();
        image.Opacity = normalized.Opacity;
        image.Shadow = normalized.Shadow;
        image.Outline = normalized.Outline;
        image.OutlineBlur = Math.Clamp(normalized.OutlineBlur, 0, ImageAnnotation.MaxOutlineBlur);
    }

    /// <summary>Neues Bildobjekt mit den Vorgaben über <paramref name="rect"/>.</summary>
    public ImageAnnotation Create(ImportedImage image, RectD rect, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var annotation = new ImageAnnotation { Source = image.Asset, OriginalSize = image.Size, Rect = rect.Normalize() };
        Apply(annotation, Style);
        if (!string.IsNullOrWhiteSpace(name))
        {
            annotation.Name = name.Trim();
        }

        return annotation;
    }

    /// <summary>
    /// Fügt Bilder als einen Undo-Schritt ein und wählt sie aus. Mit <paramref name="area"/> (aufgezogener Bereich) wird jedes
    /// Bild proportional hineingepasst, sonst in Originalgröße (höchstens 80 % des Screenshots) um <paramref name="center"/>
    /// gesetzt; weitere Bilder versetzt.
    /// </summary>
    public IReadOnlyList<ImageAnnotation> Insert(EditorContext context, IReadOnlyList<(ImportedImage Image, string? Name)> images, PointD center, RectD? area = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count == 0)
        {
            return [];
        }

        var document = context.Document;
        var canvas = new SizeD(document.BaseImage.Width, document.BaseImage.Height);
        var created = new List<ImageAnnotation>(images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var (image, name) = images[i];
            var offset = new VectorD(CascadeOffset * i, CascadeOffset * i);
            var rect = area is { } a
                ? ImageAnnotation.FitInto(image.Size, new RectD(a.X + offset.X, a.Y + offset.Y, a.Width, a.Height))
                : ImageAnnotation.PlaceAt(image.Size, center + offset, canvas);
            var annotation = Create(image, rect, name);
            if (string.IsNullOrWhiteSpace(annotation.Name))
            {
                annotation.Name = AnnotationNames.NextName([.. document.Annotations, .. created], annotation);
            }

            created.Add(annotation);
        }

        var description = created.Count == 1 ? "Bild einfügen" : "Bilder einfügen";
        using (context.Undo.BeginTransaction(description))
        {
            foreach (var annotation in created)
            {
                context.Undo.Execute(new AddAnnotationAction(document.Annotations, annotation, description: description));
            }
        }

        context.Selection.Set(created);
        return created;
    }
}
