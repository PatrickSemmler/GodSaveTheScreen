using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>
/// JSON für Objektlisten (Objekt-Zwischenablage; ab M10 Teil des Projektformats).
/// Diskriminatoren stehen in <see cref="Annotation"/> und dürfen nie geändert werden.
/// </summary>
public static class AnnotationSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = false,
    };

    public static string Serialize(IEnumerable<Annotation> annotations) =>
        JsonSerializer.Serialize(annotations.ToList(), Options);

    /// <summary>Liest Objekte; die Objekte behalten ihre gespeicherte <see cref="Annotation.Id"/>.</summary>
    public static IReadOnlyList<Annotation> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<Annotation>>(json, Options) ?? [];

    /// <summary>Deserialisiert und vergibt neue Ids (Einfügen).</summary>
    public static IReadOnlyList<Annotation> DeserializeAsCopies(string json) =>
        [.. Deserialize(json).Select(a => a.Clone())];
}
