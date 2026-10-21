using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Drehung ausgewählter Formen (Rechteck, Ellipse, Dreieck), Polygone, Freihandstriche und Texte (gemischte Werte = <c>null</c>).
/// Keine Werkzeug-Vorgabe: neue Objekte entstehen ungedreht. Emoji, Stempel und Sprechblase haben eigene Regler.
/// Änderungen laufen über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class RotationPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;

    public RotationPropertiesViewModel(EditorContext context)
    {
        _context = context;
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
    }

    public bool IsApplicable => Targets.Count > 0;

    /// <summary>Drehung in Grad (−180 bis 180).</summary>
    public double? Rotation
    {
        get => StyleEditing.TryCommon(Targets, a => Math.Round(Signed(((IRotatableAnnotation)a).Rotation), 1), out var v) ? v : null;
        set
        {
            if (value is not { } degrees || Targets.Count == 0)
            {
                return;
            }

            StyleEditing.Apply(
                _context,
                nameof(Rotation),
                Targets,
                RotationMath.Normalize(Math.Clamp(degrees, -180, 180)),
                a => ((IRotatableAnnotation)a).Rotation,
                (a, v) => ((IRotatableAnnotation)a).Rotation = v,
                Strings.Props_Rotation);
            Refresh();
        }
    }

    public double RotationValue
    {
        get => Rotation ?? 0;
        set => Rotation = Math.Round(value);
    }

    private IReadOnlyList<Annotation> Targets => [.. _context.Selection.Items.Where(Supports)];

    /// <summary>Objekte, deren Drehung dieser Abschnitt bearbeitet.</summary>
    public static bool Supports(Annotation annotation) =>
        annotation is BoxAnnotation or PolygonAnnotation or StrokeAnnotation or TextAnnotation;

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
    }

    private static double Signed(double degrees) => degrees > 180 ? degrees - 360 : degrees;

    [RelayCommand]
    private void ResetRotation() => Rotation = 0;

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();
}
