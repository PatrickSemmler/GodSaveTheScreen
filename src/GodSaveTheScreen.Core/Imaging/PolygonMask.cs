using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Freiform-Ausschnitt: Bounding-Box ausschneiden, außerhalb des Polygons Alpha = 0 (Even-Odd-Füllregel).</summary>
public static class PolygonMask
{
    private const int AntiAliasFactor = 4;

    /// <summary>Umschließendes Pixelrechteck des Polygons (Endkanten exklusiv), nicht an ein Bild geklemmt.</summary>
    public static PixelRect BoundingBox(IReadOnlyList<PointD> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return PixelRect.Empty;
        }

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        return PixelRect.FromLtrb((int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX), (int)Math.Ceiling(maxY));
    }

    /// <summary>
    /// Schneidet die Bounding-Box des Polygons aus <paramref name="source"/> aus. Pixel außerhalb erhalten Alpha 0.
    /// Die Punkte liegen in Pixelkoordinaten von <paramref name="source"/>. Mit <paramref name="antiAlias"/> wird die
    /// Kante per 4×4-Supersampling geglättet (teiltransparente Randpixel).
    /// </summary>
    public static PixelBuffer Apply(PixelBuffer source, IReadOnlyList<PointD> points, bool antiAlias = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(points);

        var box = BoundingBox(points).Intersect(source.Bounds);
        if (box.IsEmpty || points.Count < 3)
        {
            return new PixelBuffer(0, 0);
        }

        var result = source.Crop(box);
        var samples = antiAlias ? AntiAliasFactor : 1;
        var coverage = new int[box.Width];
        var crossings = new List<double>();

        for (var row = 0; row < box.Height; row++)
        {
            Array.Clear(coverage);
            for (var sy = 0; sy < samples; sy++)
            {
                var y = box.Y + row + ((sy + 0.5) / samples);
                CollectCrossings(points, y, crossings);
                for (var i = 0; i + 1 < crossings.Count; i += 2)
                {
                    AddSpan(coverage, crossings[i] - box.X, crossings[i + 1] - box.X, samples);
                }
            }

            var maxCoverage = samples * samples;
            var offset = row * result.Stride;
            for (var x = 0; x < box.Width; x++)
            {
                var index = offset + (x * PixelBuffer.BytesPerPixel) + 3;
                var alpha = result.Pixels[index];
                result.Pixels[index] = coverage[x] switch
                {
                    0 => 0,
                    var c when c >= maxCoverage => alpha,
                    var c => (byte)((alpha * c) / maxCoverage),
                };
            }
        }

        return result;
    }

    /// <summary>X-Schnittpunkte des Polygons mit der Zeile <paramref name="y"/>, aufsteigend sortiert.</summary>
    private static void CollectCrossings(IReadOnlyList<PointD> points, double y, List<double> crossings)
    {
        crossings.Clear();
        var count = points.Count;
        for (var i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % count];
            if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
            {
                crossings.Add(a.X + ((y - a.Y) / (b.Y - a.Y) * (b.X - a.X)));
            }
        }

        crossings.Sort();
    }

    // Zählt für jedes Pixel die Unterabtastpunkte (Spalten) im Intervall [x0, x1).
    private static void AddSpan(int[] coverage, double x0, double x1, int samples)
    {
        var first = Math.Max(0, (int)Math.Ceiling((x0 * samples) - 0.5));
        var last = Math.Min((coverage.Length * samples) - 1, (int)Math.Ceiling((x1 * samples) - 0.5) - 1);
        for (var k = first; k <= last; k++)
        {
            coverage[k / samples]++;
        }
    }
}

/// <summary>Macht die Ecken eines Bilds transparent (abgerundete Fenster unter Windows 11).</summary>
public static class CornerMask
{
    /// <summary>Rundet die vier Ecken mit dem Radius (in Pixeln) ab; Alpha am Rand wird geglättet.</summary>
    public static void Apply(PixelBuffer image, int radius)
    {
        ArgumentNullException.ThrowIfNull(image);
        radius = Math.Min(radius, Math.Min(image.Width, image.Height) / 2);
        if (radius <= 0)
        {
            return;
        }

        for (var dy = 0; dy < radius; dy++)
        {
            for (var dx = 0; dx < radius; dx++)
            {
                // Abstand des Pixelmittelpunkts zum Kreismittelpunkt der Ecke.
                var distance = Math.Sqrt(Math.Pow(radius - dx - 0.5, 2) + Math.Pow(radius - dy - 0.5, 2));
                var factor = Math.Clamp(radius - distance + 0.5, 0, 1);
                if (factor >= 1)
                {
                    continue;
                }

                Scale(image, dx, dy, factor);
                Scale(image, image.Width - 1 - dx, dy, factor);
                Scale(image, dx, image.Height - 1 - dy, factor);
                Scale(image, image.Width - 1 - dx, image.Height - 1 - dy, factor);
            }
        }
    }

    private static void Scale(PixelBuffer image, int x, int y, double factor)
    {
        var index = (((y * image.Width) + x) * PixelBuffer.BytesPerPixel) + 3;
        image.Pixels[index] = (byte)Math.Round(image.Pixels[index] * factor);
    }
}
