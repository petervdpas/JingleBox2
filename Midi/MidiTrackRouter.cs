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
/// Hands a note or a wheel to every track listening on its port and channel, and says whether
/// any was.
/// </summary>
/// <remarks>
/// The router in front of the keyboard's: a note a track claims belongs to that track, and one
/// nobody claims goes on to the cursor's track exactly as before. Knows the wire and the rule and
/// nothing about the application, which is on the far side of <see cref="IPlays"/>.
///
/// **A track names itself**, which is the whole of what this adds to the road: every other source
/// says <see cref="MidiRouter.TheHand"/> and means wherever the hand is, and a keyboard pointed at
/// track three means track three whatever the cursor is doing. One member answers both, where
/// there used to be a contract apiece for the two destinations.
///
/// The wheels are here as well as the keys because they are the same claim. A keyboard pointed
/// at track three is pointed at track three whole: bending its notes while the wheel beside them
/// reached the cursor's track instead would be one hand playing two tracks at once. Its own door
/// rather than a branch inside <see cref="Handle"/>, since what has first refusal differs: a
/// note is claimed before any job is applied, and a wheel is claimed only where nothing on the
/// desk was pointed at it.
///
/// The road and the strips are asked for on every message rather than kept, since the song they
/// belong to is swapped whenever one is opened and a route changed a moment ago has to be in
/// force, and what is on the road is settled after the window this runs in was built.
/// </remarks>
/// <param name="road">Where a claimed note or wheel goes, asked for per message.</param>
/// <param name="mix">The open song's strips, asked for per message.</param>
/// <param name="routes">The rules, defaulted to the real ones.</param>
/// <param name="wire">How a number on the wire becomes a note, defaulted to the real reading.</param>
/// <param name="turning">How a wheel is read off the wire, defaulted to the real reading.</param>
/// <param name="jobs">What each control on a desk is for, defaulted to the real rule.</param>
public sealed class MidiTrackRouter(Func<IPlays> road, Func<IReadOnlyList<TrackMix>?> mix,
                                    ITrackMidiRoutes? routes = null, IMidiNoteInput? wire = null,
                                    IMidiWheelInput? turning = null, IControlJobs? jobs = null)
{
    private readonly Func<IPlays> _road = road;

    private readonly Func<IReadOnlyList<TrackMix>?> _mix = mix;

    private readonly ITrackMidiRoutes _routes = routes ?? new TrackMidiRoutes();

    private readonly IMidiNoteInput _wire = wire ?? new MidiNoteInput();

    private readonly IMidiWheelInput _turning = turning ?? new MidiWheelInput();

    private readonly IControlJobs _jobs = jobs ?? new ControlJobs();

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

        var road = _road();

        foreach (int track in tracks)
        {
            if (msg.IsOn) road.Press(track, note, _wire.VolumeFor(msg.Data));
            else road.Let(track, note);
        }

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "track midi in: '" + msg.Device + "' ch" + msg.Channel + " " + (msg.IsOn ? "down " : "up ")
                + note + " to track " + string.Join(", ", tracks));

        return true;
    }

    /// <summary>
    /// Bends or modulates every track listening for it, and answers true if there was one.
    /// </summary>
    /// <remarks>
    /// A wheel is kept for as long as it is held, so unlike a note there is nothing here that
    /// can be missed: whatever the last message said is where the track stays until the next one
    /// says otherwise. Which is also why nothing is reset when a route changes. A track that
    /// stops listening keeps whatever bend it was last given, exactly as it keeps whatever note
    /// it was last sounding, and the wheel that put it there is still under the same hand.
    /// </remarks>
    /// <param name="msg">What arrived.</param>
    public bool Wheels(MidiMessage msg)
    {
        if (!_jobs.Turns(msg)) return false;

        var tracks = _routes.TracksFor(_mix(), msg.Device, msg.Channel);
        if (tracks.Count == 0) return false;

        bool bending = msg.Type == MidiMessageType.PitchBend;
        double value = bending ? _turning.LeanFor(msg.Data) : _turning.AmountFor(msg.Data);

        var road = _road();

        foreach (int track in tracks)
        {
            if (bending) road.Bend(track, value);
            else road.Modulate(track, value);
        }

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "track midi in: '" + msg.Device + "' ch" + msg.Channel + " "
                + (bending ? "bend " : "modulation ") + value.ToString("0.###")
                + " to track " + string.Join(", ", tracks));

        return true;
    }

    /// <summary>
    /// Whether any track took this message at all, whatever kind it was.
    /// </summary>
    /// <remarks>
    /// The one door <see cref="MidiDispatcher"/> asks, so that what a track claims is this
    /// class's answer rather than a list of kinds kept over there. A keyboard pointed at track
    /// three is pointed at it whole: its keys and the wheels beside them are one hand, and a
    /// claim that covered the notes alone would have the wheel reach the cursor's track instead.
    ///
    /// Both halves are tried rather than one being chosen by looking at the message, since each
    /// already refuses what is not its own: a note is not a wheel and a wheel is not a note.
    /// </remarks>
    /// <param name="msg">What arrived.</param>
    public bool Claim(MidiMessage msg) => Handle(msg) || Wheels(msg);
}
