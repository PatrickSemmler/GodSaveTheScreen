using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Angefasster Teil des Zuschnittrahmens.</summary>
public enum CropHandle
{
    None,
    Move,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

/// <summary>Rechenlogik des Zuschneidens (ohne UI): Größenänderung mit Seitenverhältnis, Klemmen, Einrasten, Randerkennung.</summary>
public static class CropGeometry
{
    public const int MinSize = 1;

    /// <summary>Verschiebt den Rahmen; Größe bleibt erhalten, der Rahmen bleibt in <paramref name="bounds"/>.</summary>
    public static PixelRect Move(PixelRect start, int dx, int dy, PixelRect bounds)
    {
        var width = Math.Min(start.Width, bounds.Width);
        var height = Math.Min(start.Height, bounds.Height);
        var x = Math.Clamp(start.X + dx, bounds.X, bounds.Right - width);
        var y = Math.Clamp(start.Y + dy, bounds.Y, bounds.Bottom - height);
        return new PixelRect(x, y, width, height);
    }

    /// <summary>
    /// Zieht den Griff um (<paramref name="dx"/>, <paramref name="dy"/>) Pixel. Mit <paramref name="aspect"/> (Breite/Höhe)
    /// bleibt das Verhältnis erhalten: Eckgriffe halten die Gegenecke fest, Kantengriffe die Mitte der Gegenachse.
    /// </summary>
    public static PixelRect Resize(PixelRect start, CropHandle handle, int dx, int dy, PixelRect bounds, double? aspect = null)
    {
        if (handle == CropHandle.Move)
        {
            return Move(start, dx, dy, bounds);
        }

        if (handle == CropHandle.None)
        {
            return start;
        }

        return aspect is { } ratio and > 0
            ? ResizeWithAspect(start, handle, dx, dy, bounds, ratio)
            : ResizeFree(start, handle, dx, dy, bounds);
    }

    private static PixelRect ResizeFree(PixelRect start, CropHandle handle, int dx, int dy, PixelRect bounds)
    {
        int left = start.X, top = start.Y, right = start.Right, bottom = start.Bottom;
        if (handle is CropHandle.TopLeft or CropHandle.Left or CropHandle.BottomLeft)
        {
            left = Math.Clamp(start.X + dx, bounds.X, right - MinSize);
        }

        if (handle is CropHandle.TopRight or CropHandle.Right or CropHandle.BottomRight)
        {
            right = Math.Clamp(start.Right + dx, left + MinSize, bounds.Right);
        }

        if (handle is CropHandle.TopLeft or CropHandle.Top or CropHandle.TopRight)
        {
            top = Math.Clamp(start.Y + dy, bounds.Y, bottom - MinSize);
        }

        if (handle is CropHandle.BottomLeft or CropHandle.Bottom or CropHandle.BottomRight)
        {
            bottom = Math.Clamp(start.Bottom + dy, top + MinSize, bounds.Bottom);
        }

        return PixelRect.FromLtrb(left, top, right, bottom);
    }

    private static PixelRect ResizeWithAspect(PixelRect start, CropHandle handle, int dx, int dy, PixelRect bounds, double aspect)
    {
        if (handle is CropHandle.Left or CropHandle.Right)
        {
            var anchor = handle == CropHandle.Right ? start.X : start.Right;
            var sign = handle == CropHandle.Right ? 1 : -1;
            var maxWidth = handle == CropHandle.Right ? bounds.Right - anchor : anchor - bounds.X;
            double width = Math.Max(MinSize, start.Width + (sign * dx));
            var height = width / aspect;
            (width, height) = FitInside(width, height, maxWidth, bounds.Height, aspect);
            var w = Math.Max(MinSize, (int)Math.Round(width));
            var h = Math.Clamp((int)Math.Round(height), MinSize, bounds.Height);
            var centerY = start.Y + (start.Height / 2.0);
            var y = Math.Clamp((int)Math.Round(centerY - (h / 2.0)), bounds.Y, bounds.Bottom - h);
            return new PixelRect(sign > 0 ? anchor : anchor - w, y, w, h);
        }

        if (handle is CropHandle.Top or CropHandle.Bottom)
        {
            var anchor = handle == CropHandle.Bottom ? start.Y : start.Bottom;
            var sign = handle == CropHandle.Bottom ? 1 : -1;
            var maxHeight = handle == CropHandle.Bottom ? bounds.Bottom - anchor : anchor - bounds.Y;
            double height = Math.Max(MinSize, start.Height + (sign * dy));
            var width = height * aspect;
            (width, height) = FitInside(width, height, bounds.Width, maxHeight, aspect);
            var h = Math.Max(MinSize, (int)Math.Round(height));
            var w = Math.Clamp((int)Math.Round(width), MinSize, bounds.Width);
            var centerX = start.X + (start.Width / 2.0);
            var x = Math.Clamp((int)Math.Round(centerX - (w / 2.0)), bounds.X, bounds.Right - w);
            return new PixelRect(x, sign > 0 ? anchor : anchor - h, w, h);
        }

        // Eckgriff: Gegenecke bleibt fest.
        var right = handle is CropHandle.TopRight or CropHandle.BottomRight;
        var below = handle is CropHandle.BottomLeft or CropHandle.BottomRight;
        var anchorX = right ? start.X : start.Right;
        var anchorY = below ? start.Y : start.Bottom;
        var movingX = (right ? start.Right : start.X) + dx;
        var movingY = (below ? start.Bottom : start.Y) + dy;
        var rawWidth = Math.Max(MinSize, right ? movingX - anchorX : anchorX - movingX);
        var rawHeight = Math.Max(MinSize, below ? movingY - anchorY : anchorY - movingY);

        // Das Verhältnis folgt der Achse, die der Zeiger weiter übererfüllt.
        double cornerWidth, cornerHeight;
        if (rawWidth / aspect >= rawHeight)
        {
            cornerWidth = rawWidth;
            cornerHeight = rawWidth / aspect;
        }
        else
        {
            cornerHeight = rawHeight;
            cornerWidth = rawHeight * aspect;
        }

        var maxW = right ? bounds.Right - anchorX : anchorX - bounds.X;
        var maxH = below ? bounds.Bottom - anchorY : anchorY - bounds.Y;
        (cornerWidth, cornerHeight) = FitInside(cornerWidth, cornerHeight, maxW, maxH, aspect);
        var cw = Math.Max(MinSize, (int)Math.Round(cornerWidth));
        var ch = Math.Max(MinSize, (int)Math.Round(cornerHeight));
        return new PixelRect(right ? anchorX : anchorX - cw, below ? anchorY : anchorY - ch, cw, ch);
    }

