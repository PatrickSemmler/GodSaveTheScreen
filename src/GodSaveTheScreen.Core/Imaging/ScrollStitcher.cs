namespace GodSaveTheScreen.Core.Imaging;

public enum StitchOutcome
{
    /// <summary>Der Inhalt hat sich verschoben (<see cref="StitchStep.Dy"/> &gt; 0: neue Zeilen; ≤ 0: Zurückscrollen, nichts Neues).</summary>
    Moved,

    /// <summary>Das Bild gleicht dem vorigen (Seitenende oder noch nicht gescrollt).</summary>
    NoMovement,

    /// <summary>Keine sichere Überlappung gefunden; der Zustand bleibt unverändert.</summary>
    NoOverlap,

    /// <summary>Die höchste erlaubte Höhe ist erreicht; das Ergebnis ist vollständig.</summary>
    LimitReached,
}

/// <summary>Ergebnis von <see cref="ScrollStitcher.Add"/>.</summary>
/// <param name="Outcome">Art des Ergebnisses.</param>
/// <param name="Dy">Verschiebung gegenüber dem bisherigen Ende des Ergebnisses in Zeilen.</param>
/// <param name="NewRows">Zeilen, die dem Ergebnis hinzugekommen sind.</param>
public readonly record struct StitchStep(StitchOutcome Outcome, int Dy = 0, int NewRows = 0)
{
    public static StitchStep NoMovement => new(StitchOutcome.NoMovement);

    public static StitchStep NoOverlap => new(StitchOutcome.NoOverlap);

    public static StitchStep Moved(int dy, int newRows) => new(StitchOutcome.Moved, dy, newRows);

    public static StitchStep LimitReached(int newRows = 0) => new(StitchOutcome.LimitReached, newRows, newRows);
}

public sealed record ScrollStitcherOptions
{
    /// <summary>Breite des Streifens am rechten Rand, der nie zum Vergleich dient und auf eine Bildlaufleiste geprüft wird (Pixel, bereits mit der Skalierung multipliziert).</summary>
    public int ScrollbarGuard { get; init; } = 32;

    /// <summary>Mindestzahl nicht einfarbiger Zeilen in der Überlappung.</summary>
    public int MinOverlapRows { get; init; } = 12;

    /// <summary>Anteil übereinstimmender Pixel in der Überlappung (0–1).</summary>
    public double MinMatch { get; init; } = 0.98;

    /// <summary>Erlaubte Abweichung je Farbkanal (Rauschen, Kantenglättung).</summary>
    public int Tolerance { get; init; } = 10;
}

/// <summary>
/// Setzt eine Folge von Bildschirmbildern, zwischen denen sich der Inhalt senkrecht verschiebt, zu einem langen Bild zusammen
/// (rein, ohne Bildschirm). Kopf- und Fußzeilen, die beim Scrollen stehen bleiben, erscheinen genau einmal; Spalten, die sich nie
/// ändern (Seitenleiste), und eine Bildlaufleiste am rechten Rand stören den Vergleich nicht.
/// </summary>
/// <remarks>
/// Die Bilder müssen gleich groß sein und dürfen nach <see cref="Add"/> nicht verändert werden. Alle Zugriffe auf Pixel laufen über
/// <see cref="Axis"/> (Zeile = Laufrichtung, Abtastwert = Position in der Zeile), damit eine waagerechte Variante später die
/// Achsen tauschen kann.
/// </remarks>
public sealed class ScrollStitcher
{
    private const int MaxSamples = 512;
    private const int SignatureBlocks = 16;
    private const int MaxChain = 6;
    private const int MinVotes = 4;
    private const int MaxHashCandidates = 10;
    private const int UniformTolerance = 10;
    private const double ColumnChangedRatio = 0.02;
    private const double ScrollbarBadRatio = 0.05;

    // Stufe 2 (unscharfe Abweichungen, z. B. Bruchteil-Scrollen oder Rauschen)
    private const int LooseTolerance = 40;
    private const double LooseMatch = 0.9;
    private const double LooseScore = 25;

