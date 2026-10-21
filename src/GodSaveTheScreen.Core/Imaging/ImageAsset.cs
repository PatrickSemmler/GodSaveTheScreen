using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Speicherformat eingebetteter Bilder: PNG und JPEG bleiben Original, andere Rasterformate werden PNG, SVG bleibt Text.</summary>
public enum ImageAssetFormat
{
    Png,
    Jpeg,
    Svg,
}

/// <summary>
/// Unveränderliche Bilddatei eines Bildobjekts (Bytes der Datei, nicht dekodiert). Gleichheit über Format und SHA-256;
/// Kopien und Undo-Schnappschüsse teilen sich dieselbe Instanz. Die Bytes dürfen nach dem Erzeugen nicht verändert werden.
/// </summary>
[JsonConverter(typeof(ImageAssetJsonConverter))]
public sealed class ImageAsset : IEquatable<ImageAsset>
{
    /// <summary>Obergrenze einer eingebetteten Bilddatei (Projekt und Zwischenablage).</summary>
    public const long MaxBytes = 256L * 1024 * 1024;

    private readonly byte[] _data;

    public ImageAsset(ImageAssetFormat format, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new ArgumentException("Leere Bilddatei.", nameof(data));
        }

        Format = format;
        _data = data;
        Hash = Convert.ToHexStringLower(SHA256.HashData(data));
    }

    public ImageAssetFormat Format { get; }

    /// <summary>SHA-256 der Bytes, 64 Kleinbuchstaben-Hexziffern.</summary>
    public string Hash { get; }

    /// <summary>Die Bytes der Datei (nicht verändern).</summary>
    public byte[] Data => _data;

    public int Length => _data.Length;

    public string Extension => ExtensionOf(Format);

    /// <summary>Name des Eintrags in der Projektdatei (oberste Ebene, aus dem Hash gebildet).</summary>
    public string EntryName => EntryNameOf(Hash, Format);

    public static string ExtensionOf(ImageAssetFormat format) => format switch
    {
        ImageAssetFormat.Jpeg => ".jpg",
        ImageAssetFormat.Svg => ".svg",
        _ => ".png",
    };

    public static string EntryNameOf(string hash, ImageAssetFormat format) => $"image-{hash}{ExtensionOf(format)}";

    /// <summary>Gültiger Hash: genau 64 Hexziffern in Kleinbuchstaben (schützt die Eintragsnamen beim Lesen).</summary>
    public static bool IsValidHash(string? hash) =>
        hash is { Length: 64 } && hash.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public bool Equals(ImageAsset? other) =>
        other is not null && (ReferenceEquals(this, other) || (Format == other.Format && Hash == other.Hash));

    public override bool Equals(object? obj) => Equals(obj as ImageAsset);

    public override int GetHashCode() => HashCode.Combine(Format, Hash);
}

/// <summary>
/// Bereich, in dem Bilddateien beim (De-)Serialisieren nicht inline als Base64 stehen, sondern als eigene Einträge
/// der Projektdatei: Beim Schreiben sammelt er die Dateien (je Hash einmal), beim Lesen liefert er sie über den Hash.
/// Gilt für den aktuellen Thread, bis er freigegeben wird. Ohne Bereich (Objekt-Zwischenablage) stehen die Bytes im JSON.
/// </summary>
public sealed class ImageAssetScope : IDisposable
{
    [ThreadStatic]
    private static ImageAssetScope? _current;

    private readonly ImageAssetScope? _previous;
    private readonly Dictionary<string, ImageAsset> _written = new(StringComparer.Ordinal);
    private readonly Func<string, ImageAssetFormat, byte[]?>? _resolve;

    private ImageAssetScope(Func<string, ImageAssetFormat, byte[]?>? resolve)
    {
        _resolve = resolve;
        _previous = _current;
        _current = this;
    }

    internal static ImageAssetScope? Current => _current;

    /// <summary>Gesammelte Bilddateien in der Reihenfolge des ersten Auftretens.</summary>
    public IReadOnlyCollection<ImageAsset> Written => _written.Values;

    /// <summary>Bereich zum Schreiben: Bilddateien werden gesammelt und nur über Format und Hash referenziert.</summary>
    public static ImageAssetScope ForWriting() => new(null);

    /// <summary>Bereich zum Lesen: <paramref name="resolve"/> liefert die Bytes zu Hash und Format oder <c>null</c>.</summary>
    public static ImageAssetScope ForReading(Func<string, ImageAssetFormat, byte[]?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        return new ImageAssetScope(resolve);
    }

    public void Dispose() => _current = _previous;

    internal bool IsWriting => _resolve is null;

    internal void Add(ImageAsset asset) => _written.TryAdd(asset.EntryName, asset);

    internal byte[]? Resolve(string hash, ImageAssetFormat format) => _resolve?.Invoke(hash, format);
}

/// <summary>
/// JSON eines <see cref="ImageAsset"/>: <c>{"format":"Png","hash":"…","data":"Base64"}</c>; im <see cref="ImageAssetScope"/>
/// zum Schreiben ohne <c>data</c>. Beim Lesen werden inline übergebene Bytes neu gehasht; fehlt die Datei, schlägt das
/// Lesen des Objekts fehl (der Aufrufer überspringt es mit Warnung).
/// </summary>
internal sealed class ImageAssetJsonConverter : JsonConverter<ImageAsset>
{
    public override ImageAsset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Bilddatei erwartet.");
        }

        ImageAssetFormat? format = null;
        string? hash = null;
        byte[]? data = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Ungültige Bilddatei.");
            }

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "format":
                    format = Enum.TryParse<ImageAssetFormat>(reader.GetString(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                        ? parsed
                        : throw new JsonException("Unbekanntes Bildformat.");
                    break;
                case "hash":
                    hash = reader.GetString();
                    break;
                case "data":
                    data = reader.TokenType == JsonTokenType.Null ? null : reader.GetBytesFromBase64();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (format is not { } f)
        {
            throw new JsonException("Das Bildformat fehlt.");
        }

        if (data is null && ImageAssetIsResolvable(hash, f, out var resolved))
        {
            data = resolved;
        }

        if (data is null || data.Length == 0)
        {
            throw new JsonException("Die eingebettete Bilddatei fehlt.");
        }

        if (data.Length > ImageAsset.MaxBytes)
        {
            throw new JsonException("Die eingebettete Bilddatei ist zu groß.");
        }

        return new ImageAsset(f, data);
    }

    public override void Write(Utf8JsonWriter writer, ImageAsset value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("format", value.Format.ToString());
        writer.WriteString("hash", value.Hash);
        if (ImageAssetScope.Current is { IsWriting: true } scope)
        {
            scope.Add(value);
        }
        else
        {
            writer.WriteBase64String("data", value.Data);
        }

        writer.WriteEndObject();
    }

    private static bool ImageAssetIsResolvable(string? hash, ImageAssetFormat format, out byte[]? data)
    {
        data = null;
        if (ImageAssetScope.Current is not { IsWriting: false } scope || !ImageAsset.IsValidHash(hash))
        {
            return false;
        }

        data = scope.Resolve(hash!, format);
        return data is not null;
    }
}