    private static (double Width, double Height) FitInside(double width, double height, double maxWidth, double maxHeight, double aspect)
    {
        if (width > maxWidth)
        {
            width = maxWidth;
            height = width / aspect;
        }

        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * aspect;
        }

        return (Math.Max(width, MinSize), Math.Max(height, MinSize));
    }

    /// <summary>Neuer Bereich, aufgezogen vom Ankerpunkt zum Zielpunkt (optional mit festem Seitenverhältnis).</summary>
    public static PixelRect FromDrag(PointD anchor, PointD target, PixelRect bounds, double? aspect = null)
    {
        var ax = Math.Clamp((int)Math.Round(anchor.X), bounds.X, bounds.Right);
        var ay = Math.Clamp((int)Math.Round(anchor.Y), bounds.Y, bounds.Bottom);
        var px = Math.Clamp((int)Math.Round(target.X), bounds.X, bounds.Right);
        var py = Math.Clamp((int)Math.Round(target.Y), bounds.Y, bounds.Bottom);
        if (aspect is not { } ratio || ratio <= 0)
        {
            return PixelRect.FromLtrb(Math.Min(ax, px), Math.Min(ay, py), Math.Max(ax, px), Math.Max(ay, py));
        }

        var signX = px >= ax ? 1 : -1;
        var signY = py >= ay ? 1 : -1;
        double w = Math.Abs(px - ax), h = Math.Abs(py - ay);
        if (w / ratio >= h)
        {
            h = w / ratio;
        }
        else
        {
            w = h * ratio;
        }

        var maxW = signX > 0 ? bounds.Right - ax : ax - bounds.X;
        var maxH = signY > 0 ? bounds.Bottom - ay : ay - bounds.Y;
        (w, h) = FitInside(w, h, Math.Max(maxW, MinSize), Math.Max(maxH, MinSize), ratio);
        var iw = Math.Max(MinSize, (int)Math.Round(w));
        var ih = Math.Max(MinSize, (int)Math.Round(h));
        return new PixelRect(signX > 0 ? ax : ax - iw, signY > 0 ? ay : ay - ih, iw, ih).Intersect(bounds);
    }

    /// <summary>Klemmt den Rahmen in die Grenzen (Größe bleibt, soweit sie passt).</summary>
    public static PixelRect Clamp(PixelRect rect, PixelRect bounds)
    {
        var width = Math.Clamp(rect.Width, MinSize, bounds.Width);
        var height = Math.Clamp(rect.Height, MinSize, bounds.Height);
        return new PixelRect(
            Math.Clamp(rect.X, bounds.X, bounds.Right - width),
            Math.Clamp(rect.Y, bounds.Y, bounds.Bottom - height),
            width,
            height);
    }

