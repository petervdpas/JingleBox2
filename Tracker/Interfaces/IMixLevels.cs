using JingleBox2.Tracker.Records;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What the mix adds up to. Solo is the awkward one: the moment any track is soloed, every
/// track that is not becomes silent, whatever its own fader says.
/// </summary>
/// <remarks>
/// Every answer here is given for a track that has no strip of its own, because that is an
/// ordinary state rather than a fault: the mix list is grown to match the track count when a
/// song is normalised, and a track can be asked about before that has happened. What comes back
/// is what a strip at its defaults would have said.
/// </remarks>
public interface IMixLevels
{
    /// <summary>True when anything at all is soloed, which is what silences everything else.</summary>
    /// <param name="mix">The song's strips. Nothing is a mix with nothing soloed.</param>
    bool AnySolo(IReadOnlyList<TrackMix>? mix);

    /// <summary>False when the track is muted, or when something else is soloed and it is not.</summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    bool IsAudible(IReadOnlyList<TrackMix>? mix, int track);

    /// <summary>The track's own level, or zero when it is not being heard.</summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    float GainFor(IReadOnlyList<TrackMix>? mix, int track);

    /// <summary>The track's placement, or null for a track with no strip of its own.</summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    float? PanFor(IReadOnlyList<TrackMix>? mix, int track);

    /// <summary>How far a track is ducked, or zero when nothing is keying it.</summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    /// <param name="trackCount">How many tracks the song has, which bounds the key.</param>
    double DuckFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount);

    /// <summary>
    /// The track that ducks this one, or <see cref="TrackMix.NoKey"/>. A track cannot key
    /// itself: it would pull itself down the moment it played, which is a gate, not a duck.
    /// </summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    /// <param name="trackCount">How many tracks the song has, which bounds the key.</param>
    int KeyFor(IReadOnlyList<TrackMix>? mix, int track, int trackCount);

    /// <summary>How long the track takes to come back up, in milliseconds.</summary>
    /// <param name="mix">The song's strips.</param>
    /// <param name="track">Which track is being asked about.</param>
    double DuckReleaseFor(IReadOnlyList<TrackMix>? mix, int track);
}
