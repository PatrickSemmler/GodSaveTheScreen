using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Pixelpuffer im Format BGRA32, nicht vormultipliziert.</summary>
public sealed class PixelBuffer
{
    public const int BytesPerPixel = 4;

    public PixelBuffer(int width, int height)
        : this(width, height, new byte[checked(width * height * BytesPerPixel)])
    {
    }

    public PixelBuffer(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < checked(width * height * BytesPerPixel))
        {
            throw new ArgumentException("Pixelarray ist zu klein für die angegebene Größe.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * BytesPerPixel;

    public byte[] Pixels { get; }

    public PixelRect Bounds => new(0, 0, Width, Height);

    /// <summary>Schneidet einen Bereich aus; der Bereich wird an die Puffergrenzen geklemmt.</summary>
    public PixelBuffer Crop(PixelRect rect)
    {
        var r = rect.Intersect(Bounds);
        var result = new PixelBuffer(r.Width, r.Height);
        for (var row = 0; row < r.Height; row++)
        {
            Buffer.BlockCopy(Pixels, ((r.Y + row) * Stride) + (r.X * BytesPerPixel), result.Pixels, row * result.Stride, result.Stride);
        }

        return result;
    }

    public PixelBuffer Clone() => new(Width, Height, (byte[])Pixels.Clone());
}