    private readonly int _maxHeight;
    private readonly ScrollStitcherOptions _options;
    private readonly List<byte[]> _sections = [];

    private PixelBuffer? _last;
    private PixelBuffer? _previous;
    private Axis _axis;
    private int _width;
    private int _height;
    private int _guard;
    private int _stripRows;
    private byte[] _tail = [];
    private int _tailRows;
    private int _pendingStart;
    private int _header;
    private int _footer;
    private bool _haveEstimate;
    private int _pairs;
    private int _lastDy;
    private bool[] _changed = [];
    private int[] _badColumns = [];
    private int _badSamples;
    private bool _limit;

    public ScrollStitcher(int maxHeight, ScrollStitcherOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeight, 1);
        _maxHeight = maxHeight;
        _options = options ?? new ScrollStitcherOptions();
    }

    /// <summary>Breite der Bilder (0 vor dem ersten Bild).</summary>
    public int Width => _width;

    /// <summary>Zeilen, die das Ergebnis jetzt hätte (ohne Zuschnitt der Bildlaufleiste).</summary>
    public int ContentHeight => _last is null ? 0 : Math.Min(_maxHeight, _stripRows + (_height - _pendingStart));

    public int MaxHeight => _maxHeight;

    public bool LimitHit => _limit;

    /// <summary>Zeilen am oberen Rand, die beim Scrollen stehen bleiben (geschätzt).</summary>
    public int HeaderRows => _header;

    /// <summary>Zeilen am unteren Rand, die beim Scrollen stehen bleiben (geschätzt).</summary>
    public int FooterRows => _footer;

    /// <summary>Wahr, wenn feste Ränder erkannt wurden, aber noch wenige Bildpaare sie bestätigt haben.</summary>
    public bool FixedRegionsUncertain => (_header > 0 || _footer > 0) && _pairs < 3;

    /// <summary>Wahr, wenn mindestens zwei Bilder verbunden wurden.</summary>
    public bool HasOverlap => _pairs > 0;

    /// <summary>Höhe des Bereichs, der sich beim Scrollen bewegt; Grundlage für die Schrittgröße des Treibers.</summary>
    public int FrameHeightHint => _last is null ? 0 : Math.Max(1, _height - _header - _footer);

    /// <summary>Zeilen, die zuletzt neu hinzukamen (Schrittweite des Treibers); 0 vor der ersten Bewegung.</summary>
    public int LastStep => _lastDy;

    public StitchStep Add(PixelBuffer frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_last is null)
        {
            return Start(frame);
        }

        if (frame.Width != _width || frame.Height != _height)
        {
            throw new ArgumentException("Alle Bilder müssen gleich groß sein.", nameof(frame));
        }

        if (_limit)
        {
            return StitchStep.LimitReached();
        }

        if (NearlyIdentical(frame, _previous!))
        {
            return StitchStep.NoMovement;
        }

        var step = Match(frame);
        _previous = frame;
        return step;
    }

    /// <summary>Setzt das Ergebnis zusammen. Die Bildlaufleiste am rechten Rand wird abgeschnitten, sofern erkannt.</summary>
    public PixelBuffer Build()
    {
        if (_last is null)
        {
            throw new InvalidOperationException("Es wurde noch kein Bild hinzugefügt.");
        }

        var stride = _axis.LineStep;
        var pending = Math.Min(_height - _pendingStart, Math.Max(0, _maxHeight - _stripRows));
        var total = _stripRows + pending;
        var width = _width - CropRight();
        var rowBytes = width * PixelBuffer.BytesPerPixel;
        var result = new PixelBuffer(width, total);
        var row = 0;
        foreach (var section in _sections)
        {
            var rows = section.Length / stride;
            for (var i = 0; i < rows && row < total; i++, row++)
            {
                Buffer.BlockCopy(section, i * stride, result.Pixels, row * result.Stride, rowBytes);
            }
        }

        for (var i = 0; i < pending; i++, row++)
        {
            Buffer.BlockCopy(_last.Pixels, (_pendingStart + i) * stride, result.Pixels, row * result.Stride, rowBytes);
        }

        return result;
    }

    /// <summary>
    /// Verkleinerte Vorschau des bisherigen Ergebnisses (ganzzahlig abgetastet, ohne Zuschnitt der Bildlaufleiste); kostet nur
    /// so viel wie die Vorschau groß ist und eignet sich daher für eine laufende Anzeige.
    /// </summary>
    public PixelBuffer BuildPreview(int maxWidth, int maxHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHeight, 1);
        if (_last is null)
        {
            throw new InvalidOperationException("Es wurde noch kein Bild hinzugefügt.");
        }

        var total = ContentHeight;
        var factor = Math.Max(1, Math.Max((_width + maxWidth - 1) / maxWidth, (total + maxHeight - 1) / maxHeight));
        var width = Math.Max(1, _width / factor);
        var height = Math.Max(1, total / factor);
        var result = new PixelBuffer(width, height);
        var stride = _axis.LineStep;
        var section = 0;
        var sectionStart = 0;
        for (var y = 0; y < height; y++)
        {
            var source = Math.Min(total - 1, y * factor);
            byte[] data;
            int rowOffset;
            if (source < _stripRows)
            {
                while (section < _sections.Count && source >= sectionStart + (_sections[section].Length / stride))
                {
                    sectionStart += _sections[section].Length / stride;
                    section++;
                }

                data = _sections[section];
                rowOffset = (source - sectionStart) * stride;
            }
            else
            {
                data = _last.Pixels;
                rowOffset = (_pendingStart + source - _stripRows) * stride;
            }

            for (var x = 0; x < width; x++)
            {
                Buffer.BlockCopy(data, rowOffset + (x * factor * PixelBuffer.BytesPerPixel), result.Pixels, (y * result.Stride) + (x * PixelBuffer.BytesPerPixel), PixelBuffer.BytesPerPixel);
            }
        }

        return result;
    }

    private StitchStep Start(PixelBuffer frame)
    {
        _width = frame.Width;
        _height = frame.Height;
        _axis = new Axis(frame.Stride, PixelBuffer.BytesPerPixel, _height, _width);
        _guard = Math.Clamp(Math.Min(_options.ScrollbarGuard, _width / 8), 0, _width);
        _changed = new bool[_width];
        _badColumns = new int[_guard];
        _tail = new byte[frame.Stride * _height];
        _last = frame;
        _previous = frame;
        _pendingStart = 0;
        if (_height > _maxHeight)
        {
            _limit = true;
            return StitchStep.LimitReached(_maxHeight);
        }

        return StitchStep.Moved(0, _height);
    }

    private StitchStep Match(PixelBuffer frame)
    {
        var last = _last!;
        var (headerPair, footerPair) = FixedRows(last, frame);
        var header = _haveEstimate ? Math.Min(_header, headerPair) : headerPair;
        var footer = _haveEstimate ? Math.Min(_footer, footerPair) : footerPair;
        if (_height - header - footer < Math.Max(_options.MinOverlapRows * 2, _height / 10))
        {
            header = 0;
            footer = 0;
        }

        var changed = (bool[])_changed.Clone();
        MarkChangedColumns(last, frame, changed);

        var reference = _stripRows == 0 ? last.Pixels : _tail;
        var referenceRows = _stripRows == 0 ? _height - footer : _tailRows;
        var context = new MatchContext(this, frame.Pixels, reference, referenceRows, header, footer, BuildSamples(changed));

        // Bei sich wiederholenden Zeilen stimmen viele Versätze; der erwartete Schritt wird deshalb immer mit geprüft.
        var expectedOffset = context.ReferenceRows - (context.Height - context.Footer - ExpectedDy(context));
        var accepted = new List<(int O, double Weight)>();
        var hashCandidates = HashCandidates(context);
        if (hashCandidates.TrueForAll(c => c.Offset != expectedOffset))
        {
            hashCandidates.Add((expectedOffset, 0));
        }

        foreach (var (offset, votes) in hashCandidates)
        {
            if (Verify(context, offset, _options.Tolerance, _options.MinMatch, _options.MinOverlapRows))
            {
                accepted.Add((offset, votes));
            }
        }

        var loose = false;
        if (accepted.Count == 0)
        {
            var looseCandidates = SignatureCandidates(context);
            if (looseCandidates.TrueForAll(c => c.Offset != expectedOffset))
            {
                looseCandidates.Add((expectedOffset, LooseScore));
            }

            foreach (var (offset, score) in looseCandidates)
            {
                if (Verify(context, offset, LooseTolerance, LooseMatch, Math.Max(24, _options.MinOverlapRows * 2)))
                {
                    accepted.Add((offset, 1000 - score));
                    loose = true;
                }
            }
        }

        if (accepted.Count == 0)
        {
            return StitchStep.NoOverlap;
        }

        var best = Choose(context, accepted);
        _header = header;
        _footer = footer;
        _haveEstimate = true;
        _pairs++;
        _changed = changed;
        UpdateScrollbar(context, best, loose ? LooseTolerance : _options.Tolerance);

        var dy = _height - footer - (referenceRows - best);
        if (dy == 0)
        {
            return StitchStep.NoMovement;
        }

        if (dy < 0)
        {
            return StitchStep.Moved(dy, 0);
        }

        return Extend(frame, context, referenceRows - best, dy);
    }

    private StitchStep Extend(PixelBuffer frame, MatchContext context, int newStart, int dy)
    {
        if (_stripRows == 0)
        {
            AppendRows(_last!.Pixels, 0, _height - context.Footer);
        }

        var rows = Math.Min(dy, _maxHeight - _stripRows);
        var limit = rows < dy || _stripRows + rows + context.Footer > _maxHeight;
        AppendRows(frame.Pixels, newStart, rows);
        _last = frame;
        _pendingStart = newStart + rows;
        _lastDy = rows;
        if (limit)
        {
            _limit = true;
            return StitchStep.LimitReached(rows);
        }

        return StitchStep.Moved(dy, rows);
    }

    private void AppendRows(byte[] source, int firstRow, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var stride = _axis.LineStep;
        var section = new byte[count * stride];
        Buffer.BlockCopy(source, firstRow * stride, section, 0, section.Length);
        _sections.Add(section);
        _stripRows += count;

        if (count >= _height)
        {
            Buffer.BlockCopy(section, (count - _height) * stride, _tail, 0, _height * stride);
            _tailRows = _height;
            return;
        }

        var keep = Math.Min(_tailRows, _height - count);
        if (keep > 0)
        {
            Buffer.BlockCopy(_tail, (_tailRows - keep) * stride, _tail, 0, keep * stride);
        }

        Buffer.BlockCopy(section, 0, _tail, keep * stride, section.Length);
        _tailRows = keep + count;
    }

    private int Choose(MatchContext context, List<(int O, double Weight)> accepted)
    {
        if (accepted.Count == 1)
        {
            return accepted[0].O;
        }

        var expected = ExpectedDy(context);
        var best = accepted[0];
        var bestDistance = int.MaxValue;
        foreach (var candidate in accepted)
        {
            var distance = Math.Abs(DyOf(context, candidate.O) - expected);
            if (distance < bestDistance || (distance == bestDistance && candidate.Weight > best.Weight))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best.O;
    }

    private int ExpectedDy(MatchContext context) =>
        _lastDy > 0 ? _lastDy : (int)(0.7 * (_height - context.Header - context.Footer));

    private static int DyOf(MatchContext context, int offset) => context.Height - context.Footer - (context.ReferenceRows - offset);

    // ---------------------------------------------------------------- feste Ränder, Spalten

    private (int Header, int Footer) FixedRows(PixelBuffer previous, PixelBuffer current)
    {
        var header = 0;
        while (header < _height && RowsSame(previous, current, header))
        {
            header++;
        }

        var footer = 0;
        while (footer < _height - header && RowsSame(previous, current, _height - 1 - footer))
        {
            footer++;
        }

        return (header, footer);
    }

    private bool RowsSame(PixelBuffer a, PixelBuffer b, int row)
    {
        var width = _width - _guard;
        if (width <= 0)
        {
            return true;
        }

        var step = Math.Max(1, width / 256);
        var bad = 0;
        var total = 0;
        for (var x = 0; x < width; x += step, total++)
        {
            if (!Same(a.Pixels, _axis.At(row, x), b.Pixels, _axis.At(row, x), _options.Tolerance))
            {
                bad++;
            }
        }

        return bad * 100 <= total;
    }

    private void MarkChangedColumns(PixelBuffer previous, PixelBuffer current, bool[] changed)
    {
        var counts = new int[_width];
        var rows = 0;
        for (var row = 0; row < _height; row += 2, rows++)
        {
            for (var x = 0; x < _width; x++)
            {
                if (!Same(previous.Pixels, _axis.At(row, x), current.Pixels, _axis.At(row, x), _options.Tolerance))
                {
                    counts[x]++;
                }
            }
        }

        var threshold = Math.Max(1, (int)(rows * ColumnChangedRatio));
        for (var x = 0; x < _width; x++)
        {
            if (counts[x] >= threshold)
            {
                changed[x] = true;
            }
        }
    }

    private int[] BuildSamples(bool[] changed)
    {
        var limit = _width - _guard;
        var active = new List<int>();
        for (var x = 0; x < limit; x++)
        {
            if (changed[x])
            {
                active.Add(x);
            }
        }

        if (active.Count < Math.Max(8, limit / 20))
        {
            active.Clear();
            for (var x = 0; x < limit; x++)
            {
                active.Add(x);
            }
        }

        if (active.Count <= MaxSamples)
        {
            return [.. active];
        }

        var samples = new int[MaxSamples];
        for (var i = 0; i < MaxSamples; i++)
        {
            samples[i] = active[(int)((long)i * active.Count / MaxSamples)];
        }

        return samples;
    }

    // ---------------------------------------------------------------- Stufe 1: Zeilen-Hash

    private static List<(int Offset, int Votes)> HashCandidates(MatchContext context)
    {
        var head = new Dictionary<ulong, int>();
        var next = new int[context.ReferenceRows];
        for (var q = 0; q < context.ReferenceRows; q++)
        {
            if (context.ReferenceUniform[q])
            {
                continue;
            }

            next[q] = head.TryGetValue(context.ReferenceHash[q], out var previous) ? previous : -1;
            head[context.ReferenceHash[q]] = q;
        }

        var votes = new int[context.ReferenceRows + context.Height];
        for (var r = context.Header; r < context.Height - context.Footer; r++)
        {
            if (context.FrameUniform[r] || !head.TryGetValue(context.FrameHash[r], out var q))
            {
                continue;
            }

            var length = 0;
            for (var i = q; i >= 0 && length <= MaxChain; i = next[i])
            {
                length++;
            }

            if (length > MaxChain)
            {
                continue;
            }

            for (var i = q; i >= 0; i = next[i])
            {
                votes[i - r + context.Height]++;
            }
        }

        return Enumerable.Range(0, votes.Length)
            .Where(i => votes[i] >= MinVotes)
            .OrderByDescending(i => votes[i])
            .Take(MaxHashCandidates)
            .Select(i => (Offset: i - context.Height, Votes: votes[i]))
            .ToList();
    }

    // ---------------------------------------------------------------- Stufe 2: Korrelation der Zeilenhelligkeit

    private List<(int Offset, double Score)> SignatureCandidates(MatchContext context)
    {
        var minRows = Math.Max(24, _options.MinOverlapRows * 2);
        var frameSignature = Signatures(context, context.Frame, context.Header, context.Height - context.Footer, context.FrameUniform);
        var referenceSignature = Signatures(context, context.Reference, 0, context.ReferenceRows, null);

        var scores = new double[context.ReferenceRows + context.Height];
        Array.Fill(scores, double.MaxValue);
        for (var o = -context.Height + 1; o < context.ReferenceRows; o++)
        {
            var start = Math.Max(context.Header, -o);
            var end = Math.Min(context.Height - context.Footer, context.ReferenceRows - o);
            if (end - start < minRows || context.ReferenceRows - o < context.Header)
            {
                continue;
            }

            long sum = 0;
            var count = 0;
            for (var r = start; r < end; r += 2)
            {
                if (context.FrameUniform[r])
                {
                    continue;
                }

                count++;
                var a = r * SignatureBlocks;
                var b = (r + o) * SignatureBlocks;
                for (var k = 0; k < SignatureBlocks; k++)
                {
                    sum += Math.Abs(frameSignature[a + k] - referenceSignature[b + k]);
                }
            }

            if (count >= minRows / 2)
            {
                scores[o + context.Height] = (double)sum / (count * SignatureBlocks);
            }
        }

        var result = new List<(int, double)>();
        foreach (var index in Enumerable.Range(0, scores.Length).Where(i => scores[i] <= LooseScore).OrderBy(i => scores[i]))
        {
            var offset = index - context.Height;
            if (result.All(c => Math.Abs(c.Item1 - offset) >= 3))
            {
                result.Add((offset, scores[index]));
                if (result.Count == 5)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static int[] Signatures(MatchContext context, byte[] pixels, int from, int to, bool[]? skip)
    {
        var samples = context.Samples;
        var result = new int[context.Height * SignatureBlocks];
        for (var row = from; row < to; row++)
        {
            if (skip is not null && skip[row])
            {
                continue;
            }

            for (var block = 0; block < SignatureBlocks; block++)
            {
                var first = block * samples.Length / SignatureBlocks;
                var last = (block + 1) * samples.Length / SignatureBlocks;
                var sum = 0;
                for (var i = first; i < last; i++)
                {
                    var o = context.At(row, samples[i]);
                    sum += ((29 * pixels[o]) + (150 * pixels[o + 1]) + (77 * pixels[o + 2])) >> 8;
                }

                result[(row * SignatureBlocks) + block] = last > first ? sum / (last - first) : 0;
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- Prüfung

    private static bool Verify(MatchContext context, int offset, int tolerance, double minMatch, int minRows)
    {
        var start = Math.Max(context.Header, -offset);
        var end = Math.Min(context.Height - context.Footer, context.ReferenceRows - offset);
        if (end - start < minRows || context.ReferenceRows - offset < context.Header)
        {
            return false;
        }

        var rows = 0;
        long matches = 0;
        long total = 0;
        var samples = context.Samples;
        for (var r = start; r < end; r++)
        {
            if (context.FrameUniform[r])
            {
                continue;
            }

            rows++;
            for (var i = 0; i < samples.Length; i++)
            {
                total++;
                if (Same(context.Frame, context.At(r, samples[i]), context.Reference, context.At(r + offset, samples[i]), tolerance))
                {
                    matches++;
                }
            }
        }

        return rows >= minRows && matches >= total * minMatch;
    }

    private void UpdateScrollbar(MatchContext context, int offset, int tolerance)
    {
        if (_guard == 0)
        {
            return;
        }

        var start = Math.Max(context.Header, -offset);
        var end = Math.Min(context.Height - context.Footer, context.ReferenceRows - offset);
        if (end - start < _options.MinOverlapRows)
        {
            return;
        }

        _badSamples++;
        for (var c = 0; c < _guard; c++)
        {
            var x = _width - _guard + c;
            var bad = 0;
            var total = 0;
            for (var r = start; r < end; r += 2, total++)
            {
                if (!Same(context.Frame, context.At(r, x), context.Reference, context.At(r + offset, x), tolerance))
                {
                    bad++;
                }
            }

            if (bad > total * ScrollbarBadRatio)
            {
                _badColumns[c]++;
            }
        }
    }

    private int CropRight()
    {
        if (_badSamples == 0)
        {
            return 0;
        }

        for (var first = 0; first < _guard; first++)
        {
            if (_badColumns[first] * 2 < _badSamples)
            {
                continue;
            }

            var bad = 0;
            for (var c = first; c < _guard; c++)
            {
                if (_badColumns[c] * 2 >= _badSamples)
                {
                    bad++;
                }
            }

            if (bad * 2 >= _guard - first)
            {
                return _guard - first;
            }
        }

        return 0;
    }

    private bool NearlyIdentical(PixelBuffer a, PixelBuffer b)
    {
        var width = _width - _guard;
        var stepX = Math.Max(1, width / 128);
        long same = 0;
        long total = 0;
        for (var row = 0; row < _height; row += 2)
        {
            for (var x = 0; x < width; x += stepX, total++)
            {
                if (Same(a.Pixels, _axis.At(row, x), b.Pixels, _axis.At(row, x), _options.Tolerance))
                {
                    same++;
                }
            }
        }

        return total == 0 || same >= total * 0.998;
    }

    private static bool Same(byte[] a, int ia, byte[] b, int ib, int tolerance) =>
        Math.Abs(a[ia] - b[ib]) <= tolerance
        && Math.Abs(a[ia + 1] - b[ib + 1]) <= tolerance
        && Math.Abs(a[ia + 2] - b[ib + 2]) <= tolerance;

    /// <summary>Zugriffsschicht: <c>line</c> = Zeile in Laufrichtung, <c>sample</c> = Position quer dazu.</summary>
    private readonly record struct Axis(int LineStep, int SampleStep, int Lines, int Samples)
    {
        public int At(int line, int sample) => (line * LineStep) + (sample * SampleStep);
    }

    private sealed class MatchContext
    {
        public MatchContext(ScrollStitcher owner, byte[] frame, byte[] reference, int referenceRows, int header, int footer, int[] samples)
        {
            Owner = owner;
            Frame = frame;
            Reference = reference;
            ReferenceRows = referenceRows;
            Header = header;
            Footer = footer;
            Samples = samples;
            Height = owner._height;
            FrameHash = new ulong[Height];
            FrameUniform = new bool[Height];
            for (var r = header; r < Height - footer; r++)
            {
                Describe(frame, r, out FrameHash[r], out FrameUniform[r]);
            }

            ReferenceHash = new ulong[referenceRows];
            ReferenceUniform = new bool[referenceRows];
            for (var r = 0; r < referenceRows; r++)
            {
                Describe(reference, r, out ReferenceHash[r], out ReferenceUniform[r]);
            }
        }

        public ScrollStitcher Owner { get; }

        public byte[] Frame { get; }

        public byte[] Reference { get; }

        public int ReferenceRows { get; }

        public int Header { get; }

        public int Footer { get; }

        public int Height { get; }

        public int[] Samples { get; }

        public ulong[] FrameHash { get; }

        public bool[] FrameUniform { get; }

        public ulong[] ReferenceHash { get; }

        public bool[] ReferenceUniform { get; }

        public int At(int line, int sample) => Owner._axis.At(line, sample);

        private void Describe(byte[] pixels, int row, out ulong hash, out bool uniform)
        {
            hash = 14695981039346656037UL;
            uniform = true;
            var first = At(row, Samples[0]);
            for (var i = 0; i < Samples.Length; i++)
            {
                var o = At(row, Samples[i]);
                var b = pixels[o];
                var g = pixels[o + 1];
                var r = pixels[o + 2];
                hash = (hash ^ (uint)(b | (g << 8) | (r << 16))) * 1099511628211UL;
                if (uniform
                    && (Math.Abs(b - pixels[first]) > UniformTolerance
                        || Math.Abs(g - pixels[first + 1]) > UniformTolerance
                        || Math.Abs(r - pixels[first + 2]) > UniformTolerance))
                {
                    uniform = false;
                }
            }
        }
    }
}
