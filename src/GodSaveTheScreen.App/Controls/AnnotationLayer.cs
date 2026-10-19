using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Zeichnet ein Objekt über den gemeinsamen <see cref="AnnotationRenderer"/> (Bildpixel-Koordinaten).</summary>
public sealed class AnnotationElement : FrameworkElement
{
    private readonly RenderContext _context;
    private readonly Func<Annotation?> _hidden;

    public AnnotationElement(Annotation annotation, RenderContext context, Func<Annotation?> hidden)
    {
        Annotation = annotation;
        _context = context;
        _hidden = hidden;
        IsHitTestVisible = false;
        annotation.PropertyChanged += OnAnnotationChanged;
    }

    public Annotation Annotation { get; }

    public void Detach() => Annotation.PropertyChanged -= OnAnnotationChanged;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (ReferenceEquals(_hidden(), Annotation))
        {
            return;
        }

        AnnotationRenderer.Render(drawingContext, Annotation, _context);
    }

    private void OnAnnotationChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
}

/// <summary>
/// Ebene mit allen Objekten des Dokuments in Z-Reihenfolge. Jedes Objekt hat die Bildgröße als Fläche und zeichnet
/// in Bildpixeln; was außerhalb des Bilds liegt, wird – wie im Export – abgeschnitten.
/// </summary>
public sealed class AnnotationLayer : Canvas
{
    private ImageDocument? _document;
    private InteractionState? _interaction;
    private RenderContext _context = RenderContext.Default;

    public AnnotationLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SnapsToDevicePixels = false;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
    }

    public void Attach(ImageDocument document, InteractionState interaction, RenderContext context)
    {
        Detach();
        _document = document;
        _interaction = interaction;
        _context = context;
        document.Annotations.CollectionChanged += OnCollectionChanged;
        Rebuild();
    }

    /// <summary>Zeichnet alle Objekte neu (z. B. nach Änderung des ausgeblendeten Objekts).</summary>
    public void InvalidateAll()
    {
        foreach (var element in Children.OfType<AnnotationElement>())
        {
            element.InvalidateVisual();
        }
    }

    public void Detach()
    {
        if (_document is not null)
        {
            _document.Annotations.CollectionChanged -= OnCollectionChanged;
        }

        ClearChildren();
        _document = null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void ClearChildren()
    {
        foreach (var element in Children.OfType<AnnotationElement>())
        {
            element.Detach();
        }

        Children.Clear();
    }

    private void Rebuild()
    {
        ClearChildren();
        if (_document is null || _interaction is null)
        {
            return;
        }

        var interaction = _interaction;
        foreach (var annotation in _document.Annotations)
        {
            Children.Add(new AnnotationElement(annotation, _context, () => interaction.Hidden)
            {
                Width = _document.BaseImage.Width,
                Height = _document.BaseImage.Height,
            });
        }
    }
}
