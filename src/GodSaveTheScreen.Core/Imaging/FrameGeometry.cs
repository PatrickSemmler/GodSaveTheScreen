using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Core.Imaging;

/// <summary>Geometrie der Rahmeneffekte ohne WPF: Risskanten als Punktliste und Eckenradius.</summary>
public static class FrameGeometry
{
    /// <summary>
    /// Umriss von <paramref name="rect"/> im Uhrzeigersinn (oben, rechts, unten, links) als geschlossener Linienzug.
    /// Die Seiten in <paramref name="sides"/> sind ausgefranst: die Kante weicht nach innen um höchstens
    /// <paramref name="height"/> aus, die Zacken sind im Mittel <paramref name="width"/> breit. Das Muster ist allein durch
    /// Größe, Seiten, Zahnmaß und <paramref name="seed"/> bestimmt.
    /// </summary>
    public static IReadOnlyList<PointD> TornOutline(RectD rect, FrameSides sides, double height, double width, int seed)
    {
        rect = rect.Normalize();
        height = Math.Max(0, height);
        width = Math.Max(1, width);
        var points = new List<PointD>();

        // Eckpunkte im Uhrzeigersinn; jede Seite liefert ihre Punkte ohne den Endpunkt (der ist der Anfang der nächsten).
        var corners = new[]
        {
            new PointD(rect.X, rect.Y),
            new PointD(rect.Right, rect.Y),
            new PointD(rect.Right, rect.Bottom),
            new PointD(rect.X, rect.Bottom),
        };
        var flags = new[] { FrameSides.Top, FrameSides.Right, FrameSides.Bottom, FrameSides.Left };
        // Nach innen zeigende Richtung je Seite.
        var inward = new[] { new VectorD(0, 1), new VectorD(-1, 0), new VectorD(0, -1), new VectorD(1, 0) };

        for (var side = 0; side < 4; side++)
        {
            var from = corners[side];
            var to = corners[(side + 1) % 4];
            points.Add(from);
            if (!sides.HasFlag(flags[side]) || height <= 0)
            {
                continue;
            }

            var length = from.DistanceTo(to);
            if (length <= width)
            {
                continue;
            }

            var direction = new VectorD((to.X - from.X) / length, (to.Y - from.Y) / length);
            var step = 0;
            var position = 0.0;
            while (true)
            {
                position += width * (0.5 + Noise(seed, side, step, 0));
                if (position >= length - (width * 0.25))
                {
                    break;
                }

                // Zacken wechseln zwischen tief und flach, damit die Kante gezahnt statt wellig wirkt.
                var depth = height * (0.15 + (0.85 * Noise(seed, side, step, 1)));
                if (step % 2 == 1)
                {
                    depth *= 0.35;
                }

                points.Add(new PointD(
                    from.X + (direction.X * position) + (inward[side].X * depth),
                    from.Y + (direction.Y * position) + (inward[side].Y * depth)));
                step++;
            }
        }

        return points;
    }

    /// <summary>Eckenradius, höchstens die halbe kürzere Seite.</summary>
    public static double ClampRadius(RectD rect, double radius)
    {
        rect = rect.Normalize();
        return Math.Clamp(double.IsFinite(radius) ? radius : 0, 0, Math.Min(rect.Width, rect.Height) / 2);
    }

    /// <summary>Gleichverteilte Zahl in [0, 1), allein aus den Eingaben bestimmt (SplitMix64).</summary>
    private static double Noise(int seed, int side, int step, int channel)
    {
        unchecked
        {
            var x = (ulong)(uint)seed;
            x = (x * 0x9E3779B97F4A7C15UL) + ((ulong)(side + 1) * 0xBF58476D1CE4E5B9UL) + ((ulong)(step + 1) * 0x94D049BB133111EBUL) + ((ulong)(channel + 1) * 0xD6E8FEB86659FD93UL);
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }
}
