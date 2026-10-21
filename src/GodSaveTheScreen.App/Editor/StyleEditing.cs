using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Undo;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Gemeinsame Hilfen der Eigenschaften-ViewModels: gemeinsame Werte lesen, Änderungen als ein Undo-Schritt setzen.</summary>
public static class StyleEditing
{
    /// <summary><c>true</c>, wenn alle Objekte denselben Wert haben (gemischte Werte → <c>false</c>).</summary>
    public static bool TryCommon<TA, TV>(IEnumerable<TA> items, Func<TA, TV> get, out TV value)
    {
        var values = items.Select(get).Distinct().Take(2).ToList();
        if (values.Count == 1)
        {
            value = values[0];
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Setzt den Wert an allen (nicht gesperrten) Zielen; mehrere Ziele werden ein Undo-Schritt.</summary>
    public static void Apply<TA, TV>(
        EditorContext context, string key, IEnumerable<TA> targets, TV value, Func<TA, TV> get, Action<TA, TV> set, string description = "Eigenschaft ändern")
        where TA : Annotation
    {
        var changed = targets.Where(t => !t.IsLocked && !EqualityComparer<TV>.Default.Equals(get(t), value)).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        using (context.Undo.BeginTransaction(description))
        {
            foreach (var target in changed)
            {
                var old = get(target);
                // Ändert der Wert die Größe eines gedrehten Objekts (z. B. Schriftgröße), bleibt die Lage im Bild erhalten.
                context.Undo.Execute(new PropertyChangeAction<TV>(target, key, old, value, v => RotationMath.PreserveFrame(target, () => set(target, v)), description));
            }
        }

        context.RaiseSelectionStyleEdited();
    }
}
