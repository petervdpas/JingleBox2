using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>One track's place in the mix: how loud, how wide, and whether it is heard at all.</summary>
public sealed class TrackMix
{
    public const double MinVolume = 0;

    /// <summary>
    /// Past unity, so a quiet track can be pushed up rather than everything else down. Stored
    /// as an amplitude, because that is what the engine multiplies by; the fader reads it in
    /// decibels, where this is +6.
    /// </summary>
    public const double MaxVolume = 2;

    public const double DefaultVolume = 1;

    public double Volume { get; set; } = DefaultVolume;

    /// <summary>-1 hard left, 0 centre, 1 hard right.</summary>
    public double Pan { get; set; }

    public bool Mute { get; set; }

    public bool Solo { get; set; }

    /// <summary>No key track: this strip is not ducked by anything.</summary>
    public const int NoKey = -1;

    public const double MinDuck = 0;

    /// <summary>Full depth ducks to silence while the key track is at full scale.</summary>
    public const double MaxDuck = 1;

    public const double MinDuckReleaseMs = 20;
    public const double MaxDuckReleaseMs = 1000;
    public const double DefaultDuckReleaseMs = 150;

    /// <summary>How far this track is pushed down while the key track sounds. Zero is off.</summary>
    public double Duck { get; set; }

    /// <summary>The track that does the pushing, or <see cref="NoKey"/>.</summary>
    public int DuckFrom { get; set; } = NoKey;

    /// <summary>How long the track takes to come back up once the key stops.</summary>
    public double DuckReleaseMs { get; set; } = DefaultDuckReleaseMs;

    /// <summary>
    /// The effects on this track, saved with the song. Null for a track that has none, so a
    /// song file does not carry a row of empty chains.
    /// </summary>
    public Audio.Plugins.PluginChainConfig? Plugins { get; set; }

    public TrackMix Clone() => new()
    {
        Volume = Volume,
        Pan = Pan,
        Mute = Mute,
        Solo = Solo,
        Duck = Duck,
        DuckFrom = DuckFrom,
        DuckReleaseMs = DuckReleaseMs,
        Plugins = Plugins?.Clone()
    };

    public void Clamp()
    {
        Volume = double.IsNaN(Volume) ? DefaultVolume : Math.Clamp(Volume, MinVolume, MaxVolume);
        Pan = double.IsNaN(Pan) ? 0 : Math.Clamp(Pan, -1, 1);

        Duck = double.IsNaN(Duck) ? 0 : Math.Clamp(Duck, MinDuck, MaxDuck);

        DuckReleaseMs = double.IsNaN(DuckReleaseMs)
            ? DefaultDuckReleaseMs
            : Math.Clamp(DuckReleaseMs, MinDuckReleaseMs, MaxDuckReleaseMs);
    }
}

/// <summary>
/// What the mix adds up to. Solo is the awkward one: the moment any track is soloed, every
/// track that is not becomes silent, whatever its own fader says.
/// </summary>
public static class MixLevels
{
    public static bool AnySolo(IReadOnlyList<TrackMix>? mix)
    {
        if (mix is null) return false;

        foreach (var strip in mix)
        {
            if (strip is { Solo: true }) return true;
        }

        return false;
    }

    /// <summary>False when the track is muted, or when something else is soloed and it is not.</summary>
    public static bool IsAudible(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        if (strip == null) return true;

        if (strip.Mute) return false;

        return !AnySolo(mix) || strip.Solo;
    }

    /// <summary>The track's own level, or zero when it is not being heard.</summary>
    public static float GainFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        if (!IsAudible(mix, track)) return 0f;

        var strip = StripFor(mix, track);
        return strip == null ? 1f : (float)Math.Clamp(strip.Volume, TrackMix.MinVolume, TrackMix.MaxVolume);
    }

    /// <summary>The track's placement, or null for a track with no strip of its own.</summary>
    public static float? PanFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        return strip == null ? null : (float)Math.Clamp(strip.Pan, -1, 1);
    }

    /// <summary>How far a track is ducked, or zero when nothing is keying it.</summary>
    public static double DuckFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount) =>
        KeyFor(mix, track, trackCount) == TrackMix.NoKey
            ? 0
            : Math.Clamp(StripFor(mix, track)!.Duck, TrackMix.MinDuck, TrackMix.MaxDuck);

    /// <summary>
    /// The track that ducks this one, or <see cref="TrackMix.NoKey"/>. A track cannot key
    /// itself: it would pull itself down the moment it played, which is a gate, not a duck.
    /// </summary>
    public static int KeyFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount)
    {
        var strip = StripFor(mix, track);
        if (strip == null || strip.Duck <= 0) return TrackMix.NoKey;

        int key = strip.DuckFrom;
        if (key < 0 || key >= trackCount || key == track) return TrackMix.NoKey;

        return key;
    }

    /// <summary>How long the track takes to come back up, in milliseconds.</summary>
    public static double DuckReleaseFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        var strip = StripFor(mix, track);
        return strip == null ? TrackMix.DefaultDuckReleaseMs : strip.DuckReleaseMs;
    }

    private static TrackMix? StripFor(IReadOnlyList<TrackMix>? mix, int track) =>
        mix != null && track >= 0 && track < mix.Count ? mix[track] : null;
}
