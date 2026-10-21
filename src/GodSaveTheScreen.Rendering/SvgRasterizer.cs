using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using SkiaSharp;
using Svg.Skia;

namespace GodSaveTheScreen.Rendering;

/// <summary>Geladenes SVG: Zeichnung (Skia) und Ausdehnung in SVG-Einheiten.</summary>
public sealed class SvgPicture : IDisposable
{
    private readonly SKSvg _svg;

    internal SvgPicture(SKSvg svg, SKPicture picture, double width, double height)
    {
        _svg = svg;
        Picture = picture;
        Width = width;
        Height = height;
    }

    internal SKPicture Picture { get; }

    /// <summary>Breite der Zeichnung (Ausschnitt der <c>viewBox</c> bzw. <c>width</c>).</summary>
    public double Width { get; }

    public double Height { get; }

    public void Dispose() => _svg.Dispose();
}

/// <summary>
/// SVG über Svg.Skia. Externe Verweise (Dateien, URLs, externe Entitäten) werden nie aufgelöst; eingebettete
/// <c>data:</c>-Bilder funktionieren. Fehlerhafte Dateien liefern <c>null</c>.
/// </summary>
public static class SvgRasterizer
{
    /// <summary>Kantenlänge (längere Seite) eines SVG ohne feste Größe (nur <c>viewBox</c> oder Prozentangaben).</summary>
    public const double DefaultSize = 256;

    /// <summary>Größte Rastergröße in Pixeln je Seite.</summary>
    public const int MaxPixelSize = 8192;

    private static readonly Svg.Model.SvgParameters SecureParameters = new(
        null,
        null,
        null,
        new Svg.SvgDocumentLoadOptions
        {
            ProcessingMode = Svg.SvgProcessingMode.SecureStatic,
            ExternalResources = Svg.SvgExternalResourcePolicy.SameDocumentAndDataOnly,
        });

    static SvgRasterizer()
    {
        // Zusätzlich global: keine externen Bilder, Elemente oder Entitäten.
        Svg.SvgDocument.ResolveExternalImages = Svg.ExternalType.None;
        Svg.SvgDocument.ResolveExternalElements = Svg.ExternalType.None;
        Svg.SvgDocument.ResolveExternalXmlEntites = Svg.ExternalType.None;
    }

    /// <summary>Sieht der Inhalt wie ein SVG aus (XML mit Wurzelelement <c>svg</c>)?</summary>
    public static bool LooksLikeSvg(ReadOnlySpan<byte> data)
    {
        var head = System.Text.Encoding.UTF8.GetString(data[..Math.Min(data.Length, 4096)]);
        return head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Lädt ein SVG; <c>null</c>, wenn es kein darstellbares SVG ist.</summary>
    public static SvgPicture? Load(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var svg = new SKSvg();
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            var picture = svg.Load(stream, SecureParameters);
            if (picture is null)
            {
                svg.Dispose();
                return null;
            }

            var cull = picture.CullRect;
            if (!(cull.Width > 0) || !(cull.Height > 0) || !float.IsFinite(cull.Width) || !float.IsFinite(cull.Height))
            {
                svg.Dispose();
                return null;
            }

            return new SvgPicture(svg, picture, cull.Width, cull.Height);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or IOException or NullReferenceException or IndexOutOfRangeException or OverflowException)
        {
            svg.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Größe beim Einfügen: <c>width</c>/<c>height</c> des Wurzelelements in Pixeln, sonst die längere Seite
    /// <see cref="DefaultSize"/> im Seitenverhältnis der Zeichnung.
    /// </summary>
    public static (double Width, double Height) IntrinsicSize(byte[] data, SvgPicture picture)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(picture);
        if (ReadRootSize(data) is { } size)
        {
            return size;
        }

        var scale = DefaultSize / Math.Max(picture.Width, picture.Height);
        return (picture.Width * scale, picture.Height * scale);
    }

    /// <summary>Rastert die Zeichnung gestreckt auf <paramref name="width"/> × <paramref name="height"/> Pixel.</summary>
    public static BitmapSource Render(SvgPicture picture, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(picture);
        width = Math.Clamp(width, 1, MaxPixelSize);
        height = Math.Clamp(height, 1, MaxPixelSize);
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var cull = picture.Picture.CullRect;
            canvas.Scale(width / cull.Width, height / cull.Height);
            canvas.Translate(-cull.Left, -cull.Top);
            canvas.DrawPicture(picture.Picture);
            canvas.Flush();
        }

        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        result.Freeze();
        return result;
    }

    /// <summary>Feste Größe aus <c>width</c>/<c>height</c> (Einheiten px, pt, mm, cm, in; ohne Einheit = px); Prozent = keine.</summary>
    private static (double Width, double Height)? ReadRootSize(byte[] data)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = 0,
            };
            using var stream = new MemoryStream(data, writable: false);
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!string.Equals(reader.LocalName, "svg", StringComparison.Ordinal))
                {
                    return null;
                }

                var width = ParseLength(reader.GetAttribute("width"));
                var height = ParseLength(reader.GetAttribute("height"));
                return width is { } w && height is { } h ? (w, h) : null;
            }
        }
        catch (XmlException)
        {
            // Größe unbekannt.
        }

        return null;
    }

    private static double? ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        var factor = 1.0;
        foreach (var (unit, f) in new[] { ("px", 1.0), ("pt", 96.0 / 72), ("pc", 16.0), ("mm", 96 / 25.4), ("cm", 96 / 2.54), ("in", 96.0) })
        {
            if (value.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^unit.Length].Trim();
                factor = f;
                break;
            }
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0 && double.IsFinite(number)
            ? number * factor
            : null;
    }
}
