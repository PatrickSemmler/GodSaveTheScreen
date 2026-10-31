using System.Runtime.InteropServices;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Ocr;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using OcrLine = GodSaveTheScreen.Core.Ocr.OcrLine;
using OcrResult = GodSaveTheScreen.Core.Ocr.OcrResult;
using OcrWord = GodSaveTheScreen.Core.Ocr.OcrWord;
using Windows.Storage.Streams;

namespace GodSaveTheScreen.Ocr;

/// <summary>
/// Texterkennung über <c>Windows.Media.Ocr</c> (lokal, ohne Modelldateien; erkannt werden nur installierte Sprachpakete).
/// Lange Bilder werden in überlappenden Streifen erkannt (<see cref="OcrTiling"/>), kleine Schrift bekommt einen zweiten,
/// vergrößerten Lauf (<see cref="OcrImagePrep"/>). Läuft im Hintergrund und ist abbrechbar.
/// </summary>
public sealed class WindowsOcrService : IOcrService, IDisposable
{
    private readonly ILogger<WindowsOcrService>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, OcrEngine> _engines = new(StringComparer.OrdinalIgnoreCase);
    private OcrEngine? _automatic;

    public WindowsOcrService(ILogger<WindowsOcrService>? logger = null) => _logger = logger;

    /// <summary>Bei sehr kleiner Schrift zusätzlich vergrößert erkennen (Standard; abschaltbar für Messungen).</summary>
    public bool RetryScaled { get; init; } = true;

