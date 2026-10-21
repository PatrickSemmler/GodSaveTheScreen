namespace GodSaveTheScreen.Core.Settings;

/// <summary>Abschnitts-Schlüssel des Eigenschaftenpanels → aufgeklappt, mit Wertgleichheit (damit <c>AppSettings</c> als Record vergleichbar bleibt).</summary>
public sealed class PanelSectionMap : Dictionary<string, bool>
{
    public PanelSectionMap()
    {
    }

    public PanelSectionMap(IDictionary<string, bool> source)
        : base(source)
    {
    }

    public override bool Equals(object? obj) =>
        obj is PanelSectionMap other && Count == other.Count && this.All(p => other.TryGetValue(p.Key, out var v) && v == p.Value);

    public override int GetHashCode() => Count;
}
