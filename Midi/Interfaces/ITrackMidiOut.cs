using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// A song's tracks sending their notes out of a MIDI port.
/// </summary>
/// <remarks>
/// A voice is a track and a number: the pattern uses the note column, and notes played into a
/// track live use numbers of their own past the columns, so the two never end each other's notes.
/// A note in a voice lasts until the voice plays the next one or is told to stop, which is how a
/// pattern column behaves, so starting a note ends the one before it first.
///
/// What was sent is remembered with the port and channel it went to, and a note is let go of
/// there, so a route changed while a note is held still releases it rather than leaving a synth
/// holding a key for ever.
///
/// Called from the tracker's clock thread and from the drawing thread, so what is held is locked.
/// </remarks>
public interface ITrackMidiOut
{
    /// <summary>
    /// Opens every port the tracks send to, before anything is played.
    /// </summary>
    /// <remarks>
    /// Opening a port can take tens of milliseconds and the first note would otherwise pay it on
    /// the clock thread, which is a late first note on every play.
    /// </remarks>
    /// <param name="mix">The song's strips, or null.</param>
    void Prepare(IReadOnlyList<TrackMix>? mix);

    /// <summary>Ends whatever this voice is holding and starts this note, where the track sends.</summary>
    /// <param name="mix">The song's strips, read now so a route set a moment ago is in force.</param>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="voice">The note column, or a live voice's own number.</param>
    /// <param name="note">The note; one with no MIDI number sends nothing.</param>
    /// <param name="velocity">1 to 127.</param>
    void NoteOn(IReadOnlyList<TrackMix>? mix, int track, int voice, Note note, int velocity);

    /// <summary>Lets go of whatever this voice is holding, where it was sent.</summary>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="voice">The note column, or a live voice's own number.</param>
    void NoteOff(int track, int voice);

    /// <summary>Lets go of every note still held, which is what stopping the transport means.</summary>
    void AllOff();
}
