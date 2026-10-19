using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Zeile der Ebenenliste.</summary>
public sealed class LayerItem : System.ComponentModel.INotifyPropertyChanged, IDisposable
{
    private readonly ImageDocument? _document;

    public LayerItem(Annotation annotation, ImageDocument? document = null)
    {
        Annotation = annotation;
        _document = document;
        annotation.PropertyChanged += OnAnnotationChanged;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public Annotation Annotation { get; }

    public string DisplayName => AnnotationNames.DisplayName(Annotation);

    public bool IsVisible => Annotation.IsVisible;

    public bool IsLocked => Annotation.IsLocked;

    /// <summary>Das Objekt liegt vollständig außerhalb des Zuschnitts (bleibt im Modell, ist im Ergebnis nicht sichtbar).</summary>
    public bool IsOutsideCrop =>
        _document?.CropRect is { } crop && crop.ToRectD().Intersect(Annotation.VisualBounds).IsEmpty;

    /// <summary>Der Zuschnitt des Dokuments hat sich geändert.</summary>
    public void RefreshCrop() =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsOutsideCrop)));

    public void Dispose() => Annotation.PropertyChanged -= OnAnnotationChanged;

    private void OnAnnotationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Annotation.Name) or nameof(Annotation.IsVisible) or nameof(Annotation.IsLocked))
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
        }
        else if (_document?.CropRect is not null)
        {
            RefreshCrop();
        }
    }
}

/// <summary>
/// Ebenenliste in umgekehrter Reihenfolge (oben = vorne). Sichtbarkeit, Sperre, Umbenennen und Umsortieren
/// laufen über den <see cref="UndoManager"/>.
/// </summary>
public sealed partial class LayersViewModel : IDisposable
{
    private readonly EditorContext _context;

    public LayersViewModel(EditorContext context)
    {
        _context = context;
        _context.Document.Annotations.CollectionChanged += OnCollectionChanged;
        _context.Selection.Changed += OnSelectionChanged;
        _context.Document.PropertyChanged += OnDocumentChanged;
        Rebuild();
    }

    private void OnDocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageDocument.CropRect))
        {
            foreach (var item in Items)
            {
                item.RefreshCrop();
            }
        }
    }

    /// <summary>Die Auswahl des Dokuments hat sich geändert (die Liste gleicht ihre Markierung an).</summary>
    public event EventHandler? SelectionChanged;

    public IReadOnlyList<Annotation> SelectedAnnotations => _context.Selection.Items;

    public void SetSelection(IEnumerable<Annotation> annotations) => _context.Selection.Set(annotations);

    public ObservableCollection<LayerItem> Items { get; } = [];

    public void Dispose()
    {
        _context.Document.Annotations.CollectionChanged -= OnCollectionChanged;
        _context.Selection.Changed -= OnSelectionChanged;
        _context.Document.PropertyChanged -= OnDocumentChanged;
        foreach (var item in Items)
        {
            item.Dispose();
        }
    }

    [RelayCommand]
    private void ToggleVisibility(LayerItem? item)
    {
        if (item is null)
        {
            return;
        }

        var annotation = item.Annotation;
        _context.Undo.Execute(new PropertyChangeAction<bool>(annotation, nameof(Annotation.IsVisible), annotation.IsVisible, !annotation.IsVisible, v => annotation.IsVisible = v, "Sichtbarkeit"));
        if (!annotation.IsVisible)
        {
            _context.Selection.Set(_context.Selection.Items.Where(a => a != annotation));
        }
    }

    [RelayCommand]
    private void ToggleLock(LayerItem? item)
    {
        if (item is null)
        {
            return;
        }

        var annotation = item.Annotation;
        _context.Undo.Execute(new PropertyChangeAction<bool>(annotation, nameof(Annotation.IsLocked), annotation.IsLocked, !annotation.IsLocked, v => annotation.IsLocked = v, "Sperren"));
    }

    public void Rename(LayerItem item, string name)
    {
        var annotation = item.Annotation;
        var trimmed = name.Trim();
        if (trimmed == annotation.Name)
        {
            return;
        }

        _context.Undo.Execute(new PropertyChangeAction<string>(annotation, nameof(Annotation.Name), annotation.Name, trimmed, v => annotation.Name = v, "Umbenennen"));
    }

    /// <summary>Verschiebt ein Objekt an die Listenposition <paramref name="displayIndex"/> (0 = ganz oben/vorne).</summary>
    public void MoveTo(Annotation annotation, int displayIndex)
    {
        var list = _context.Document.Annotations;
        var order = list.ToList();
        var oldIndex = order.IndexOf(annotation);
        if (oldIndex < 0)
        {
            return;
        }

        order.RemoveAt(oldIndex);
        var targetIndex = Math.Clamp(order.Count - displayIndex, 0, order.Count);
        order.Insert(targetIndex, annotation);
        var action = new ReorderAction(list, order);
        if (!action.IsNoOp)
        {
            _context.Undo.Execute(action);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke(this, EventArgs.Empty);

    private void Rebuild()
    {
        foreach (var item in Items)
        {
            item.Dispose();
        }

        Items.Clear();
        foreach (var annotation in _context.Document.Annotations.Reverse())
        {
            Items.Add(new LayerItem(annotation, _context.Document));
        }
    }
}
