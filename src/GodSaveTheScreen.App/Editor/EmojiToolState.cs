using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Settings;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Editor;

/// <summary>
/// Zustand des Emoji-Werkzeugs: gewähltes Emoji, Hautton, Standardgröße und Deckkraft sowie „Zuletzt verwendet“ und Favoriten.
/// Alles außer dem gewählten Emoji liegt in den Einstellungen und überlebt einen Neustart.
/// </summary>
public sealed class EmojiToolState
{
    public const string FallbackSequence = "\U0001F600";

    private readonly ISettingsService _settings;
    private string? _selected;

    public EmojiToolState(ISettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    public static EmojiCatalog Catalog => EmojiCatalog.Shared;

    private EmojiStyle Style => _settings.Current.EmojiDefaults;

    /// <summary>Basis-Sequenz (ohne Hautton), die beim Klick auf die Zeichenfläche eingefügt wird. Ohne Wahl: zuletzt verwendetes, sonst 😀.</summary>
    public string SelectedSequence
    {
        get => _selected ?? (Recent.Count > 0 ? Catalog.GetBaseSequence(Recent[0]) : FallbackSequence);
        set
        {
            if (!string.IsNullOrEmpty(value) && value != _selected)
            {
                _selected = Catalog.GetBaseSequence(value);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public SkinTone SkinTone
    {
        get => Style.SkinTone;
        set
        {
            if (value != Style.SkinTone)
            {
                Save(Style with { SkinTone = value });
            }
        }
    }

    public double Opacity
    {
        get => Style.Opacity;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (Math.Abs(clamped - Style.Opacity) > 1e-9)
            {
                Save(Style with { Opacity = clamped });
            }
        }
    }

    /// <summary>Kantenlänge in Bildpixeln bei einem Klick ohne Aufziehen.</summary>
    public double DefaultSize
    {
        get => Style.DefaultSize;
        set
        {
            var clamped = Math.Clamp(value, EmojiAnnotation.MinSize, 2000);
            if (Math.Abs(clamped - Style.DefaultSize) > 1e-9)
            {
                Save(Style with { DefaultSize = clamped });
            }
        }
    }

    /// <summary>Zuletzt verwendete Emojis (höchstens <see cref="AppSettings.MaxRecentEmojis"/>), neueste zuerst.</summary>
    public IReadOnlyList<string> Recent => _settings.Current.EmojiRecent;

    public IReadOnlyList<string> Favorites => _settings.Current.EmojiFavorites;

    /// <summary>Das gewählte Emoji mit Hautton (nur, wenn die Variante darstellbar ist).</summary>
    public string EffectiveSequence => Resolve(SelectedSequence, SkinTone);

    /// <summary>Variante mit Hautton; ist sie mit der installierten Schrift nicht darstellbar, bleibt das Basis-Emoji.</summary>
    public static string Resolve(string sequence, SkinTone tone)
    {
        var variant = Catalog.ApplySkinTone(sequence, tone);
        return variant == sequence || EmojiRasterizer.IsSupported(variant) ? variant : Catalog.GetBaseSequence(sequence);
    }

    /// <summary>Trägt ein Emoji in „Zuletzt verwendet“ ein (nach vorn, ohne Dubletten, höchstens 30).</summary>
    public void AddRecent(string sequence)
    {
        if (string.IsNullOrEmpty(sequence))
        {
            return;
        }

        var list = new StringList(Recent);
        list.RemoveAll(s => s == sequence);
        list.Insert(0, sequence);
        if (list.Count > AppSettings.MaxRecentEmojis)
        {
            list.RemoveRange(AppSettings.MaxRecentEmojis, list.Count - AppSettings.MaxRecentEmojis);
        }

        _settings.Current = _settings.Current with { EmojiRecent = list };
        _ = _settings.SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsFavorite(string sequence) => Favorites.Contains(Catalog.GetBaseSequence(sequence));

    /// <summary>Schaltet den Favoritenstatus des Emojis (unabhängig vom Hautton) um.</summary>
    public void ToggleFavorite(string sequence)
    {
        var key = Catalog.GetBaseSequence(sequence);
        var list = new StringList(Favorites);
        if (!list.Remove(key))
        {
            list.Add(key);
        }

        _settings.Current = _settings.Current with { EmojiFavorites = list };
        _ = _settings.SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Neues Emoji-Objekt mit den Standardwerten (Deckkraft) im Rahmen <paramref name="rect"/>.</summary>
    public EmojiAnnotation Create(RectD rect, string? sequence = null) => new()
    {
        Sequence = sequence ?? EffectiveSequence,
        Rect = rect,
        Opacity = Opacity,
    };

    /// <summary>Quadrat mit der Standardgröße, mittig auf <paramref name="center"/>.</summary>
    public RectD DefaultRect(PointD center)
    {
        var size = DefaultSize;
        return new RectD(center.X - (size / 2), center.Y - (size / 2), size, size);
    }

    private void Save(EmojiStyle style)
    {
        _settings.Current = _settings.Current with { EmojiDefaults = style };
        _ = _settings.SaveAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
