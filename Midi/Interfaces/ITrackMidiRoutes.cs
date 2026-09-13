using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What a song's tracks say about MIDI: which tracks a note on the wire belongs to, which ports
/// have to be open for them, and where a track's notes go.
/// </summary>
/// <remarks>
/// Rules over the strips and nothing else, so each can be asked without a port or a song file.
/// Port names are compared without regard to case and trimmed, like every other port name here.
/// </remarks>
public interface ITrackMidiRoutes
{
    /// <summary>
    /// Every track listening to this port and channel, lowest first.
    /// </summary>
    /// <remarks>
    /// Every one rather than the first, so one channel can play two tracks at once, which is a
    /// layered part rather than a mistake. A track that names no port listens on any port.
    /// </remarks>
    /// <param name="mix">The song's strips, or null.</param>
    /// <param name="device">The port the note arrived on.</param>
    /// <param name="channel">Its channel, counted from one.</param>
    IReadOnlyList<int> TracksFor(IReadOnlyList<TrackMix>? mix, string? device, int channel);

    /// <summary>
    /// The ports the tracks listen to by name, once each, in the order the tracks name them.
    /// </summary>
    /// <remarks>
    /// Only named ports: a track listening on any port hears the ones that are already open, since
    /// opening every port on the machine for it would be deciding somebody's SETTINGS for them.
    /// </remarks>
    /// <param name="mix">The song's strips, or null.</param>
    IReadOnlyList<string> InputPorts(IReadOnlyList<TrackMix>? mix);

    /// <summary>
    /// Where a track sends, or null where it sends nowhere.
    /// </summary>
    /// <remarks>Nowhere is a route with no channel, a route with no port, or a track past the end.</remarks>
    /// <param name="mix">The song's strips, or null.</param>
    /// <param name="track">The track, counted from nought.</param>
    TrackMidiRoute? OutFor(IReadOnlyList<TrackMix>? mix, int track);
}
