using System.Collections.Concurrent;
using System.Numerics;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.WIC;
using DCommon = Vortice.DCommon;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Rendert Emoji-Sequenzen farbig über DirectWrite/Direct2D (Segoe UI Emoji, COLR/CPAL) in einen <see cref="PixelBuffer"/>.
/// WPF zeichnet Farbschriften nur einfarbig; deshalb wird hier gerastert und das Bitmap in den normalen Renderpfad übernommen.
/// </summary>
public static class EmojiRasterizer
{
    public const string FontFamilyName = "Segoe UI Emoji";

    public const int MinPixelSize = 4;

    public const int MaxPixelSize = 4096;

    private const float ReferenceFontSize = 64;
    private const int ReferenceCanvas = 224;
    private const float ReferenceOrigin = 56;
    private const double FillFactor = 0.96;
    private const string ReferenceSequence = "\U0001F600";

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, bool> SupportCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, InkMetrics?> InkCache = new(StringComparer.Ordinal);
    private static Native? _native;
    private static bool _nativeFailed;

    /// <summary>Ink-Rahmen eines Emojis in Einheiten der Schriftgröße relativ zum Textursprung.</summary>
    private readonly record struct InkMetrics(double CenterX, double CenterY, double Width, double Height);

    /// <summary>Ist die Schrift „Segoe UI Emoji“ vorhanden und Direct2D nutzbar?</summary>
    public static bool IsAvailable => GetNative() is not null;

    /// <summary>
    /// Kann die installierte Schrift die Sequenz als <b>ein</b> farbiges Zeichen darstellen? Länderflaggen (Regional-Indicator-Paare)
    /// und Sequenzen mit unbekannten Zeichen (neuer als die Schrift) gelten als nicht darstellbar. Ergebnis je Sitzung gecacht.
    /// </summary>
    public static bool IsSupported(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
        {
            return false;
        }

        return SupportCache.GetOrAdd(sequence, static s =>
        {
            lock (Gate)
            {
                return GetNative() is { } native && native.IsSupported(s);
            }
        });
    }

    /// <summary>Quadratisches, nicht vormultipliziertes BGRA32-Bild; <c>null</c>, wenn die Sequenz nicht darstellbar ist.</summary>
    public static PixelBuffer? Render(string sequence, int pixelSize)
    {
        if (!IsSupported(sequence))
        {
            return null;
        }

        var size = Math.Clamp(pixelSize, MinPixelSize, MaxPixelSize);
        lock (Gate)
        {
            var native = GetNative();
            if (native is null)
            {
                return null;
            }

            var ink = GetInk(native, sequence);
            if (ink is not { } metrics)
            {
                return null;
            }

            var reference = GetInk(native, ReferenceSequence) ?? metrics;
            var fontSize = size * FillFactor / Math.Max(Math.Max(reference.Width, reference.Height), 0.01);
            var largest = Math.Max(metrics.Width, metrics.Height) * fontSize;
            if (largest > size * FillFactor)
            {
                fontSize *= size * FillFactor / largest;
            }

            var origin = new Vector2(
                (float)((size / 2.0) - (metrics.CenterX * fontSize)),
                (float)((size / 2.0) - (metrics.CenterY * fontSize)));
            return native.Draw(sequence, (float)fontSize, size, size, origin);
        }
    }

    /// <summary>WPF-Bitmap (eingefroren) der Sequenz in der Pixelgröße.</summary>
    public static BitmapSource? RenderBitmap(string sequence, int pixelSize)
    {
        var buffer = Render(sequence, pixelSize);
        return buffer?.ToBitmapSource();
    }

