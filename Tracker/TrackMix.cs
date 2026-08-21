using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>One track's place in the mix: how loud, how wide, and whether it is heard at all.</summary>
public sealed class TrackMix
{
    public const double MinVolume = 0;

    /// <summary>Past unity, so a quiet track can be pushed up rather than everything else down.</summary>
    public const double MaxVolume = 2;

    public const double DefaultVolume = 1;

    public double Volume { get; set; } = DefaultVolume;

    /// <summary>-1 hard left, 0 centre, 1 hard right.</summary>
    public double Pan { get; set; }

    public bool Mute { get; set; }

    public bool Solo { get; set; }

    public TrackMix Clone() => new()
    {
        Volume = Volume,
        Pan = Pan,
        Mute = Mute,
        Solo = Solo
    };

    public void Clamp()
    {
        Volume = double.IsNaN(Volume) ? DefaultVolume : Math.Clamp(Volume, MinVolume, MaxVolume);
        Pan = double.IsNaN(Pan) ? 0 : Math.Clamp(Pan, -1, 1);
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

    private static TrackMix? StripFor(IReadOnlyList<TrackMix>? mix, int track) =>
        mix != null && track >= 0 && track < mix.Count ? mix[track] : null;
}
