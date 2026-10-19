using System.Reflection;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>
/// Basis aller Objekte auf dem Bild. Koordinaten und Maße in Bildpixeln.
/// Nach dem Hinzufügen zum Dokument nur noch über den <c>UndoManager</c> ändern.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RectangleAnnotation), "rect")]
[JsonDerivedType(typeof(EllipseAnnotation), "ellipse")]
[JsonDerivedType(typeof(TriangleAnnotation), "triangle")]
[JsonDerivedType(typeof(LineAnnotation), "line")]
[JsonDerivedType(typeof(PolygonAnnotation), "polygon")]
[JsonDerivedType(typeof(TextAnnotation), "text")]
[JsonDerivedType(typeof(MarkerAnnotation), "marker")]
public abstract partial class Annotation : ObservableObject
{
    private static readonly Dictionary<Type, PropertyInfo[]> CopyableProperties = [];

    [JsonInclude]
    public Guid Id { get; private set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private double _opacity = 1;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isLocked;

    /// <summary>Geometrische Ausdehnung (ohne Strichstärke).</summary>
    [JsonIgnore]
    public abstract RectD Bounds { get; }

    /// <summary>Zusätzlicher Rand um <see cref="Bounds"/> für Strichstärke, Spitzen und Kontur.</summary>
    [JsonIgnore]
    public virtual double VisualMargin => 0;

    [JsonIgnore]
    public RectD VisualBounds => Bounds.Inflate(VisualMargin, VisualMargin);

    /// <summary>Wendet eine Punktabbildung auf die gesamte Geometrie an (Verschieben, Skalieren).</summary>
    public abstract void MapPoints(Func<PointD, PointD> map);

    /// <summary>Trifft der Punkt das Objekt? <paramref name="tolerance"/> in Bildpixeln.</summary>
    public abstract bool HitTest(PointD point, double tolerance);

    public virtual void Translate(VectorD delta) => MapPoints(p => p + delta);

    /// <summary>Wird bei proportionalem Skalieren mit dem Faktor aufgerufen (z. B. Schriftgröße).</summary>
    public virtual void ScaleSizes(double factor)
    {
    }

    /// <summary>Tiefe Kopie mit neuer <see cref="Id"/>.</summary>
    public Annotation Clone()
    {
        var copy = (Annotation)Activator.CreateInstance(GetType())!;
        copy.CopyFrom(this);
        copy.Id = Guid.NewGuid();
        return copy;
    }

    /// <summary>Übernimmt alle veränderlichen Eigenschaften (außer <see cref="Id"/>) von einem Objekt desselben Typs.</summary>
    public void CopyFrom(Annotation source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.GetType() != GetType())
        {
            throw new ArgumentException("Typen stimmen nicht überein.", nameof(source));
        }

        foreach (var property in GetCopyableProperties(GetType()))
        {
            property.SetValue(this, property.GetValue(source));
        }
    }

    private static PropertyInfo[] GetCopyableProperties(Type type)
    {
        lock (CopyableProperties)
        {
            if (!CopyableProperties.TryGetValue(type, out var properties))
            {
                properties = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null && p.SetMethod is { IsPublic: true })
                    .ToArray();
                CopyableProperties[type] = properties;
            }

            return properties;
        }
    }
}
