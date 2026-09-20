using System;
using System.Collections.Generic;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <summary>
/// A track's MIDI out as something the router can tell: what it hears, it sends out of the port
/// that track plays to.
/// </summary>
/// <remarks>
/// The out half of the one road, and it is <c>PanelPlays</c> said again about a port: a
/// sink is a source's shape, so the thing that sends the bytes stands on the road beside the
/// thing that makes the sound rather than being a second call every caller has to remember.
///
/// It is what closes the hole that made this step worth doing. A note in the pattern goes out of
/// the track's port, and so does a note arriving on that track's own MIDI in, but a key played
/// on the keyboard under somebody's hand did not: the cursor's track sounded its instrument and
/// the port heard nothing whatever. So a track with a hardware synth on its out and no
/// instrument of its own was silent under the hands and played perfectly from the pattern, which
/// is one gesture answering two ways depending on what made it.
///
/// **A release is sent where its press went**, which for a track named outright is the same track
/// and for <see cref="MidiRouter.TheHand"/> need not be: the cursor moves, and a note-off aimed
/// at wherever the cursor has got to leaves a synth holding a key on the track the note really
/// started on. So where each of the hand's notes went is written down and the release follows
/// it. A track names itself and needs no such memory.
///
/// The wheels are deliberately not sent. A note has an end and a wheel does not, so a port left
/// holding a bend is a synth holding every note off its own pitch with nothing anywhere to
/// straighten it, and what has to be settled first is who straightens it when a route changes or
/// a song is opened. That is its own piece of work rather than a line here.
///
/// One of these for the tracker rather than one per ask, which the memory forces: a press and
/// the release after it are two events and whatever remembers where the first went has to still
/// be there for the second.
///
/// Called on whichever thread the event arrived on, which for a port is the MIDI thread and for
/// a mouse or a letter is the drawing one, so what is remembered is locked.
/// </remarks>
public sealed class PortPlays : IPlays
{
    /// <summary>Where the bytes go, asked for per event since it is settled after this is built.</summary>
    private readonly Func<ITrackMidiOut?> _port;

    /// <summary>The open song's strips, asked for per event so a route set a moment ago is in force.</summary>
    private readonly Func<IReadOnlyList<TrackMix>?> _mix;

    /// <summary>Which track the hand is playing on.</summary>
    private readonly Func<int> _hand;

    /// <summary>How a note becomes a velocity.</summary>
    private readonly IMidiNoteInput _wire;

    /// <summary>Guards <see cref="_took"/>.</summary>
    private readonly object _lock = new();

    /// <summary>Which track each of the hand's notes went to, so its release goes there too.</summary>
    private readonly Dictionary<int, int> _took = new();

    /// <param name="port">Where the bytes go, asked for per event; nothing is sent where there is none.</param>
    /// <param name="mix">The open song's strips, asked for per event.</param>
    /// <param name="hand">Which track the hand is on, for an event that names no track.</param>
    /// <param name="wire">How a volume becomes a velocity, defaulted to the real reading.</param>
    public PortPlays(Func<ITrackMidiOut?> port, Func<IReadOnlyList<TrackMix>?> mix, Func<int> hand,
                     IMidiNoteInput? wire = null)
    {
        _port = port;
        _mix = mix;
        _hand = hand;
        _wire = wire ?? new MidiNoteInput();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Its own voice number, well past any note column a pattern can have, so a key held by hand
    /// and the pattern playing the same track never end each other's notes.
    /// </remarks>
    public void Press(int track, Note note, int volume)
    {
        int on = Took(track, note);

        _port()?.NoteOn(_mix(), on, Voice(note), note, _wire.VelocityFor(volume));
    }

    /// <inheritdoc/>
    /// <remarks>To the track the press went to. See the remarks on the class.</remarks>
    public void Let(int track, Note note) => _port()?.NoteOff(Where(track, note), Voice(note));

    /// <inheritdoc/>
    /// <remarks>Nothing is sent. See the remarks on the class for why the wheels are not here.</remarks>
    public void Bend(int track, double lean)
    {
    }

    /// <inheritdoc/>
    /// <remarks>Nor this. See <see cref="Bend"/>.</remarks>
    public void Modulate(int track, double amount)
    {
    }

    /// <summary>Which track this event is really about, and for the hand's notes, written down.</summary>
    private int Took(int track, Note note)
    {
        if (track != MidiRouter.TheHand) return track;

        int on = _hand();

        lock (_lock) _took[note.Semitone] = on;

        return on;
    }

    /// <summary>
    /// And which track a release is about, which is wherever its press went.
    /// </summary>
    /// <remarks>
    /// A release nobody remembers goes where the hand is now, since that is what a device
    /// already holding a note when the application starts sends.
    /// </remarks>
    private int Where(int track, Note note)
    {
        if (track != MidiRouter.TheHand) return track;

        lock (_lock)
        {
            if (_took.Remove(note.Semitone, out int was)) return was;
        }

        return _hand();
    }

    /// <summary>The voice a note played by hand holds, which is its own and not a pattern column.</summary>
    private static int Voice(Note note) => TrackMidiOut.LiveVoices + note.Semitone;
}
