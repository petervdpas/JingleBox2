using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker;

namespace JingleBox2.Midi;

/// <summary>
/// Hands a note to every track listening on its port and channel, and says whether any was.
/// </summary>
/// <remarks>
/// The router in front of the keyboard's: a note a track claims belongs to that track, and one
/// nobody claims goes on to the cursor's track exactly as before. Knows the wire and the rule and
/// nothing about the application, which is on the far side of <see cref="ITrackNotes"/>.
///
/// The strips are asked for on every message rather than kept, since the song they belong to is
/// swapped whenever one is opened and a route changed a moment ago has to be in force.
/// </remarks>
/// <param name="notes">Where a claimed note goes.</param>
/// <param name="mix">The open song's strips, asked for per message.</param>
/// <param name="routes">The rules, defaulted to the real ones.</param>
/// <param name="wire">How a number on the wire becomes a note, defaulted to the real reading.</param>
public sealed class MidiTrackRouter(ITrackNotes notes, Func<IReadOnlyList<TrackMix>?> mix,
                                    ITrackMidiRoutes? routes = null, IMidiNoteInput? wire = null)
{
    private readonly ITrackNotes _notes = notes;

    private readonly Func<IReadOnlyList<TrackMix>?> _mix = mix;

    private readonly ITrackMidiRoutes _routes = routes ?? new TrackMidiRoutes();

    private readonly IMidiNoteInput _wire = wire ?? new MidiNoteInput();

    /// <summary>
    /// Plays or releases the note on every track listening for it, and answers true if there was one.
    /// </summary>
    /// <remarks>
    /// A note outside what a pattern can hold is not claimed, so it goes where it went before
    /// rather than vanishing because a track happened to listen to its channel.
    /// </remarks>
    /// <param name="msg">What arrived.</param>
    public bool Handle(MidiMessage msg)
    {
        if (msg is null || msg.Type != MidiMessageType.Note) return false;

        var tracks = _routes.TracksFor(_mix(), msg.Device, msg.Channel);
        if (tracks.Count == 0) return false;

        if (!_wire.TryNote(msg.Value, out var note)) return false;

        foreach (int track in tracks)
        {
            if (msg.IsOn) _notes.PressOnTrack(track, note, _wire.VolumeFor(msg.Data));
            else _notes.ReleaseOnTrack(track, note);
        }

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "track midi in: '" + msg.Device + "' ch" + msg.Channel + " " + (msg.IsOn ? "down " : "up ")
                + note + " to track " + string.Join(", ", tracks));

        return true;
    }
}
