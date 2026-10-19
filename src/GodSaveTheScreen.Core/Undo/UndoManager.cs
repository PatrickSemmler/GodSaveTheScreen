namespace GodSaveTheScreen.Core.Undo;

public interface IUndoableAction
{
    string Description { get; }

    void Do();

    void Undo();

    /// <summary>Fasst die folgende Aktion in diese zusammen (z. B. Schieberegler-Ziehen). <c>true</c> = übernommen.</summary>
    bool TryMerge(IUndoableAction next);
}

/// <summary>Mehrere Aktionen als ein Undo-Schritt.</summary>
public sealed class CompositeAction : IUndoableAction
{
    private readonly List<IUndoableAction> _children;

    public CompositeAction(string description, IEnumerable<IUndoableAction> children)
    {
        Description = description;
        _children = [.. children];
    }

    public string Description { get; }

    public int Count => _children.Count;

    public void Do()
    {
        foreach (var child in _children)
        {
            child.Do();
        }
    }

    public void Undo()
    {
        for (var i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Undo();
        }
    }

    public bool TryMerge(IUndoableAction next) => false;
}

/// <summary>
/// Undo/Redo-Verlauf. <see cref="Execute"/> führt die Aktion aus und legt sie ab.
/// Transaktionen fassen mehrere Aktionen zu einem Schritt zusammen.
/// </summary>
public sealed class UndoManager
{
    private const int NoSavePoint = -1;

    private readonly List<IUndoableAction> _undo = [];
    private readonly Stack<IUndoableAction> _redo = new();
    private List<IUndoableAction>? _transaction;
    private string _transactionDescription = string.Empty;
    private int _transactionDepth;
    private int _savePoint;

    public UndoManager(int limit = 200)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        Limit = limit;
    }

    public event EventHandler? Changed;

    public int Limit { get; }

    public bool CanUndo => _transactionDepth == 0 && _undo.Count > 0;

    public bool CanRedo => _transactionDepth == 0 && _redo.Count > 0;

    public string? UndoDescription => _undo.Count > 0 ? _undo[^1].Description : null;

    public string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

    public int UndoCount => _undo.Count;

    /// <summary>Der aktuelle Stand entspricht dem zuletzt gespeicherten (<see cref="MarkSaved"/>).</summary>
    public bool IsAtSavePoint => _transactionDepth == 0 && _savePoint == _undo.Count;

    public void MarkSaved()
    {
        _savePoint = _undo.Count;
        OnChanged();
    }

    public void Execute(IUndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action.Do();
        if (_transaction is not null)
        {
            _transaction.Add(action);
            return;
        }

        Push(action);
        OnChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var action = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        action.Undo();
        _redo.Push(action);
        OnChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var action = _redo.Pop();
        action.Do();
        _undo.Add(action);
        OnChanged();
    }

    /// <summary>Alle bis zum Verwerfen des Rückgabewerts ausgeführten Aktionen werden ein Undo-Schritt.</summary>
    public IDisposable BeginTransaction(string description)
    {
        if (_transactionDepth++ == 0)
        {
            _transaction = [];
            _transactionDescription = description;
        }

        return new TransactionScope(this);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _savePoint = NoSavePoint;
        OnChanged();
    }

    private void Push(IUndoableAction action)
    {
        if (_redo.Count > 0)
        {
            _redo.Clear();
            if (_savePoint > _undo.Count)
            {
                _savePoint = NoSavePoint;
            }
        }

        if (_undo.Count > 0 && _undo[^1].TryMerge(action))
        {
            // Der gemergte Stand weicht vom gespeicherten ab, obwohl die Anzahl gleich bleibt.
            if (_savePoint == _undo.Count)
            {
                _savePoint = NoSavePoint;
            }

            return;
        }

        _undo.Add(action);
        if (_undo.Count > Limit)
        {
            _undo.RemoveAt(0);
            _savePoint = _savePoint == NoSavePoint ? NoSavePoint : _savePoint - 1;
            if (_savePoint < 0)
            {
                _savePoint = NoSavePoint;
            }
        }
    }

    private void EndTransaction()
    {
        if (--_transactionDepth > 0)
        {
            return;
        }

        var actions = _transaction!;
        _transaction = null;
        if (actions.Count == 0)
        {
            return;
        }

        Push(actions.Count == 1 ? actions[0] : new CompositeAction(_transactionDescription, actions));
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class TransactionScope : IDisposable
    {
        private UndoManager? _owner;

        public TransactionScope(UndoManager owner) => _owner = owner;

        public void Dispose()
        {
            _owner?.EndTransaction();
            _owner = null;
        }
    }
}
