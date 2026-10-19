using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>Eine Zelle im Emoji-Raster. Das Vorschaubild wird beim ersten Zugriff im Hintergrund gerastert.</summary>
public sealed class EmojiCellViewModel : ObservableObject
{
    /// <summary>Rastergröße der Vorschau in Pixeln (Zelle 32 DIP, bis 200 % Skalierung scharf).</summary>
    public const int PreviewPixels = 64;

    private static readonly SemaphoreSlim RenderGate = new(1, 1);

    private readonly EmojiToolState _state;
    private ImageSource? _preview;
    private bool _requested;

    public EmojiCellViewModel(EmojiInfo info, string sequence, EmojiToolState state)
    {
        Info = info;
        Sequence = sequence;
        _state = state;
    }

    public EmojiInfo Info { get; }

    /// <summary>Anzuzeigende Sequenz (mit Hautton).</summary>
    public string Sequence { get; }

    /// <summary>Name in der Oberflächensprache (Narrator).</summary>
    public string Name => Info.GetName();

    public bool IsFavorite => _state.IsFavorite(Info.Sequence);

    public string FavoriteMenuText => IsFavorite ? Strings.Emoji_RemoveFavorite : Strings.Emoji_AddFavorite;

    public ImageSource? Preview
    {
        get
        {
            if (_preview is null && !_requested)
            {
                _requested = true;
                _ = LoadPreviewAsync();
            }

            return _preview;
        }
    }

    public void RefreshFavorite()
    {
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteMenuText));
    }

    private async Task LoadPreviewAsync()
    {
        await RenderGate.WaitAsync();
        try
        {
            var buffer = await Task.Run(() => EmojiRasterizer.Render(Sequence, PreviewPixels));
            if (buffer is not null)
            {
                _preview = buffer.ToBitmapSource();
                OnPropertyChanged(nameof(Preview));
            }
        }
        finally
        {
            RenderGate.Release();
        }
    }
}

/// <summary>Kategorie der Emoji-Auswahl (Favoriten, Zuletzt verwendet oder eine Unicode-Gruppe).</summary>
public sealed record EmojiCategoryViewModel(string Key, string Name, string Glyph, int GroupIndex);

/// <summary>Hautton-Schaltfläche der Auswahl.</summary>
public sealed record SkinToneChoice(SkinTone Tone, string Name, Brush Swatch);

/// <summary>
/// Emoji-Auswahl im Eigenschaftenpanel: Suche (Entprellung ~150 ms), Kategorien, Zuletzt verwendet, Favoriten, Hautton.
/// Es werden nur Emojis angeboten, die die installierte Schrift als ein farbiges Zeichen darstellen kann.
/// </summary>
public sealed partial class EmojiPickerViewModel : ObservableObject, IDisposable
{
    public const string FavoritesKey = "favorites";
    public const string RecentKey = "recent";

    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    private static readonly string[] GroupGlyphs =
        ["\U0001F600", "\U0001F44B", "\U0001F43B", "\U0001F354", "\U0001F697", "⚽", "\U0001F4A1", "\U0001F523", "\U0001F3C1"];

    private readonly EditorContext _context;
    private readonly EmojiCatalog _catalog = EmojiCatalog.Shared;
    private readonly EmojiSearch _search = new(EmojiCatalog.Shared);
    private readonly Dictionary<string, EmojiCellViewModel> _cellCache = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _searchTimer;
    private HashSet<string>? _supported;
    private Task? _loading;

    public EmojiPickerViewModel(EditorContext context)
    {
        _context = context;
        _searchTimer = new DispatcherTimer { Interval = SearchDelay };
        _searchTimer.Tick += OnSearchTimer;
        SkinToneChoices =
        [
            Choice(SkinTone.None, Strings.Emoji_Tone_Default, "#FFC83D"),
            Choice(SkinTone.Light, Strings.Emoji_Tone_Light, "#F7D7C4"),
            Choice(SkinTone.MediumLight, Strings.Emoji_Tone_MediumLight, "#D8B094"),
            Choice(SkinTone.Medium, Strings.Emoji_Tone_Medium, "#BB9167"),
            Choice(SkinTone.MediumDark, Strings.Emoji_Tone_MediumDark, "#8E562E"),
            Choice(SkinTone.Dark, Strings.Emoji_Tone_Dark, "#613D30"),
        ];
        _context.Emoji.Changed += OnStateChanged;
        RebuildCategories();
    }

    public IReadOnlyList<SkinToneChoice> SkinToneChoices { get; }

