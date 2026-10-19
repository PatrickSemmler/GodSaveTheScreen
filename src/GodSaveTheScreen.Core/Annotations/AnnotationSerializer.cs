using GodSaveTheScreen.Core.Project;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>
/// JSON für Objektlisten (Objekt-Zwischenablage). Nutzt denselben Serializer wie das Projektformat
/// (<see cref="ProjectSerializer"/>); Diskriminatoren stehen in <see cref="Annotation"/> und dürfen nie geändert werden.
/// </summary>
public static class AnnotationSerializer
{
    public static string Serialize(IEnumerable<Annotation> annotations) => ProjectSerializer.SerializeAnnotations(annotations);

    /// <summary>Liest Objekte (unbekannte Typen werden übersprungen); die Objekte behalten ihre gespeicherte <see cref="Annotation.Id"/>.</summary>
    /// <exception cref="ProjectFormatException">Kein gültiges JSON.</exception>
    public static IReadOnlyList<Annotation> Deserialize(string json) => ProjectSerializer.DeserializeAnnotations(json);

    /// <summary>Deserialisiert und vergibt neue Ids (Einfügen).</summary>
    public static IReadOnlyList<Annotation> DeserializeAsCopies(string json) =>
        [.. Deserialize(json).Select(a => a.Clone())];
}