    public IReadOnlyList<OcrLanguage> AvailableLanguages
    {
        get
        {
            try
            {
                return [.. OcrEngine.AvailableRecognizerLanguages.Select(l => new OcrLanguage(l.LanguageTag, l.DisplayName))];
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
            {
                _logger?.LogWarning(ex, "Die installierten Erkennungssprachen konnten nicht gelesen werden.");
                return [];
            }
        }
    }

    public void Dispose() => _gate.Dispose();

    public async Task<OcrResult> RecognizeAsync(PixelBuffer image, string? languageTag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width < 1 || image.Height < 1)
        {
            return new OcrResult(languageTag, []);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var engine = GetEngine(languageTag) ?? throw new OcrUnavailableException();
            var tag = engine.RecognizerLanguage.LanguageTag;
            var lines = await Task.Run(() => RecognizeAllAsync(engine, image, cancellationToken), cancellationToken).ConfigureAwait(false);
            return new OcrResult(tag, lines);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<OcrLine>> RecognizeAllAsync(OcrEngine engine, PixelBuffer image, CancellationToken token)
    {
        var flat = OcrImagePrep.FlattenOnWhite(image);

        // Breiter als die Engine erlaubt: gleichmäßig verkleinern und die Rechtecke zurückrechnen.
        var fit = 1.0;
        var maxSide = OcrEngine.MaxImageDimension;
        if (flat.Width > maxSide)
        {
            fit = (double)maxSide / flat.Width;
            flat = OcrImagePrep.Resize(flat, (int)maxSide, Math.Max(1, (int)Math.Round(flat.Height * fit)));
        }

        var tiles = OcrTiling.Plan(flat.Height, (int)maxSide);
        var perTile = new List<IReadOnlyList<OcrLine>>(tiles.Count);
        foreach (var tile in tiles)
        {
            token.ThrowIfCancellationRequested();
            var part = tiles.Count == 1 ? flat : flat.Crop(new PixelRect(0, tile.Top, flat.Width, tile.Height));
            perTile.Add(await RecognizeTileAsync(engine, part, token).ConfigureAwait(false));
        }

        var lines = OcrTiling.Merge(tiles, perTile);
        return fit == 1 ? lines : [.. lines.Select(l => OcrImagePrep.Unscale(l, fit, fit))];
    }

    /// <summary>Ein Streifen: Originalgröße; bei sehr kleiner Schrift zusätzlich vergrößert, das bessere Ergebnis gilt.</summary>
    private async Task<IReadOnlyList<OcrLine>> RecognizeTileAsync(OcrEngine engine, PixelBuffer tile, CancellationToken token)
    {
        var first = await RecognizeOnceAsync(engine, tile, token).ConfigureAwait(false);
        var factor = RetryScaled ? OcrImagePrep.RetryFactor(first) : 0;

        // Ein vergrößerter Streifen darf die Höchstgröße der Erkennung nicht überschreiten.
        while (factor > 1 && (tile.Height * factor > OcrEngine.MaxImageDimension || tile.Width * factor > OcrEngine.MaxImageDimension))
        {
            factor--;
        }

        if (factor <= 1)
        {
            return first;
        }

        token.ThrowIfCancellationRequested();
        var scaled = OcrImagePrep.Resize(tile, tile.Width * factor, tile.Height * factor);
        var second = await RecognizeOnceAsync(engine, scaled, token).ConfigureAwait(false);
        if (!OcrImagePrep.PreferScaled(first, second))
        {
            return first;
        }

        return [.. second.Select(l => OcrImagePrep.Unscale(l, factor, factor))];
    }

    private static async Task<IReadOnlyList<OcrLine>> RecognizeOnceAsync(OcrEngine engine, PixelBuffer image, CancellationToken token)
    {
        using var bitmap = CreateBitmap(image);
        var result = await engine.RecognizeAsync(bitmap).AsTask(token).ConfigureAwait(false);
        var lines = new List<OcrLine>(result.Lines.Count);
        foreach (var line in result.Lines)
        {
            var words = new List<OcrWord>(line.Words.Count);
            foreach (var word in line.Words)
            {
                var r = word.BoundingRect;
                words.Add(new OcrWord(word.Text, new RectD(r.X, r.Y, r.Width, r.Height)));
            }

            if (words.Count == 0)
            {
                continue;
            }

            var left = words.Min(w => w.Bounds.X);
            var top = words.Min(w => w.Bounds.Y);
            var right = words.Max(w => w.Bounds.Right);
            var bottom = words.Max(w => w.Bounds.Bottom);
            lines.Add(new OcrLine(line.Text, new RectD(left, top, right - left, bottom - top), [.. words]));
        }

        return lines;
    }

    private static SoftwareBitmap CreateBitmap(PixelBuffer image)
    {
        using var writer = new DataWriter();
        writer.WriteBytes(image.Pixels.Length == image.Width * image.Height * 4 ? image.Pixels : image.Pixels[..(image.Width * image.Height * 4)]);
        return SoftwareBitmap.CreateCopyFromBuffer(writer.DetachBuffer(), BitmapPixelFormat.Bgra8, image.Width, image.Height, BitmapAlphaMode.Premultiplied);
    }

    /// <summary>
    /// Engine der gewünschten Sprache; leer, unbekannt oder inzwischen deinstalliert = Automatik über die Profilsprachen
    /// von Windows, danach die erste installierte Sprache. <c>null</c>, wenn keine Sprache installiert ist.
    /// </summary>
    private OcrEngine? GetEngine(string? languageTag)
    {
        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            // Nur vorhandene Engines merken: wird später ein Sprachpaket installiert, soll der nächste Aufruf es finden.
            if (!_engines.TryGetValue(languageTag, out var engine))
            {
                engine = CreateForTag(languageTag);
                if (engine is not null)
                {
                    _engines[languageTag] = engine;
                }
            }

            if (engine is not null)
            {
                return engine;
            }
        }

        _automatic ??= CreateAutomatic();
        return _automatic;
    }

    private OcrEngine? CreateForTag(string tag)
    {
        try
        {
            var installed = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => string.Equals(l.LanguageTag, tag, StringComparison.OrdinalIgnoreCase));
            return installed is null ? null : OcrEngine.TryCreateFromLanguage(installed);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Die Erkennung für {Language} konnte nicht erstellt werden.", tag);
            return null;
        }
    }

    private OcrEngine? CreateAutomatic()
    {
        try
        {
            if (OcrEngine.TryCreateFromUserProfileLanguages() is { } profile)
            {
                return profile;
            }

            return OcrEngine.AvailableRecognizerLanguages is { Count: > 0 } available && available[0] is { } any ? OcrEngine.TryCreateFromLanguage(any) : null;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            _logger?.LogWarning(ex, "Die automatische Erkennung konnte nicht erstellt werden.");
            return null;
        }
    }
}
