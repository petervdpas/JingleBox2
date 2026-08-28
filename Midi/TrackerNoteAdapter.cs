using JingleBox2.Diagnostics;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;

namespace JingleBox2.Midi;

/// <summary>
/// Hands keyboard notes to whichever half of the app is in front: the rack while you are
/// building a sound, the pattern otherwise.
/// </summary>
public sealed class TrackerNoteAdapter : INoteTrigger
{
    private readonly IPlaysNotes _tracker;
    private readonly IPlaysNotes _rack;
    private readonly Func<bool> _rackHasIt;

    /// <summary>
    /// Which half took each key that is down, so its release can be sent after it.
    /// </summary>
    /// <remarks>
    /// A key press is one thing with two halves, and the half that hears the second must be the
    /// one that heard the first. Asked twice instead, the question can be answered differently
    /// the second time: leave the rack while a key is held, or open a song while one is down,
    /// and the release goes to the pattern while the rack is still holding the note and drawing
    /// its key lit. Nothing there is ever told the hand has gone, so the sound hangs and so does
    /// the light. Written from whichever thread the port delivers on, so it is locked.
    /// </remarks>
    private readonly Dictionary<int, IPlaysNotes> _sent = new();

    private readonly object _lock = new();

    public TrackerNoteAdapter(TrackerViewModel tracker, MachineRackViewModel rack)
        : this(tracker, rack, () => tracker.ShowsMachines || !tracker.HasInstruments)
    {
    }

    /// <summary>The same, told which half is in front rather than working it out from a window.</summary>
    /// <remarks>
    /// Asked of the page that is up, not of the page that exists. The machines page lives inside
    /// the tracker and is hidden rather than taken away when the pattern is in front, so a flag
    /// set when it was put together would stay set for the rest of the session and every note
    /// would go to the rack. A song with no instruments has nothing a note could mean, so it
    /// goes to the rack either way rather than sounding nothing.
    /// </remarks>
    public TrackerNoteAdapter(IPlaysNotes tracker, IPlaysNotes rack, Func<bool> rackHasIt)
    {
        _tracker = tracker;
        _rack = rack;
        _rackHasIt = rackHasIt;
    }

    public void TriggerNote(Note note, int volume)
    {
        var half = Half();

        lock (_lock) _sent[note.Semitone] = half;

        Said(note, "down", half, remembered: true);

        half.PlayMidiNote(note, volume);
    }

    /// <summary>
    /// A key coming up, to whichever half was given the press.
    /// </summary>
    /// <remarks>
    /// It used to be dropped for the rack, on the grounds that there is nothing there for a
    /// note-off to be written into. True, and beside the point: a key coming up is also the
    /// moment its light goes out and the moment the sound is let go of, both of which the rack
    /// has. Dropped, the two halves of one key press went to different places.
    ///
    /// A release for a key nobody remembers still goes somewhere, since it is what a device that
    /// was already holding a note when the program started sends, and telling the half in front
    /// about it costs nothing.
    /// </remarks>
    public void ReleaseNote(Note note)
    {
        IPlaysNotes? half;

        lock (_lock)
        {
            if (!_sent.Remove(note.Semitone, out half)) half = null;
        }

        var to = half ?? Half();

        Said(note, "up", to, remembered: half != null);

        to.ReleaseMidiNote(note);
    }

    private IPlaysNotes Half() => _rackHasIt() ? _rack : _tracker;

    /// <summary>
    /// Which half a key went to, for a log read while a key is hanging.
    /// </summary>
    /// <remarks>
    /// Whether the release was remembered is the whole of what such a log has to say: a release
    /// that goes where nobody was expecting it is a note left sounding and a key left lit.
    /// </remarks>
    private void Said(Note note, string what, IPlaysNotes half, bool remembered) =>
        Log.Write(LogArea.Midi, () =>
            "note " + what + " " + note + " to the " + (ReferenceEquals(half, _rack) ? "rack" : "pattern")
            + (what == "up" && !remembered ? ", which is not where any press of it went" : ""));
}
