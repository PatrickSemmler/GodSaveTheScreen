using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;

namespace GodSaveTheScreen.Core.Project;

/// <summary>Das Projekt ist beschädigt, hat ein unbekanntes Format oder überschreitet die Sicherheitsgrenzen.</summary>
public sealed class ProjectFormatException : Exception
{
    public ProjectFormatException()
    {
    }

    public ProjectFormatException(string message)
        : base(message)
    {
    }

    public ProjectFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Überführt eine ältere Formatversion auf JSON-Ebene in die nächste (vor dem Deserialisieren).</summary>
public interface IProjectMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    void Migrate(JsonObject manifest, JsonObject document);
}

/// <summary>Inhalt einer <c>.gsts</c>-Datei (Bilder als PNG-Bytes, damit <c>Core</c> frei von WPF bleibt).</summary>
public sealed record ProjectContent
{
    public string AppVersion { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.Now;

    public int Width { get; init; }

    public int Height { get; init; }

    public double DpiX { get; init; } = 96;

    public double DpiY { get; init; } = 96;

    public PixelRect? CropRect { get; init; }

    public bool HighlightersBehind { get; init; } = true;

    public DocumentMetadata Metadata { get; init; } = new();

    public IReadOnlyList<Annotation> Annotations { get; init; } = [];

    public required byte[] BasePng { get; init; }

    public byte[]? PreviewPng { get; init; }

    /// <summary>Beim Lesen: übersprungene Objekte, neuere Version u. Ä.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Projektformat <c>.gsts</c>: ZIP mit <c>manifest.json</c>, <c>document.json</c>, <c>base.png</c> und <c>preview.png</c>.
/// Es werden nur die festen Eintragsnamen gelesen (Pfade im ZIP werden ignoriert); Größen sind begrenzt (ZIP-Bomben).
/// </summary>
public static class ProjectSerializer
{
    public const string FormatName = "GodSaveTheScreen.Project";
    public const int CurrentVersion = 1;
    public const string Extension = ".gsts";

    public const string ManifestEntry = "manifest.json";
    public const string DocumentEntry = "document.json";
    public const string BaseEntry = "base.png";
    public const string PreviewEntry = "preview.png";

    public const long MaxManifestBytes = 64 * 1024;
    public const long MaxDocumentBytes = 128L * 1024 * 1024;
    public const long MaxBaseBytes = 1536L * 1024 * 1024;
    public const long MaxPreviewBytes = 16L * 1024 * 1024;
    public const int MaxDimension = 32768;
    public const long MaxPixels = 16384L * 16384;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = false,
    };

    private static readonly JsonSerializerOptions IndentedOptions = new(Options) { WriteIndented = true };

    // ---- Objektlisten (auch Objekt-Zwischenablage) ----

    /// <summary>JSON-Array der Objekte (Diskriminator <c>type</c>, siehe <see cref="Annotation"/>).</summary>
    public static string SerializeAnnotations(IEnumerable<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        return JsonSerializer.Serialize(annotations.ToList(), Options);
    }

    /// <summary>Liest Objekte tolerant: unbekannte oder defekte Einträge werden übersprungen und in <paramref name="warnings"/> gemeldet.</summary>
    public static IReadOnlyList<Annotation> DeserializeAnnotations(string json, ICollection<string>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProjectFormatException("Die Objektliste ist kein gültiges JSON.", ex);
        }

        return node is JsonArray array ? ReadAnnotations(array, warnings) : [];
    }

    private static List<Annotation> ReadAnnotations(JsonArray array, ICollection<string>? warnings)
    {
        var result = new List<Annotation>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            try
            {
                if (array[i].Deserialize<Annotation>(Options) is { } annotation)
                {
                    result.Add(annotation);
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
            {
                var type = array[i] is JsonObject o && o["type"] is JsonValue v ? v.ToString() : "?";
                warnings?.Add($"Objekt {i + 1} (Typ „{type}“) wurde übersprungen: {ex.Message}");
            }
        }

        return result;
    }

    // ---- Schreiben ----

    public static void Write(Stream output, ProjectContent content)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(content);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);

