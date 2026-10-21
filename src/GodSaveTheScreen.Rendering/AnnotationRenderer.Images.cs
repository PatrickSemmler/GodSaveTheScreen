using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Rendering;

/// <summary>Bildobjekte: Bild gedreht und gespiegelt, darunter die Kontur um die deckenden Bildteile.</summary>
public static partial class AnnotationRenderer
{
    /// <summary>Bitmap eines Bildobjekts in der Auflösung des Kontexts (SVG neu gerastert); <c>null</c>, wenn nicht darstellbar.</summary>
    public static BitmapSource? GetImageBitmap(ImageAnnotation a, RenderContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        context ??= RenderContext.Default;
        if (a.Source is not { } asset)
        {
            return null;
        }

        if (asset.Format != ImageAssetFormat.Svg)
        {
            return context.Images.Get(asset, 0, 0);
        }

        // SVG: Rastergröße = Rahmen × Maßstab, die längere Seite auf Stufen gerundet (wenige Neurasterungen beim Zoomen).
        var rect = a.Rect.Normalize();
        var longest = Math.Max(rect.Width, rect.Height);
        if (longest <= 0)
        {
            return null;
        }

        var wanted = longest * Math.Max(context.EmojiPixelScale, 0.01);
        var limit = Math.Min(context.EmojiMaxPixels * 2, SvgRasterizer.MaxPixelSize);
        var quantized = wanted <= EmojiRasterizer.MaxPixelSize ? EmojiCache.Quantize(wanted, limit) : Math.Min(limit, (int)Math.Ceiling(wanted / 256) * 256);
        var factor = quantized / longest;
        return context.Images.Get(asset, Math.Max(1, (int)Math.Round(rect.Width * factor)), Math.Max(1, (int)Math.Round(rect.Height * factor)));
    }

    private static void DrawImageAnnotation(DrawingContext dc, ImageAnnotation a, RenderContext context)
    {
        var rect = ToRect(a.Rect);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var drawing = ImageDrawingOf(a, rect, context);
        if (a.Outline.IsVisible)
        {
            var layer = SoftLayerCache.Get(a, "outline", SoftScale(context), () => SoftLayer.CreateOutline(
                drawing,
                a.Outline.Color,
                Math.Clamp(a.Outline.Thickness, 0, ImageAnnotation.MaxOutlineThickness),
                Math.Clamp(a.OutlineBlur, 0, ImageAnnotation.MaxOutlineBlur),
                SoftScale(context),
                context.EmojiMaxPixels));
            if (layer is not null)
            {
                dc.DrawImage(layer.Bitmap, layer.Rect);
            }
        }

        dc.DrawDrawing(drawing);
    }

    /// <summary>Das Bild als Zeichnung in Bildkoordinaten (gedreht, gespiegelt, hochwertig skaliert) bzw. Platzhalter.</summary>
    private static DrawingGroup ImageDrawingOf(ImageAnnotation a, Rect rect, RenderContext context)
    {
        var group = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.HighQuality);
        using (var dc = group.Open())
        {
            var pivot = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
            var pushes = 0;
            if (a.Rotation != 0)
            {
                dc.PushTransform(new RotateTransform(a.Rotation, pivot.X, pivot.Y));
                pushes++;
            }

            if (a.FlipHorizontal || a.FlipVertical)
            {
                dc.PushTransform(new ScaleTransform(a.FlipHorizontal ? -1 : 1, a.FlipVertical ? -1 : 1, pivot.X, pivot.Y));
                pushes++;
            }

            if (GetImageBitmap(a, context) is { } bitmap)
            {
                dc.DrawImage(bitmap, rect);
            }
            else
            {
                DrawEmojiPlaceholder(dc, rect);
            }

            for (var i = 0; i < pushes; i++)
            {
                dc.Pop();
            }
        }

        group.Freeze();
        return group;
    }
}
