using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <summary>
/// A monitor of the notes going past: which keys are down, whatever pressed them.
/// </summary>
/// <remarks>
/// One of these, wired to the note stream when the application starts and never taken off it.
/// That is the whole point: a picture of a keyboard is a picture of a keyboard, and it cannot be
/// right only while the page it is on happens to own the notes, or only while the cursor is on
/// the right track.
///
/// Every producer reaches it, and there are three. A key on the hardware arrives through
/// <see cref="INoteTrigger"/>, which this stands in front of and passes on untouched, so nothing
/// about what gets played goes through here. A mouse on a drawn key and a letter on the computer
/// keyboard say so themselves through <see cref="Pressed"/>, because the panel they are on sounds
/// them itself and forwarding those as well would sound everything twice.
///
/// The two halves of a press are all it holds. What a note went on to sound, and for how long, is
/// a different question with a different answer, and it is not this one.
///
/// Written from whichever thread the port delivers on and read by the drawing thread, so the set
/// is locked and handed out as a copy. It is a handful of notes: a copy is cheaper than making
/// everybody who reads it hold a lock.
/// </remarks>
public sealed class MidiMonitor : INoteTrigger, IMidiMonitor
{
    /// <summary>Whoever really plays the notes. Every one is passed on untouched.</summary>
    private readonly INoteTrigger _next;

    /// <summary>The keys held down now, whatever put them there.</summary>
    private readonly HashSet<int> _down = new();

    /// <summary>Written from the port's thread and read from the drawing one.</summary>
    private readonly object _lock = new();

    /// <param name="next">
    /// Where the notes were going anyway. Left out for a monitor standing on its own, which is
    /// what a test wants and what a keyboard with nothing behind it gets.
    /// </param>
    public MidiMonitor(INoteTrigger? next = null) => _next = next ?? new Nobody();

    /// <summary>Nowhere for a note to go, for a monitor standing on its own.</summary>
    private sealed class Nobody : INoteTrigger
    {
        /// <inheritdoc/>
        public void TriggerNote(Note note, int volume) { }

        /// <inheritdoc/>
        public void ReleaseNote(Note note) { }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A copy, taken under the lock. It is a handful of notes, and a copy is cheaper than making
    /// everybody who reads it hold a lock.
    /// </remarks>
    public IReadOnlyCollection<int> Down
    {
        get { lock (_lock) return new List<int>(_down); }
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    /// <remarks>Noted and passed on. Nothing about what gets played goes through here.</remarks>
    public void TriggerNote(Note note, int volume)
    {
        Hold(note.Semitone, true);

        _next.TriggerNote(note, volume);
    }

    /// <inheritdoc/>
    public void ReleaseNote(Note note)
    {
        Hold(note.Semitone, false);

        _next.ReleaseNote(note);
    }

    /// <inheritdoc/>
    public void Pressed(int semitone) => Hold(semitone, true);

    /// <inheritdoc/>
    public void Released(int semitone) => Hold(semitone, false);

    /// <inheritdoc/>
    public bool Holds(int semitone)
    {
        lock (_lock) return _down.Contains(semitone);
    }

    /// <summary>
    /// Puts a key down or takes it up, and says so only when something really moved.
    /// </summary>
    /// <remarks>
    /// Raised outside the lock, since what listens to it draws, and only on a real change: a
    /// device sending the same note on twice is ordinary, and a redraw per repeat is not.
    /// </remarks>
    private void Hold(int semitone, bool down)
    {
        bool moved;

        lock (_lock) moved = down ? _down.Add(semitone) : _down.Remove(semitone);

        if (moved) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Everything is up, for a device that has stopped saying so.
    /// </summary>
    /// <remarks>
    /// Nothing calls this yet. A cable pulled while a key is held leaves that key down for ever,
    /// and this is where the answer to that goes when there is one.
    /// </remarks>
    public void AllUp()
    {
        bool moved;

        lock (_lock)
        {
            moved = _down.Count > 0;
            _down.Clear();
        }

        if (moved) Changed?.Invoke(this, EventArgs.Empty);
    }
}
