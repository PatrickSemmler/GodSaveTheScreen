using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Markereigenschaften der Auswahl (gemischte Werte = <c>null</c>); ohne Auswahl die Standardwerte des Markerwerkzeugs
/// samt Startnummer. Enthält die Befehle zum Neunummerieren.
/// </summary>
public sealed partial class MarkerPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;

    public MarkerPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        ShapeChoices =
        [
            new Choice<MarkerShape>(Strings.Marker_Circle, MarkerShape.Circle),
            new Choice<MarkerShape>(Strings.Marker_Square, MarkerShape.Square),
            new Choice<MarkerShape>(Strings.Marker_Rounded, MarkerShape.Rounded),
        ];
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
    }

    public IReadOnlyList<Choice<MarkerShape>> ShapeChoices { get; }

    private IReadOnlyList<MarkerAnnotation> Markers => [.. _context.Selection.Items.OfType<MarkerAnnotation>()];

    private ToolBase? DefaultsTool => _context.Selection.IsEmpty && _activeTool() is MarkerTool tool ? tool : null;

    public bool IsApplicable => Markers.Count > 0 || DefaultsTool is not null;

    /// <summary>Die Nummer lässt sich nur an ausgewählten Markern ändern.</summary>
    public bool ShowNumber => DefaultsTool is null && Markers.Count > 0;

    public bool HasMarkers => _context.Document.Annotations.OfType<MarkerAnnotation>().Any();

    public int? Number
    {
        get => StyleEditing.TryCommon(Markers, m => m.Number, out var v) ? v : null;
        set
        {
            if (value is { } number && DefaultsTool is null)
            {
                StyleEditing.Apply(_context, nameof(Number), Markers, Math.Clamp(number, 0, 9999), m => m.Number, (m, v) => m.Number = v, Strings.Marker_Number);
                Refresh();
            }
        }
    }

    /// <summary>Nummer des ersten Markers (gilt für neue Marker und „Neu nummerieren“).</summary>
    public double? StartNumber
    {
        get => _context.Defaults.MarkerStartNumber;
        set
        {
            if (value is { } start)
            {
                _context.Defaults.MarkerStartNumber = (int)Math.Round(start);
                Refresh();
            }
        }
    }

    public MarkerShape? Shape
    {
        get => TryGet(m => m.Shape, s => s.Shape, out var v) ? v : null;
        set => SetNullable(nameof(Shape), value, m => m.Shape, (m, v) => m.Shape = v, (s, v) => s with { Shape = v });
    }

    public double? Size
    {
        get => TryGet(m => m.Size, s => s.Size, out var v) ? v : null;
        set => SetNullable(nameof(Size), value is { } size ? Math.Clamp(size, 8, 400) : null, m => m.Size, (m, v) => m.Size = v, (s, v) => s with { Size = v });
    }

    public ArgbColor? FillColor
    {
        get => TryGet(m => m.Fill, s => s.Fill, out var v) ? v : null;
        set => SetNullable(nameof(FillColor), value, m => m.Fill, (m, v) => m.Fill = v, (s, v) => s with { Fill = v });
    }

    public ArgbColor? TextColor
    {
        get => TryGet(m => m.TextColor, s => s.TextColor, out var v) ? v : null;
        set => SetNullable(nameof(TextColor), value, m => m.TextColor, (m, v) => m.TextColor = v, (s, v) => s with { TextColor = v });
    }

    public ArgbColor? OutlineColor
    {
        get => TryGet(m => m.Stroke.Color, s => s.Stroke.Color, out var v) ? v : null;
        set => SetNullable(nameof(OutlineColor), value, m => m.Stroke.Color, (m, v) => m.Stroke = m.Stroke with { Color = v }, (s, v) => s with { Stroke = s.Stroke with { Color = v } });
    }

    /// <summary>Nummeriert alle Marker lückenlos ab der Startnummer neu (ein Undo-Schritt).</summary>
    [RelayCommand]
    private void Renumber(MarkerOrder order)
    {
        var assignments = MarkerNumbering.Renumber(_context.Document.Annotations, order, _context.Defaults.MarkerStartNumber)
            .Where(a => a.Marker.Number != a.Number && !a.Marker.IsLocked)
            .ToList();
        if (assignments.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction(Strings.Marker_Renumber))
        {
            foreach (var (marker, number) in assignments)
            {
                var target = marker;
                _context.Undo.Execute(new PropertyChangeAction<int>(target, nameof(Number), target.Number, number, v => target.Number = v, Strings.Marker_Renumber));
            }
        }

        Refresh();
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    private bool TryGet<TV>(Func<MarkerAnnotation, TV> fromMarker, Func<MarkerStyle, TV> fromStyle, out TV value)
    {
        if (DefaultsTool is not null)
        {
            value = fromStyle(_context.Defaults.GetMarker());
            return true;
        }

        return StyleEditing.TryCommon(Markers, fromMarker, out value);
    }

    private void SetNullable<TV>(string key, TV? value, Func<MarkerAnnotation, TV> get, Action<MarkerAnnotation, TV> set, Func<MarkerStyle, TV, MarkerStyle> update)
        where TV : struct
    {
        if (value is not { } v)
        {
            return;
        }

        if (DefaultsTool is not null)
        {
            _context.Defaults.SetMarker(update(_context.Defaults.GetMarker(), v));
        }
        else
        {
            StyleEditing.Apply(_context, key, Markers, v, get, set);
        }

        Refresh();
    }
}
