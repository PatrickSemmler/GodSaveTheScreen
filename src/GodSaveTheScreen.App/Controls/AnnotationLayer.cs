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
    private readonly Func<RenderContext> _context;
    private readonly Func<Annotation?> _hidden;

    public AnnotationElement(Annotation annotation, Func<RenderContext> context, Func<Annotation?> hidden)
    {
        Annotation = annotation;
        _context = context;
        _hidden = hidden;
        IsHitTestVisible = false;
        annotation.PropertyChanged += OnAnnotationChanged;
    }

    public Annotation Annotation { get; }

    /// <summary>Wird bei jeder Änderung des Objekts ausgelöst (Lupen müssen dann neu zeichnen).</summary>
    public event EventHandler? AnnotationChanged;

    public void Detach() => Annotation.PropertyChanged -= OnAnnotationChanged;

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (ReferenceEquals(_hidden(), Annotation))
        {
            return;
        }

        AnnotationRenderer.Render(drawingContext, Annotation, _context());
    }

    private void OnAnnotationChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
        AnnotationChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Ebene mit allen Objekten des Dokuments in Z-Reihenfolge. Jedes Objekt hat die Bildgröße als Fläche und zeichnet
/// in Bildpixeln; was über das Bild hinausragt, bleibt sichtbar (die Zeichenfläche wächst mit, abgeschnitten wird nur
/// beim Zuschnitt über den Rahmen in <see cref="EditorCanvas"/>).
/// </summary>
public sealed class AnnotationLayer : Canvas
{
    private const int MaxDisplayEmojiPixels = 2048;

    private ImageDocument? _document;
    private InteractionState? _interaction;
    private RenderContext _context = RenderContext.Default;

    public AnnotationLayer()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = false;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
    }

    public void Attach(ImageDocument document, InteractionState interaction, RenderContext context)
    {
        Detach();
        _document = document;
        _interaction = interaction;
        _context = context with { EmojiMaxPixels = MaxDisplayEmojiPixels };
        document.Annotations.CollectionChanged += OnCollectionChanged;
        document.PropertyChanged += OnDocumentChanged;
        Rebuild();
    }

    /// <summary>
    /// Geräte-Pixel je Bildpixel (Zoom): Emojis werden in dieser Auflösung neu gerastert, damit sie bei jeder Zoomstufe scharf bleiben.
    /// </summary>
    public void SetPixelScale(double pixelsPerImagePixel)
    {
        var scale = Math.Max(pixelsPerImagePixel, 0.01);
        if (Math.Abs(scale - _context.EmojiPixelScale) < 1e-9)
        {
            return;
        }

        _context = _context with { EmojiPixelScale = scale, EmojiMaxPixels = MaxDisplayEmojiPixels };
        foreach (var element in Children.OfType<AnnotationElement>().Where(e => e.Annotation is EmojiAnnotation))
        {
            element.InvalidateVisual();
        }
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
            _document.PropertyChanged -= OnDocumentChanged;
        }

        ClearChildren();
        _document = null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageDocument.HighlightersBehind))
        {
            _context = _context with { HighlightersBehind = _document!.HighlightersBehind };
            Rebuild();
        }
    }

    /// <summary>Lupen zeigen die Objekte unter sich: ändert sich eines, zeichnen alle Lupen neu.</summary>
    private void OnElementChanged(object? sender, EventArgs e)
    {
        foreach (var element in Children.OfType<AnnotationElement>())
        {
            if (element.Annotation is MagnifierAnnotation && !ReferenceEquals(element, sender))
            {
                element.InvalidateVisual();
            }
        }
    }

    private void ClearChildren()
    {
        foreach (var element in Children.OfType<AnnotationElement>())
        {
            element.AnnotationChanged -= OnElementChanged;
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
        foreach (var annotation in AnnotationOrdering.RenderOrder(_document.Annotations, _document.HighlightersBehind))
        {
            var element = new AnnotationElement(annotation, () => _context, () => interaction.Hidden)
            {
                Width = _document.BaseImage.Width,
                Height = _document.BaseImage.Height,
            };
            element.AnnotationChanged += OnElementChanged;
            Children.Add(element);
        }
    }
}