        var manifest = new JsonObject
        {
            ["format"] = FormatName,
            ["formatVersion"] = CurrentVersion,
            ["appVersion"] = content.AppVersion,
            ["createdAt"] = content.CreatedAt,
            ["modifiedAt"] = content.ModifiedAt,
        };
        WriteText(zip, ManifestEntry, manifest.ToJsonString(IndentedOptions));

        var document = new JsonObject
        {
            ["width"] = content.Width,
            ["height"] = content.Height,
            ["dpiX"] = content.DpiX,
            ["dpiY"] = content.DpiY,
            ["cropRect"] = content.CropRect is { } crop ? JsonSerializer.SerializeToNode(crop, Options) : null,
            ["highlightersBehind"] = content.HighlightersBehind,
            ["metadata"] = new JsonObject
            {
                ["captureMode"] = content.Metadata.CaptureMode,
                ["windowTitle"] = content.Metadata.WindowTitle,
            },
            ["annotations"] = JsonSerializer.SerializeToNode(content.Annotations.ToList(), Options),
        };
        WriteText(zip, DocumentEntry, document.ToJsonString(Options));

        // PNG ist bereits komprimiert: kein weiteres Deflate.
        WriteBytes(zip, BaseEntry, content.BasePng, CompressionLevel.NoCompression);
        if (content.PreviewPng is { Length: > 0 } preview)
        {
            WriteBytes(zip, PreviewEntry, preview, CompressionLevel.NoCompression);
        }
    }

    private static void WriteText(ZipArchive zip, string name, string text) =>
        WriteBytes(zip, name, Encoding.UTF8.GetBytes(text), CompressionLevel.Optimal);

    private static void WriteBytes(ZipArchive zip, string name, byte[] data, CompressionLevel level)
    {
        var entry = zip.CreateEntry(name, level);
        using var stream = entry.Open();
        stream.Write(data);
    }

    // ---- Lesen ----

    /// <summary>Liest ein Projekt. Ältere Versionen werden mit <paramref name="migrations"/> angehoben.</summary>
    /// <exception cref="ProjectFormatException">Datei beschädigt, unbekannt oder zu groß.</exception>
    public static ProjectContent Read(Stream input, IEnumerable<IProjectMigration>? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var warnings = new List<string>();
        try
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);

            var manifestBytes = ReadEntry(zip, ManifestEntry, MaxManifestBytes, required: true)!;
            var documentBytes = ReadEntry(zip, DocumentEntry, MaxDocumentBytes, required: true)!;
            var manifest = JsonNode.Parse(manifestBytes) as JsonObject ?? throw new ProjectFormatException("manifest.json ist ungültig.");
            var document = JsonNode.Parse(documentBytes) as JsonObject ?? throw new ProjectFormatException("document.json ist ungültig.");

            if (manifest["format"]?.GetValue<string>() != FormatName)
            {
                throw new ProjectFormatException("Das ist keine GodSaveTheScreen-Projektdatei.");
            }

            var version = manifest["formatVersion"]?.GetValue<int>() ?? throw new ProjectFormatException("Die Formatversion fehlt.");
            version = Migrate(version, manifest, document, migrations);
            if (version > CurrentVersion)
            {
                warnings.Add($"Das Projekt stammt aus einer neueren Programmversion (Format {version}); nicht erkannte Inhalte werden ignoriert.");
            }

            var width = document["width"]?.GetValue<int>() ?? 0;
            var height = document["height"]?.GetValue<int>() ?? 0;
            if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
            {
                throw new ProjectFormatException($"Ungültige oder zu große Bildgröße ({width} × {height}).");
            }

            var baseBytes = ReadEntry(zip, BaseEntry, MaxBaseBytes, required: true)!;
            var previewBytes = ReadEntry(zip, PreviewEntry, MaxPreviewBytes, required: false);

            var crop = document["cropRect"] is { } cropNode ? cropNode.Deserialize<PixelRect>(Options) : (PixelRect?)null;
            if (crop is { } c)
            {
                var clamped = c.Intersect(new PixelRect(0, 0, width, height));
                crop = clamped.IsEmpty || clamped == new PixelRect(0, 0, width, height) ? null : clamped;
            }

            var metadata = new DocumentMetadata(
                document["metadata"]?["captureMode"]?.GetValue<string>(),
                document["metadata"]?["windowTitle"]?.GetValue<string>());

            var annotations = document["annotations"] is JsonArray array ? ReadAnnotations(array, warnings) : [];

            return new ProjectContent
            {
                AppVersion = manifest["appVersion"]?.GetValue<string>() ?? string.Empty,
                CreatedAt = manifest["createdAt"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.Now,
                ModifiedAt = manifest["modifiedAt"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.Now,
                Width = width,
                Height = height,
                DpiX = document["dpiX"]?.GetValue<double>() ?? 96,
                DpiY = document["dpiY"]?.GetValue<double>() ?? 96,
                CropRect = crop,
                HighlightersBehind = document["highlightersBehind"]?.GetValue<bool>() ?? true,
                Metadata = metadata,
                Annotations = annotations,
                BasePng = baseBytes,
                PreviewPng = previewBytes,
                Warnings = warnings,
            };
        }
        catch (ProjectFormatException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException or FormatException or NotSupportedException or OverflowException)
        {
            throw new ProjectFormatException("Die Projektdatei ist beschädigt.", ex);
        }
    }

    /// <summary>Liest nur die Vorschau (<c>preview.png</c>) – für die Liste „Zuletzt verwendet“; <c>null</c>, wenn keine vorhanden ist.</summary>
    public static byte[]? ReadPreview(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true, Encoding.UTF8);
            return ReadEntry(zip, PreviewEntry, MaxPreviewBytes, required: false);
        }
        catch (Exception ex) when (ex is InvalidDataException or ProjectFormatException)
        {
            return null;
        }
    }

    /// <summary>Liest die Bildgröße aus dem PNG-Kopf (IHDR), ohne zu dekodieren; <c>null</c>, wenn es kein PNG ist.</summary>
    public static (int Width, int Height)? ReadPngSize(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(signature) || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        return width > int.MaxValue || height > int.MaxValue ? null : ((int)width, (int)height);
    }

    private static int Migrate(int version, JsonObject manifest, JsonObject document, IEnumerable<IProjectMigration>? migrations)
    {
        var available = (migrations ?? []).ToList();
        while (version < CurrentVersion)
        {
            var step = available.FirstOrDefault(m => m.FromVersion == version)
                ?? throw new ProjectFormatException($"Die Projektversion {version} wird nicht mehr unterstützt.");
            step.Migrate(manifest, document);
            version = step.ToVersion;
            manifest["formatVersion"] = version;
        }

        return version;
    }

    /// <summary>Liest einen Eintrag mit festem Namen und harter Größengrenze (unabhängig von der Angabe im ZIP-Verzeichnis).</summary>
    private static byte[]? ReadEntry(ZipArchive zip, string name, long limit, bool required)
    {
        // Nur exakte Namen auf oberster Ebene; alles andere (Unterordner, "..") wird ignoriert.
        var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, name, StringComparison.Ordinal));
        if (entry is null)
        {
            return required ? throw new ProjectFormatException($"Der Eintrag {name} fehlt in der Projektdatei.") : null;
        }

        if (entry.Length > limit)
        {
            throw new ProjectFormatException($"Der Eintrag {name} ist zu groß.");
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new ProjectFormatException($"Der Eintrag {name} ist zu groß.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
