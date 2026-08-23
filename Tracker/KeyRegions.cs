using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// Sharing a stretch of keyboard out among a number of things that each want a piece of it.
/// </summary>
/// <remarks>
/// Two places want this and would otherwise each have their own arithmetic: dropping a handful
/// of recordings on a map and laying them out, and cutting one recording into slices. They are
/// the same question, and it has one right answer: contiguous stretches, none empty, no two
/// differing by more than a key, and the last one reaching the top so nothing above it is
/// silent.
/// </remarks>
public static class KeyRegions
{
    /// <summary>The keys of a piano, A0 to C8. What a sliced recording is laid across.</summary>
    public const int PianoLow = 21;

    /// <summary>The top of that same stretch.</summary>
    public const int PianoHigh = 108;

    /// <summary>The whole of what a note column can say.</summary>
    public const int LowestKey = 0;

    /// <summary>And the top of that.</summary>
    public const int HighestKey = 119;

    /// <summary>
    /// One stretch per piece, in order, covering everything from <paramref name="low"/> to
    /// <paramref name="high"/> with no gaps between them.
    /// </summary>
    public static IReadOnlyList<(int Low, int High)> Split(int low, int high, int count)
    {
        if (count <= 0) return Array.Empty<(int, int)>();

        low = Math.Clamp(low, LowestKey, HighestKey);
        high = Math.Clamp(high, low, HighestKey);

        int span = high - low + 1;
        var regions = new List<(int, int)>(count);

        for (int i = 0; i < count; i++)
        {
            int from = low + span * i / count;

            // The last one takes whatever the division left over rather than stopping short.
            int to = i == count - 1 ? high : low + span * (i + 1) / count - 1;

            regions.Add((from, Math.Max(from, to)));
        }

        return regions;
    }

    /// <summary>The key in the middle of a stretch, which is where a piece of it is rooted.</summary>
    /// <remarks>
    /// Rooting a zone in its own middle keeps the worst transposition down to half its width
    /// either way, which is the best any even split can do.
    /// </remarks>
    public static int Middle(int low, int high) => (low + high) / 2;
}
