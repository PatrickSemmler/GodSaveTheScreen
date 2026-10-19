using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GodSaveTheScreen.App.Editor;
using GodSaveTheScreen.App.Editor.Tools;
using GodSaveTheScreen.App.Resources;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Emoji;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Eigenschaften der ausgewählten Emojis (Größe, Drehung, Spiegelung; gemischte Werte = <c>null</c>); ohne Auswahl die
/// Vorgaben des Emoji-Werkzeugs (Standardgröße, Deckkraft). Die Deckkraft ausgewählter Objekte steht im allgemeinen Panel.
/// </summary>
public sealed partial class EmojiPropertiesViewModel : ObservableObject, IDisposable
{
    private readonly EditorContext _context;
    private readonly Func<ToolBase?> _activeTool;

    public EmojiPropertiesViewModel(EditorContext context, Func<ToolBase?> activeTool)
    {
        _context = context;
        _activeTool = activeTool;
        _context.Selection.Changed += OnStateChanged;
        _context.Undo.Changed += OnStateChanged;
        _context.Emoji.Changed += OnStateChanged;
    }

    private IReadOnlyList<EmojiAnnotation> Emojis => [.. _context.Selection.Items.OfType<EmojiAnnotation>()];

    private bool IsDefaultsMode => _context.Selection.IsEmpty && _activeTool() is EmojiTool;

    /// <summary>Das Emoji-Panel (Auswahl, Hautton, Eigenschaften) ist sichtbar: Emoji-Werkzeug aktiv oder Emojis ausgewählt.</summary>
    public bool IsApplicable => Emojis.Count > 0 || IsDefaultsMode;

    public bool HasEmojis => Emojis.Count > 0;

    public bool ShowDefaults => IsDefaultsMode;

    /// <summary>Mindestens ein ausgewähltes Emoji kann mit der installierten Schrift nicht dargestellt werden.</summary>
    public bool HasPlaceholder => Emojis.Any(e => !EmojiRasterizer.IsSupported(e.Sequence));

    public string PlaceholderName => EmojiCatalog.Shared.Find(Emojis.FirstOrDefault(e => !EmojiRasterizer.IsSupported(e.Sequence))?.Sequence ?? string.Empty)?.GetName() ?? string.Empty;

    public double? Width
    {
        get => StyleEditing.TryCommon(Emojis, e => Math.Round(e.Rect.Normalize().Width, 1), out var v) ? v : null;
        set => Resize(value, null);
    }

    public double? Height
    {
        get => StyleEditing.TryCommon(Emojis, e => Math.Round(e.Rect.Normalize().Height, 1), out var v) ? v : null;
        set => Resize(null, value);
    }

    public double? Rotation
    {
        get => StyleEditing.TryCommon(Emojis, e => Math.Round(e.Rotation, 1), out var v) ? v : null;
        set
        {
            if (value is { } degrees)
            {
                StyleEditing.Apply(_context, nameof(Rotation), Emojis, RotationMath.Normalize(degrees), e => e.Rotation, (e, v) => e.Rotation = v, Strings.Emoji_Rotation);
                Refresh();
            }
        }
    }

    public bool FlipHorizontal
    {
        get => Emojis.Count > 0 && Emojis.All(e => e.FlipHorizontal);
        set => Flip(nameof(FlipHorizontal), value, e => e.FlipHorizontal, (e, v) => e.FlipHorizontal = v);
    }

    public bool FlipVertical
    {
        get => Emojis.Count > 0 && Emojis.All(e => e.FlipVertical);
        set => Flip(nameof(FlipVertical), value, e => e.FlipVertical, (e, v) => e.FlipVertical = v);
    }

    /// <summary>Standardgröße neuer Emojis (Werkzeug-Vorgabe).</summary>
    public double? DefaultSize
    {
        get => _context.Emoji.DefaultSize;
        set
        {
            if (value is { } size)
            {
                _context.Emoji.DefaultSize = size;
            }
        }
    }

    /// <summary>Deckkraft neuer Emojis in Prozent (Werkzeug-Vorgabe).</summary>
    public double? DefaultOpacityPercent
    {
        get => Math.Round(_context.Emoji.Opacity * 100);
        set
        {
            if (value is { } percent)
            {
                _context.Emoji.Opacity = Math.Clamp(percent, 0, 100) / 100;
            }
        }
    }

    public double DefaultOpacityValue
    {
        get => DefaultOpacityPercent ?? 100;
        set => DefaultOpacityPercent = Math.Round(value);
    }

    /// <summary>Setzt die Drehung der ausgewählten Emojis auf 0°.</summary>
    [RelayCommand]
    private void ResetRotation() => Rotation = 0;

    /// <summary>Macht die ausgewählten Emojis wieder quadratisch (Kantenlänge = längere Seite, Mitte bleibt).</summary>
    [RelayCommand]
    private void ResetAspect()
    {
        var targets = Emojis.Where(e => !e.IsLocked && Math.Abs(e.Rect.Normalize().Width - e.Rect.Normalize().Height) > 1e-6).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction(Strings.Emoji_ResetAspect))
        {
            foreach (var emoji in targets)
            {
                var target = emoji;
                var rect = target.Rect.Normalize();
                var side = Math.Max(rect.Width, rect.Height);
                var center = rect.Center;
                var squared = new RectD(center.X - (side / 2), center.Y - (side / 2), side, side);
                _context.Undo.Execute(new PropertyChangeAction<RectD>(target, nameof(EmojiAnnotation.Rect), target.Rect, squared, v => target.Rect = v, Strings.Emoji_ResetAspect));
            }
        }

        Refresh();
    }

    public void Refresh() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {
        _context.Selection.Changed -= OnStateChanged;
        _context.Undo.Changed -= OnStateChanged;
        _context.Emoji.Changed -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Ändert Breite und/oder Höhe (Mitte bleibt); gesperrte Objekte bleiben unverändert.</summary>
    private void Resize(double? width, double? height)
    {
        var requested = width ?? height;
        if (requested is not { } size)
        {
            return;
        }

        size = Math.Max(EmojiAnnotation.MinSize, size);
        var targets = Emojis.Where(e => !e.IsLocked).ToList();
        var changes = targets.Select(e => (Target: e, Old: e.Rect, New: Resized(e.Rect.Normalize(), width is not null ? size : null, height is not null ? size : null)))
            .Where(c => c.Old != c.New)
            .ToList();
        if (changes.Count == 0)
        {
            return;
        }

        using (_context.Undo.BeginTransaction(Strings.Emoji_Size))
        {
            foreach (var (emoji, old, resized) in changes)
            {
                var target = emoji;
                _context.Undo.Execute(new PropertyChangeAction<RectD>(target, nameof(EmojiAnnotation.Rect), old, resized, v => target.Rect = v, Strings.Emoji_Size));
            }
        }

        Refresh();
    }

    private static RectD Resized(RectD rect, double? width, double? height)
    {
        var w = width ?? rect.Width;
        var h = height ?? rect.Height;
        var center = rect.Center;
        return new RectD(center.X - (w / 2), center.Y - (h / 2), w, h);
    }

    private void Flip(string key, bool value, Func<EmojiAnnotation, bool> get, Action<EmojiAnnotation, bool> set)
    {
        StyleEditing.Apply(_context, key, Emojis, value, get, set, Strings.Emoji_Flip);
        Refresh();
    }
}
