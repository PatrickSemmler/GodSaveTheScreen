using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using GodSaveTheScreen.App.Editor;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Zeile der Tastenkürzel-Übersicht; <see cref="Automation"/> liest der Screenreader als ein Satz.</summary>
public sealed record ShortcutRow(string Category, string Name, string Keys)
{
    public string Automation => $"{Name}: {Keys}";
}

/// <summary>Gruppierte, filterbare Liste der Tastenkürzel.</summary>
public sealed partial class ShortcutsViewModel : ObservableObject
{
    private readonly ListCollectionView _view;

    public ShortcutsViewModel(IReadOnlyList<ShortcutEntry> entries)
    {
        Rows = [.. entries.Select(e => new ShortcutRow(e.Category, e.Name, e.Keys))];
        _view = new ListCollectionView(Rows);
        _view.GroupDescriptions!.Add(new PropertyGroupDescription(nameof(ShortcutRow.Category)));
    }

    public List<ShortcutRow> Rows { get; }

    public ICollectionView View => _view;

    [ObservableProperty]
    private string _filter = string.Empty;

    partial void OnFilterChanged(string value)
    {
        var text = value.Trim();
        _view.Filter = text.Length == 0
            ? null
            : row => row is ShortcutRow r
                && (r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                    || r.Keys.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                    || r.Category.Contains(text, StringComparison.CurrentCultureIgnoreCase));
    }
}
