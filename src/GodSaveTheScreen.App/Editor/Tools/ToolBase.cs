using System.Windows.Input;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Settings;
using Wpf.Ui.Controls;

namespace GodSaveTheScreen.App.Editor.Tools;

/// <summary>Basis aller Editor-Werkzeuge. Positionen kommen in Bildpixeln.</summary>
public abstract class ToolBase
{
    protected ToolBase(EditorContext context) => Context = context;

    protected EditorContext Context { get; }

    /// <summary>Stabile ID (auch Schlüssel der gespeicherten Standardwerte).</summary>
    public abstract string Id { get; }

    /// <summary>Schlüssel in <c>Strings.resx</c> für den Anzeigenamen.</summary>
    public abstract string NameKey { get; }

    public abstract SymbolRegular Symbol { get; }

    /// <summary>Eigene Symbolgeometrie (24 × 24), wenn die Symbolschrift kein passendes Zeichen hat.</summary>
    public virtual string? IconPath => null;

    public string DisplayName => Strings.ResourceManager.GetString(NameKey) ?? NameKey;

    /// <summary>Tastenkürzel (ohne Modifikator); <see cref="Key.None"/> = keines.</summary>
    public virtual Key ShortcutKey => Key.None;

    public virtual Cursor Cursor => Cursors.Cross;

    /// <summary>In der Werkzeugleiste anzeigen (temporäre Werkzeuge wie die Pipette nicht).</summary>
    public virtual bool ShowInPalette => true;

    /// <summary>Das Eigenschaftenpanel bearbeitet ohne Auswahl die Standardwerte dieses Werkzeugs.</summary>
    public virtual bool HasStyleDefaults => false;

    /// <summary>Die Standardwerte sind ein <c>ShapeStyle</c> (Formwerkzeuge); Text und Marker haben eigene Stile.</summary>
    public virtual bool UsesShapeStyle => false;

    /// <summary>Werte, solange der Anwender keine eigenen Standardwerte gespeichert hat.</summary>
    public virtual ShapeStyle BuiltInStyle => new();

    public virtual void Activate()
    {
    }

    public virtual void Deactivate()
    {
    }

    public virtual void OnPointerDown(EditorPointerArgs args)
    {
    }

    public virtual void OnPointerMove(EditorPointerArgs args)
    {
    }

    public virtual void OnPointerUp(EditorPointerArgs args)
    {
    }

    /// <summary>Tastendruck; <c>true</c> = verarbeitet.</summary>
    public virtual bool OnKeyDown(Key key, ModifierKeys modifiers) => false;

    /// <summary>Bricht eine laufende Aktion ab (Esc, Werkzeugwechsel).</summary>
    public virtual void Cancel()
    {
    }
}
