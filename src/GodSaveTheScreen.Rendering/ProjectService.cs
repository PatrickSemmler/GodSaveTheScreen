using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Project;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.Rendering;

/// <summary>Projektdateien lesen/schreiben: Dokument ⇄ <see cref="ProjectContent"/>, Bilder über den Codec-Dienst.</summary>
public sealed class ProjectService : IProjectService
{
    /// <summary>Längste Kante der Vorschau in Pixeln.</summary>
    public const int PreviewSize = 512;

    private static readonly string AppVersion =
        typeof(ProjectService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

    private readonly IDocumentExporter _exporter;
    private readonly IImageCodecService _codec;

    public ProjectService(IDocumentExporter exporter, IImageCodecService codec)
    {
        _exporter = exporter;
        _codec = codec;
    }

    public async Task SaveAsync(ImageDocument document, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Rendern und Kopieren der Objekte braucht den UI-Thread; Kodieren und Schreiben laufen im Hintergrund.
        var preview = BuildPreview(document);
        var basePng = await Task.Run(() => EncodeBase(document), cancellationToken);
        var content = Snapshot(document, basePng) with { PreviewPng = preview };
        await WriteAsync(content, path, cancellationToken);
    }

    public ProjectContent Snapshot(ImageDocument document, byte[] basePng)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(basePng);
        return new ProjectContent
        {
            AppVersion = AppVersion,
            CreatedAt = document.CreatedAt,
            ModifiedAt = DateTimeOffset.Now,
            Width = document.BaseImage.Width,
            Height = document.BaseImage.Height,
            DpiX = document.DpiX,
            DpiY = document.DpiY,
            CropRect = document.CropRect,
            HighlightersBehind = document.HighlightersBehind,
            Metadata = document.Metadata,
            Annotations = [.. document.Annotations.Select(a => a.Snapshot())],
            BasePng = basePng,
            Mask = document.Mask.IsOpaque ? null : (byte[])document.Mask.Values.Clone(),
        };
    }

    public byte[] EncodeBase(ImageDocument document) =>
        _codec.Encode(document.BaseImage, document.DpiX, document.DpiY, new ImageSaveOptions(ImageFormatSetting.Png));

    public async Task WriteAsync(ProjectContent content, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = $"{full}.{Guid.NewGuid():N}.tmp";
        try
        {
            await Task.Run(
                () =>
                {
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        ProjectSerializer.Write(stream, content);
                    }

                    File.Move(temp, full, overwrite: true);
                },
                cancellationToken);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    public async Task<LoadedProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = await Task.Run(
            () =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return ProjectSerializer.Read(stream);
            },
            cancellationToken);

        // Kopfdaten prüfen, bevor dekodiert wird (kleine PNG-Datei mit riesiger Bildgröße).
        if (ProjectSerializer.ReadPngSize(content.BasePng) is not { } size || size.Width != content.Width || size.Height != content.Height)
        {
            throw new ProjectFormatException("Das Originalbild im Projekt passt nicht zu den Angaben im Dokument.");
        }

        LoadedImage image;
        try
        {
            image = _codec.Decode(content.BasePng);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or FileFormatException or System.Runtime.InteropServices.COMException or ArgumentException)
        {
            throw new ProjectFormatException("Das Originalbild im Projekt konnte nicht gelesen werden.", ex);
        }

        var document = new ImageDocument(image.Image, content.DpiX, content.DpiY)
        {
            CreatedAt = content.CreatedAt,
            CropRect = content.CropRect,
            HighlightersBehind = content.HighlightersBehind,
            Metadata = content.Metadata,
            ProjectPath = path,
        };
        if (content.Mask is { } mask)
        {
            document.Mask.Write(document.Mask.Bounds, mask);
        }

        foreach (var annotation in content.Annotations)
        {
            document.Annotations.Add(annotation);
        }

        return new LoadedProject(document, content.Warnings);
    }

    public async Task<byte[]?> ReadPreviewAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(
                () =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    return ProjectSerializer.ReadPreview(stream);
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Gerendertes Ergebnis (mit Zuschnitt), auf höchstens <see cref="PreviewSize"/> Pixel verkleinert, als PNG.</summary>
    private byte[]? BuildPreview(ImageDocument document)
    {
        try
        {
            var rendered = _exporter.Render(document);
            return Downscale(rendered, PreviewSize).EncodePng();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or ArgumentException or OutOfMemoryException)
        {
            // Die Vorschau ist optional.
            return null;
        }
    }

    /// <summary>Verkleinert hochwertig auf die längste Kante <paramref name="maxSize"/> (größere Bilder bleiben unverändert).</summary>
    public static PixelBuffer Downscale(PixelBuffer source, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxSize)
        {
            return source;
        }

        var factor = (double)maxSize / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * factor));
        var height = Math.Max(1, (int)Math.Round(source.Height * factor));
        var bitmap = source.ToBitmapSource();
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(bitmap, new Rect(0, 0, width, height));
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return PixelBufferExtensions.FromBitmapSource(target);
    }
}
