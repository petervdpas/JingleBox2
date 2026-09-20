using JingleBox2.Diagnostics;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <summary>
/// Hands keyboard notes and the wheels beside them to whichever half of the app is in front:
/// the rack while you are building a sound, the pattern otherwise.
/// </summary>
/// <remarks>
/// The second of the three adapters. See <see cref="PadTriggerAdapter"/> for pads and
/// <see cref="ControlTargets"/> for knobs.
///
/// It takes two <see cref="IPlaysNotes"/> rather than the two view models, which is the whole
/// reason the awkward cases can be put a question to without a window: leaving the rack with a
/// finger still on a key, opening a song mid-chord, a device already holding a note when the
/// program starts. <c>Tests/NoteAdapterTests.cs</c> is that.
/// </remarks>
public sealed class TrackerNoteAdapter : INoteTrigger, IWheels
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

    /// <summary>The two halves as the application really has them, for the window to wire up.</summary>
    public TrackerNoteAdapter(TrackerViewModel tracker, RackViewModel rack)
        : this(tracker, rack, () => tracker.ShowsMachines || !tracker.HasInstruments)
    {
    }

    /// <summary>The same, told which half is in front rather than working it out from a window.</summary>
    /// <remarks>
    /// Asked of the page that is up, not of the page that exists. The designer lives inside
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

    /// <inheritdoc/>
    /// <remarks>
    /// Which half took it is written down here, under the note number, so the release can be
    /// sent after it rather than to whichever half happens to be in front by then.
    /// </remarks>
    public void TriggerNote(Note note, int volume)
    {
        var half = Half();

        lock (_lock) _sent[note.Semitone] = half;

        Said(note, "down", half, remembered: true);

        half.PlayMidiNote(note, volume);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// To whichever half was given the press. It used to be dropped for the rack, on the grounds
    /// that there is nothing there for a note-off to be written into. True, and beside the point:
    /// a key coming up is also the moment its light goes out and the moment the sound is let go
    /// of, both of which the rack has. Dropped, the two halves of one key press went to different
    /// places.
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

    /// <inheritdoc/>
    /// <remarks>
    /// To the half the hand is playing on, which is the half holding its keys. Asking which page
    /// is in front instead would send the bend somewhere the notes are not the moment a window
    /// opens under a held chord, and a bend delivered to the wrong half is a note left leaning
    /// with nothing that can straighten it: a wheel returning to rest would straighten the other
    /// half's notes rather than these.
    ///
    /// With nothing held it goes to the half in front, which is the same answer a release nobody
    /// remembers gets, and it is what makes a wheel set before a note is played mean anything at
    /// all.
    /// </remarks>
    public void Bend(double lean) => Wheels().Bend(lean);

    /// <inheritdoc/>
    /// <remarks>To the same half, and for the same reasons. See <see cref="Bend"/>.</remarks>
    public void Modulate(double amount) => Wheels().Modulate(amount);

    /// <summary>Which half is in front, asked at the moment it is needed and never cached.</summary>
    private IPlaysNotes Half() => _rackHasIt() ? _rack : _tracker;

    /// <summary>
    /// Which half the wheels belong to: whoever took the keys that are down, or the half in front.
    /// </summary>
    /// <remarks>
    /// Any one of the keys held answers, because they all went to the same place: a press is
    /// given to one half and every press since is given to whichever half was in front at the
    /// time, and the wheel is one hand on one keyboard. Two halves cannot be holding keys from
    /// this keyboard at once unless somebody changed page mid-chord, and then the older half is
    /// the one still sounding, which is the one a wheel is about.
    /// </remarks>
    private IWheels Wheels()
    {
        IPlaysNotes? playing = null;

        lock (_lock)
        {
            foreach (var half in _sent.Values)
            {
                playing = half;
                break;
            }
        }

        return (playing ?? Half()) as IWheels ?? Nowhere;
    }

    /// <summary>
    /// What a wheel reaches when the half it belongs to does not take wheels.
    /// </summary>
    /// <remarks>
    /// Both halves here do, and this is not a fallback so much as the absence of one: a half
    /// that has not been given wheels should leave the notes at the pitch they were played at
    /// rather than have the adapter invent somewhere else to send the bend. It exists so this
    /// can be handed a stand-in that only plays notes, which is what most of the tests are.
    /// </remarks>
    private static readonly IWheels Nowhere = new Unwheeled();

    /// <summary>A half with no wheels on it. See <see cref="Nowhere"/>.</summary>
    private sealed class Unwheeled : IWheels
    {
        /// <inheritdoc/>
        public void Bend(double lean)
        {
        }

        /// <inheritdoc/>
        public void Modulate(double amount)
        {
        }
    }

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