    [ObservableProperty]
    private IReadOnlyList<EmojiCategoryViewModel> _categories = [];

    [ObservableProperty]
    private EmojiCategoryViewModel? _selectedCategory;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<EmojiCellViewModel> _cells = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private EmojiCellViewModel? _selectedCell;

    [ObservableProperty]
    private bool _isLoading = true;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoResults => !IsLoading && Cells.Count == 0;

    /// <summary>Name des markierten Emojis (Statuszeile).</summary>
    public string StatusText => SelectedCell?.Name ?? string.Empty;

    public SkinTone SelectedSkinTone
    {
        get => _context.Emoji.SkinTone;
        set => ApplySkinTone(value);
    }

    /// <summary>Prüft im Hintergrund, welche Emojis der Katalog darstellbar sind (einmalig beim ersten Öffnen), und füllt dann das Raster.</summary>
    public Task LoadAsync() => _loading ??= LoadCoreAsync();

    /// <summary>Wendet die Suche sofort an (statt nach der Entprellung).</summary>
    public void ApplyFilterNow()
    {
        _searchTimer.Stop();
        Rebuild();
    }

    /// <summary>
    /// Wählt das Emoji: Bei ausgewählten Emoji-Objekten wird deren Emoji ausgetauscht (ein Undo-Schritt),
    /// sonst gilt es für das nächste Einfügen per Klick auf die Zeichenfläche.
    /// </summary>
    [RelayCommand]
    private void Choose(EmojiCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        SelectedCell = cell;
        _context.Emoji.SelectedSequence = cell.Info.Sequence;
        if (SelectedEmojis().Count > 0)
        {
            Swap(cell);
        }
    }

    /// <summary>
    /// Enter / Doppelklick: tauscht das Emoji der Auswahl aus bzw. fügt es mittig in den sichtbaren Bildausschnitt ein.
    /// </summary>
    [RelayCommand]
    private void Insert(EmojiCellViewModel? cell)
    {
        cell ??= SelectedCell;
        if (cell is null)
        {
            return;
        }

        SelectedCell = cell;
        _context.Emoji.SelectedSequence = cell.Info.Sequence;
        if (SelectedEmojis().Count > 0)
        {
            Swap(cell);
            return;
        }

        InsertAtCenter(cell.Sequence);
    }

    /// <summary>Fügt das Emoji mittig in den sichtbaren Bildausschnitt ein (ein Undo-Schritt) und wählt es aus.</summary>
    public EmojiAnnotation InsertAtCenter(string sequence)
    {
        var image = _context.Document.BaseImage;
        var center = _context.VisibleCenterProvider?.Invoke() ?? new PointD(image.Width / 2.0, image.Height / 2.0);
        var emoji = _context.Emoji.Create(_context.Emoji.DefaultRect(center), sequence);
        _context.Undo.Execute(new AddAnnotationAction(_context.Document.Annotations, emoji, description: Strings.Emoji_Insert));
        _context.Selection.Set(emoji);
        _context.Emoji.AddRecent(sequence);
        return emoji;
    }

    [RelayCommand]
    private void ToggleFavorite(EmojiCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        _context.Emoji.ToggleFavorite(cell.Info.Sequence);
        cell.RefreshFavorite();
    }