    /// <summary>Passt den Rahmen auf das Seitenverhältnis an (Größe bleibt möglichst groß, Mitte bleibt).</summary>
    public static PixelRect ApplyAspect(PixelRect rect, double aspect, PixelRect bounds)
    {
        if (aspect <= 0)
        {
            return rect;
        }

        double width = rect.Width, height = rect.Height;
        if (width / height > aspect)
        {
            width = height * aspect;
        }
        else
        {
            height = width / aspect;
        }

        (width, height) = FitInside(width, height, bounds.Width, bounds.Height, aspect);
        var w = Math.Max(MinSize, (int)Math.Round(width));
        var h = Math.Max(MinSize, (int)Math.Round(height));
        var cx = rect.X + (rect.Width / 2.0);
        var cy = rect.Y + (rect.Height / 2.0);
        return Clamp(new PixelRect((int)Math.Round(cx - (w / 2.0)), (int)Math.Round(cy - (h / 2.0)), w, h), bounds);
    }

    /// <summary>Rastet einen Wert an den nächsten Kandidaten innerhalb der Toleranz ein; sonst unverändert.</summary>
    public static double Snap(double value, IReadOnlyList<double> candidates, double tolerance)
    {
        var best = value;
        var bestDistance = tolerance;
        foreach (var candidate in candidates)
        {
            var distance = Math.Abs(candidate - value);
            if (distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Einrastlinien: Bildränder sowie Kanten der Objekt-Ausdehnungen (sichtbar).</summary>
    public static (List<double> Xs, List<double> Ys) SnapLines(PixelRect bounds, IEnumerable<Annotation> annotations)
    {
        List<double> xs = [bounds.X, bounds.Right];
        List<double> ys = [bounds.Y, bounds.Bottom];
        foreach (var annotation in annotations.Where(a => a.IsVisible))
        {
            var box = annotation.VisualBounds;
            if (box.IsEmpty)
            {
                continue;
            }

            xs.Add(Math.Round(box.X));
            xs.Add(Math.Round(box.Right));
            ys.Add(Math.Round(box.Y));
            ys.Add(Math.Round(box.Bottom));
        }

        return (xs, ys);
    }

    /// <summary>Bounding-Box aller sichtbaren Objekte plus Rand, geklemmt an die Bildfläche; <c>null</c>, wenn keine Objekte vorhanden sind.</summary>
    public static PixelRect? BoundsOfObjects(IEnumerable<Annotation> annotations, int margin, PixelRect bounds)
    {
        RectD? union = null;
        foreach (var annotation in annotations.Where(a => a.IsVisible))
        {
            var box = annotation.VisualBounds;
            if (box.IsEmpty)
            {
                continue;
            }

            union = union is { } u ? u.Union(box) : box;
        }

        if (union is not { } all)
        {
            return null;
        }

        var left = (int)Math.Floor(all.X) - margin;
        var top = (int)Math.Floor(all.Y) - margin;
        var right = (int)Math.Ceiling(all.Right) + margin;
        var bottom = (int)Math.Ceiling(all.Bottom) + margin;
        var rect = PixelRect.FromLtrb(left, top, right, bottom).Intersect(bounds);
        return rect.IsEmpty ? null : rect;
    }

    /// <summary>
    /// Erkennt einen Rand einheitlicher Farbe (Farbe der oberen linken Ecke) und liefert den Inhaltsbereich;
    /// <c>null</c>, wenn kein Rand vorhanden ist oder das Bild einfarbig ist.
    /// </summary>
    public static PixelRect? DetectContent(PixelBuffer image, int tolerance = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width == 0 || image.Height == 0)
        {
            return null;
        }

        var pixels = image.Pixels;
        var stride = image.Stride;
        byte b = pixels[0], g = pixels[1], r = pixels[2], a = pixels[3];

        bool Differs(int x, int y)
        {
            var i = (y * stride) + (x * PixelBuffer.BytesPerPixel);
            return Math.Abs(pixels[i] - b) > tolerance
                || Math.Abs(pixels[i + 1] - g) > tolerance
                || Math.Abs(pixels[i + 2] - r) > tolerance
                || Math.Abs(pixels[i + 3] - a) > tolerance;
        }

        bool RowDiffers(int y)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (Differs(x, y))
                {
                    return true;
                }
            }

            return false;
        }

        bool ColumnDiffers(int x, int top, int bottom)
        {
            for (var y = top; y < bottom; y++)
            {
                if (Differs(x, y))
                {
                    return true;
                }
            }

            return false;
        }

        var first = 0;
        while (first < image.Height && !RowDiffers(first))
        {
            first++;
        }

        if (first == image.Height)
        {
            return null;
        }

        var last = image.Height - 1;
        while (last > first && !RowDiffers(last))
        {
            last--;
        }

        var left = 0;
        while (left < image.Width && !ColumnDiffers(left, first, last + 1))
        {
            left++;
        }

        var right = image.Width - 1;
        while (right > left && !ColumnDiffers(right, first, last + 1))
        {
            right--;
        }

        var content = PixelRect.FromLtrb(left, first, right + 1, last + 1);
        return content == image.Bounds ? null : content;
    }
}
