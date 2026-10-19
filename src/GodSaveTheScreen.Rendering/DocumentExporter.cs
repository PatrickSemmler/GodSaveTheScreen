using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>Einstiegspunkt für Export und Zwischenablage: zeichnet über den gemeinsamen <see cref="AnnotationRenderer"/>.</summary>
public sealed class DocumentExporter : IDocumentExporter
{
    public DocumentExporter() => TextLayout.Install();

    public PixelBuffer Render(ImageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var visible = document.Annotations.Where(a => a.IsVisible && a.Opacity > 0).ToList();
        if (visible.Count == 0)
        {
            // Unverändertes Basisbild: exakt, ohne Vormultiplikations-Umweg.
            return document.BaseImage;
        }

        var baseImage = document.BaseImage;
        var baseSource = baseImage.ToBitmapSource();
        var context = new RenderContext
        {
            BaseImage = baseSource,
            BasePixels = baseImage,
            Annotations = document.Annotations,
            HighlightersBehind = document.HighlightersBehind,
        };
        var visual = CreateVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(baseSource, new Rect(0, 0, baseImage.Width, baseImage.Height));
            foreach (var annotation in AnnotationOrdering.RenderOrder(visible, document.HighlightersBehind))
            {
                AnnotationRenderer.Render(dc, annotation, context);
            }
        }

        return ToPixelBuffer(visual, baseImage.Width, baseImage.Height);
    }

    public PixelBuffer? RenderObjects(IReadOnlyList<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var visible = annotations.Where(a => a.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return null;
        }

        var bounds = visible.Select(a => a.VisualBounds).Aggregate((x, y) => x.Union(y));
        var left = Math.Floor(bounds.X);
        var top = Math.Floor(bounds.Y);
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Right - left));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Bottom - top));

        var visual = CreateVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-left, -top));
            foreach (var annotation in visible)
            {
                AnnotationRenderer.Render(dc, annotation);
            }

            dc.Pop();
        }

        return ToPixelBuffer(visual, width, height);
    }

    private static DrawingVisual CreateVisual()
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Ideal);
        return visual;
    }

    private static PixelBuffer ToPixelBuffer(DrawingVisual visual, int width, int height)
    {
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return PixelBufferExtensions.FromBitmapSource(target);
    }
}