    private static InkMetrics? GetInk(Native native, string sequence) =>
        InkCache.GetOrAdd(sequence, s =>
        {
            var buffer = native.Draw(s, ReferenceFontSize, ReferenceCanvas, ReferenceCanvas, new Vector2(ReferenceOrigin, ReferenceOrigin));
            if (buffer is null)
            {
                return null;
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (var y = 0; y < buffer.Height; y++)
            {
                for (var x = 0; x < buffer.Width; x++)
                {
                    if (buffer.Pixels[(y * buffer.Stride) + (x * PixelBuffer.BytesPerPixel) + 3] > 12)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0)
            {
                return null;
            }

            var width = (maxX - minX + 1) / (double)ReferenceFontSize;
            var height = (maxY - minY + 1) / (double)ReferenceFontSize;
            var centerX = (((minX + maxX + 1) / 2.0) - ReferenceOrigin) / ReferenceFontSize;
            var centerY = (((minY + maxY + 1) / 2.0) - ReferenceOrigin) / ReferenceFontSize;
            return new InkMetrics(centerX, centerY, width, height);
        });

    private static Native? GetNative()
    {
        lock (Gate)
        {
            if (_native is not null || _nativeFailed)
            {
                return _native;
            }

            try
            {
                _native = Native.TryCreate();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _native = null;
            }

            _nativeFailed = _native is null;
            return _native;
        }
    }

    private sealed class Native : IDisposable
    {
        private readonly ID2D1Factory _d2d;
        private readonly IDWriteFactory _dw;
        private readonly IWICImagingFactory _wic;
        private readonly IDWriteFont _font;

        private Native(ID2D1Factory d2d, IDWriteFactory dw, IWICImagingFactory wic, IDWriteFont font)
        {
            _d2d = d2d;
            _dw = dw;
            _wic = wic;
            _font = font;
        }

        public static Native? TryCreate()
        {
            var d2d = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.MultiThreaded);
            var dw = DWrite.DWriteCreateFactory<IDWriteFactory2>();
            using var collection = dw.GetSystemFontCollection(false);
            if (!collection.FindFamilyName(FontFamilyName, out var index))
            {
                dw.Dispose();
                d2d.Dispose();
                return null;
            }

            using var family = collection.GetFontFamily(index);
            var font = family.GetFirstMatchingFont(FontWeight.Normal, FontStretch.Normal, Vortice.DirectWrite.FontStyle.Normal);
            return new Native(d2d, dw, new IWICImagingFactory(), font);
        }

        public bool IsSupported(string sequence)
        {
            // Jedes Zeichen muss in der Schrift vorhanden sein (sonst Font-Fallback bzw. Kästchen).
            for (var i = 0; i < sequence.Length; i++)
            {
                var codePoint = char.ConvertToUtf32(sequence, i);
                if (char.IsHighSurrogate(sequence[i]))
                {
                    i++;
                }

                if (codePoint is 0x200D or 0xFE0F)
                {
                    continue;
                }

                if (!_font.HasCharacter((uint)codePoint))
                {
                    return false;
                }
            }

            // Länderflaggen: Regional-Indicator-Paare werden nur als Buchstaben dargestellt.
            if (sequence.Length >= 4 && char.ConvertToUtf32(sequence, 0) is >= 0x1F1E6 and <= 0x1F1FF)
            {
                return false;
            }

            using var layout = CreateLayout(sequence, 100, 1000, 1000);
            layout.GetClusterMetrics(null!, out var count);
            return count == 1;
        }

        public PixelBuffer? Draw(string sequence, float fontSize, int width, int height, Vector2 origin)
        {
            using var bitmap = _wic.CreateBitmap((uint)width, (uint)height, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapCreateCacheOption.CacheOnLoad);
            var properties = new RenderTargetProperties(
                RenderTargetType.Default,
                new DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, DCommon.AlphaMode.Premultiplied),
                96,
                96,
                RenderTargetUsage.None,
                FeatureLevel.Default);
            using var target = _d2d.CreateWicBitmapRenderTarget(bitmap, properties);
            using var layout = CreateLayout(sequence, fontSize, Math.Max(width, 1000) * 4f, Math.Max(height, 1000) * 4f);
            using var brush = target.CreateSolidColorBrush(new Color4(0, 0, 0, 1));
            target.BeginDraw();
            target.Clear(new Color4(0, 0, 0, 0));
            target.DrawTextLayout(origin, layout, brush, DrawTextOptions.EnableColorFont);
            target.EndDraw();

            var stride = width * PixelBuffer.BytesPerPixel;
            var pixels = new byte[stride * height];
            bitmap.CopyPixels((uint)stride, pixels);
            Unpremultiply(pixels);
            return new PixelBuffer(width, height, pixels);
        }

        public void Dispose()
        {
            _font.Dispose();
            _wic.Dispose();
            _dw.Dispose();
            _d2d.Dispose();
        }

        private IDWriteTextLayout CreateLayout(string text, float fontSize, float maxWidth, float maxHeight)
        {
            using var format = _dw.CreateTextFormat(FontFamilyName, FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, fontSize);
            format.WordWrapping = WordWrapping.NoWrap;
            return _dw.CreateTextLayout(text, format, maxWidth, maxHeight);
        }

        private static void Unpremultiply(byte[] pixels)
        {
            for (var i = 0; i < pixels.Length; i += PixelBuffer.BytesPerPixel)
            {
                var alpha = pixels[i + 3];
                if (alpha is 0 or 255)
                {
                    continue;
                }

                pixels[i] = (byte)Math.Min(255, ((pixels[i] * 255) + (alpha / 2)) / alpha);
                pixels[i + 1] = (byte)Math.Min(255, ((pixels[i + 1] * 255) + (alpha / 2)) / alpha);
                pixels[i + 2] = (byte)Math.Min(255, ((pixels[i + 2] * 255) + (alpha / 2)) / alpha);
            }
        }
    }
}
