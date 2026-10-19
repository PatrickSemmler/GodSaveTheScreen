using System.Collections.ObjectModel;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Undo;

/// <summary>Zusammenfassen gleichartiger Aktionen nur innerhalb eines kurzen Zeitfensters (ein Ziehvorgang).</summary>
internal static class MergeWindow
{
    public const long Milliseconds = 1500;

    public static long Now => Environment.TickCount64;
}

public sealed class AddAnnotationAction : IUndoableAction
{
    private readonly IList<Annotation> _list;
    private readonly Annotation _item;
    private int _index;

    public AddAnnotationAction(IList<Annotation> list, Annotation item, int index = -1, string description = "Objekt hinzufügen")
    {
        _list = list;
        _item = item;
        _index = index;
        Description = description;
    }

    public string Description { get; }

    public void Do()
    {
        if (_index < 0 || _index > _list.Count)
        {
            _index = _list.Count;
        }

        _list.Insert(_index, _item);
    }

    public void Undo() => _list.Remove(_item);

    public bool TryMerge(IUndoableAction next) => false;
}

public sealed class RemoveAnnotationsAction : IUndoableAction
{
    private readonly IList<Annotation> _list;
    private readonly List<(int Index, Annotation Item)> _removed = [];

    public RemoveAnnotationsAction(IList<Annotation> list, IEnumerable<Annotation> items, string description = "Objekte löschen")
    {
        _list = list;
        Description = description;
        var set = items.ToHashSet();
        for (var i = 0; i < list.Count; i++)
        {
            if (set.Contains(list[i]))
            {
                _removed.Add((i, list[i]));
            }
        }
    }

    public string Description { get; }

    public bool IsEmpty => _removed.Count == 0;

    public void Do()
    {
        foreach (var (_, item) in _removed)
        {
            _list.Remove(item);
        }
    }

    public void Undo()
    {
        foreach (var (index, item) in _removed)
        {
            _list.Insert(Math.Min(index, _list.Count), item);
        }
    }

    public bool TryMerge(IUndoableAction next) => false;
}

/// <summary>Ändert eine einzelne Eigenschaft; aufeinanderfolgende Änderungen derselben Eigenschaft werden zusammengefasst.</summary>
public sealed class PropertyChangeAction<T> : IUndoableAction
{
    private readonly object _target;
    private readonly string _key;
    private readonly Action<T> _setter;
    private readonly T _oldValue;
    private T _newValue;
    private long _stamp = MergeWindow.Now;

    public PropertyChangeAction(object target, string key, T oldValue, T newValue, Action<T> setter, string description = "Eigenschaft ändern")
    {
        _target = target;
        _key = key;
        _oldValue = oldValue;
        _newValue = newValue;
        _setter = setter;
        Description = description;
    }

    public string Description { get; }

    public void Do() => _setter(_newValue);

    public void Undo() => _setter(_oldValue);

    public bool TryMerge(IUndoableAction next)
    {
        if (next is not PropertyChangeAction<T> other
            || !ReferenceEquals(_target, other._target)
            || _key != other._key
            || other._stamp - _stamp > MergeWindow.Milliseconds)
        {
            return false;
        }

        _newValue = other._newValue;
        _stamp = other._stamp;
        return true;
    }
}

/// <summary>Verschiebt Objekte um einen Vektor (z. B. Pfeiltasten); aufeinanderfolgende Schritte werden zusammengefasst.</summary>
public sealed class MoveAnnotationsAction : IUndoableAction
{
    private readonly IReadOnlyList<Annotation> _items;
    private VectorD _delta;
    private long _stamp = MergeWindow.Now;

    public MoveAnnotationsAction(IEnumerable<Annotation> items, VectorD delta, string description = "Verschieben")
    {
        _items = [.. items];
        _delta = delta;
        Description = description;
    }

    public string Description { get; }

    public void Do()
    {
        foreach (var item in _items)
        {
            item.Translate(_delta);
        }
    }

    public void Undo()
    {
        foreach (var item in _items)
        {
            item.Translate(new VectorD(-_delta.X, -_delta.Y));
        }
    }

    public bool TryMerge(IUndoableAction next)
    {
        if (next is not MoveAnnotationsAction other
            || other._stamp - _stamp > MergeWindow.Milliseconds
            || !_items.SequenceEqual(other._items))
        {
            return false;
        }

        _delta = new VectorD(_delta.X + other._delta.X, _delta.Y + other._delta.Y);
        _stamp = other._stamp;
        return true;
    }
}

/// <summary>Ersetzt den Gesamtzustand von Objekten (Ziehen, Skalieren, Textbearbeitung): vorher-/nachher-Kopien.</summary>
public sealed class ReplaceGeometryAction : IUndoableAction
{
    private readonly IReadOnlyList<(Annotation Target, Annotation Before, Annotation After)> _changes;

    public ReplaceGeometryAction(IEnumerable<(Annotation Target, Annotation Before, Annotation After)> changes, string description = "Ändern")
    {
        _changes = [.. changes];
        Description = description;
    }

    public string Description { get; }

    public void Do()
    {
        foreach (var (target, _, after) in _changes)
        {
            target.CopyFrom(after);
        }
    }

    public void Undo()
    {
        foreach (var (target, before, _) in _changes)
        {
            target.CopyFrom(before);
        }
    }

    public bool TryMerge(IUndoableAction next) => false;
}

/// <summary>Ändert die Reihenfolge der Objekte (Z-Reihenfolge).</summary>
public sealed class ReorderAction : IUndoableAction
{
    private readonly IList<Annotation> _list;
    private readonly IReadOnlyList<Annotation> _before;
    private readonly IReadOnlyList<Annotation> _after;

    public ReorderAction(IList<Annotation> list, IReadOnlyList<Annotation> after, string description = "Reihenfolge ändern")
    {
        _list = list;
        _before = [.. list];
        _after = [.. after];
        Description = description;
    }

    public string Description { get; }

    public bool IsNoOp => _before.SequenceEqual(_after);

    public void Do() => Apply(_after);

    public void Undo() => Apply(_before);

    public bool TryMerge(IUndoableAction next) => false;

    private void Apply(IReadOnlyList<Annotation> order)
    {
        if (_list is ObservableCollection<Annotation> observable)
        {
            for (var i = 0; i < order.Count; i++)
            {
                var current = observable.IndexOf(order[i]);
                if (current != i && current >= 0)
                {
                    observable.Move(current, i);
                }
            }

            return;
        }

        _list.Clear();
        foreach (var item in order)
        {
            _list.Add(item);
        }
    }
}