    public void Dispose()
    {
        _searchTimer.Stop();
        _searchTimer.Tick -= OnSearchTimer;
        _context.Emoji.Changed -= OnStateChanged;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    partial void OnSelectedCategoryChanged(EmojiCategoryViewModel? value) => Rebuild();

    private static SkinToneChoice Choice(SkinTone tone, string name, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return new SkinToneChoice(tone, name, brush);
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        var supported = await Task.Run(() => _catalog.Emojis.Where(e => EmojiRasterizer.IsSupported(e.Sequence)).Select(e => e.Sequence).ToHashSet(StringComparer.Ordinal)).ConfigureAwait(true);
        _supported = supported;
        IsLoading = false;
        Rebuild();
    }

    private void OnSearchTimer(object? sender, EventArgs e) => ApplyFilterNow();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SelectedSkinTone));
        RebuildCategories();
    }

    private IReadOnlyList<EmojiAnnotation> SelectedEmojis() =>
        [.. _context.Selection.Items.OfType<EmojiAnnotation>().Where(e => !e.IsLocked)];

    private void Swap(EmojiCellViewModel cell)
    {
        var targets = SelectedEmojis();
        StyleEditing.Apply(_context, nameof(EmojiAnnotation.Sequence), targets, cell.Sequence, e => e.Sequence, (e, v) => e.Sequence = v, Strings.Emoji_Swap);
        _context.Emoji.AddRecent(cell.Sequence);
        _context.InvalidateInteraction();
    }

    private void ApplySkinTone(SkinTone tone)
    {
        _context.Emoji.SkinTone = tone;
        var targets = SelectedEmojis();
        if (targets.Count > 0)
        {
            using (_context.Undo.BeginTransaction(Strings.Emoji_SkinTone))
            {
                foreach (var emoji in targets)
                {
                    var target = emoji;
                    var changed = EmojiToolState.Resolve(target.Sequence, tone);
                    if (changed != target.Sequence)
                    {
                        _context.Undo.Execute(new PropertyChangeAction<string>(target, nameof(EmojiAnnotation.Sequence), target.Sequence, changed, v => target.Sequence = v, Strings.Emoji_SkinTone));
                    }
                }
            }
        }

        OnPropertyChanged(nameof(SelectedSkinTone));
        Rebuild();
    }

    private void RebuildCategories()
    {
        var state = _context.Emoji;
        var list = new List<EmojiCategoryViewModel>();
        if (state.Favorites.Count > 0)
        {
            list.Add(new EmojiCategoryViewModel(FavoritesKey, Strings.Emoji_Cat_Favorites, "★", -1));
        }

        if (state.Recent.Count > 0)
        {
            list.Add(new EmojiCategoryViewModel(RecentKey, Strings.Emoji_Cat_Recent, "\U0001F552", -1));
        }

        foreach (var group in _catalog.Groups)
        {
            list.Add(new EmojiCategoryViewModel(group.Key, GroupName(group), group.Index < GroupGlyphs.Length ? GroupGlyphs[group.Index] : "■", group.Index));
        }

        var key = SelectedCategory?.Key;
        Categories = list;

        // Auswahl beibehalten; verschwundene Kategorie (z. B. letzter Favorit entfernt) → erste Gruppe.
        var next = list.FirstOrDefault(c => c.Key == key) ?? list.FirstOrDefault(c => c.GroupIndex >= 0) ?? list.FirstOrDefault();
        if (!ReferenceEquals(next, SelectedCategory))
        {
            SelectedCategory = next;
        }
        else
        {
            Rebuild();
        }
    }

    private static string GroupName(EmojiGroup group) => Strings.ResourceManager.GetString("Emoji_Cat_" + group.Key) ?? group.Name;

    private void Rebuild()
    {
        if (_supported is null)
        {
            Cells = [];
            OnPropertyChanged(nameof(HasNoResults));
            return;
        }

        var tone = _context.Emoji.SkinTone;
        IEnumerable<EmojiCellViewModel> cells;
        if (IsSearching)
        {
            cells = _search.Find(SearchText).Where(e => _supported.Contains(e.Sequence)).Select(e => Cell(e, EmojiToolState.Resolve(e.Sequence, tone)));
        }
        else
        {
            cells = SelectedCategory?.Key switch
            {
                FavoritesKey => _context.Emoji.Favorites.Select(s => _catalog.Find(s)).OfType<EmojiInfo>().Where(e => _supported.Contains(e.Sequence)).Select(e => Cell(e, EmojiToolState.Resolve(e.Sequence, tone))),
                RecentKey => _context.Emoji.Recent.Select(s => (Info: _catalog.Find(s), Sequence: s)).Where(x => x.Info is not null && _supported.Contains(x.Info.Sequence)).Select(x => Cell(x.Info!, x.Sequence)),
                _ when SelectedCategory is { GroupIndex: >= 0 } category => _catalog.Emojis.Where(e => e.GroupIndex == category.GroupIndex && _supported.Contains(e.Sequence)).Select(e => Cell(e, EmojiToolState.Resolve(e.Sequence, tone))),
                _ => [],
            };
        }

        Cells = [.. cells];
        if (SelectedCell is not null && !Cells.Contains(SelectedCell))
        {
            SelectedCell = null;
        }

        OnPropertyChanged(nameof(HasNoResults));
    }

    private EmojiCellViewModel Cell(EmojiInfo info, string sequence)
    {
        if (!_cellCache.TryGetValue(sequence, out var cell))
        {
            cell = new EmojiCellViewModel(info, sequence, _context.Emoji);
            _cellCache[sequence] = cell;
        }

        return cell;
    }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(HasNoResults));
}
