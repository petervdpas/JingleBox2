using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class MixLevels : IMixLevels
{

    /// <inheritdoc/>
    public bool AnySolo(IReadOnlyList<TrackMix>? mix)
    {
        if (mix is null) return false;

        foreach (var strip in mix)
        {
            if (strip is { Solo: true }) return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool IsAudible(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        if (strip == null) return true;

        if (strip.Mute) return false;

        return !AnySolo(mix) || strip.Solo;
    }

    /// <inheritdoc/>
    public float GainFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        if (!IsAudible(mix, track)) return 0f;

        var strip = StripFor(mix, track);
        return strip == null ? 1f : (float)Math.Clamp(strip.Volume, TrackMix.MinVolume, TrackMix.MaxVolume);
    }

    /// <inheritdoc/>
    public float? PanFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        return strip == null ? null : (float)Math.Clamp(strip.Pan, -1, 1);
    }

    /// <inheritdoc/>
    public double DuckFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount) =>
        KeyFor(mix, track, trackCount) == TrackMix.NoKey
            ? 0
            : Math.Clamp(StripFor(mix, track)!.Duck, TrackMix.MinDuck, TrackMix.MaxDuck);

    /// <inheritdoc/>
    public int KeyFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount)
    {
        var strip = StripFor(mix, track);
        if (strip == null || strip.Duck <= 0) return TrackMix.NoKey;

        int key = strip.DuckFrom;
        if (key < 0 || key >= trackCount || key == track) return TrackMix.NoKey;

        return key;
    }

    /// <inheritdoc/>
    public double DuckReleaseFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        return strip == null ? TrackMix.DefaultDuckReleaseMs : strip.DuckReleaseMs;
    }

    /// <summary>
    /// One track's strip, or null when there is not one.
    /// </summary>
    /// <remarks>
    /// A track with no strip is an ordinary state rather than a fault: the mix list is grown to
    /// match the track count when a song is normalised, and a track can be asked about before
    /// that has happened. Everything here answers with what a strip at its defaults would say.
    /// </remarks>
    private TrackMix? StripFor(IReadOnlyList<TrackMix>? mix, int track) =>
        mix != null && track >= 0 && track < mix.Count ? mix[track] : null;
}
