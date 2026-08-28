using System;
using System.Collections.Generic;
using JingleBox2.Music.Interfaces;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class KeyRegions : IKeyRegions
{
    /// <inheritdoc cref="IKeyRegions.PianoLow"/>
    public const int PianoLow = 21;

    /// <inheritdoc cref="IKeyRegions.PianoHigh"/>
    public const int PianoHigh = 108;

    /// <inheritdoc cref="IKeyRegions.LowestKey"/>
    public const int LowestKey = 0;

    /// <inheritdoc cref="IKeyRegions.HighestKey"/>
    public const int HighestKey = 119;

    /// <inheritdoc/>
    int IKeyRegions.PianoLow => PianoLow;

    /// <inheritdoc/>
    int IKeyRegions.PianoHigh => PianoHigh;

    /// <inheritdoc/>
    int IKeyRegions.LowestKey => LowestKey;

    /// <inheritdoc/>
    int IKeyRegions.HighestKey => HighestKey;

    /// <inheritdoc/>
    public IReadOnlyList<(int Low, int High)> Split(int low, int high, int count)
    {
        if (count <= 0) return Array.Empty<(int, int)>();

        low = Math.Clamp(low, LowestKey, HighestKey);
        high = Math.Clamp(high, low, HighestKey);

        int span = high - low + 1;
        var regions = new List<(int, int)>(count);

        for (int i = 0; i < count; i++)
        {
            int from = low + span * i / count;

            int to = i == count - 1 ? high : low + span * (i + 1) / count - 1;

            regions.Add((from, Math.Max(from, to)));
        }

        return regions;
    }

    /// <inheritdoc/>
    public int Middle(int low, int high) => (low + high) / 2;
}
