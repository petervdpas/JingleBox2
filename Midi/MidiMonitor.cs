using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <summary>
/// A monitor of what a hand is doing: which keys are down and where the two wheels are held,
/// whatever is doing it.
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
/// The wheels go through it the same way and for the same reason: a drawn wheel is a picture of
/// the one under the hand, so it is watched here and passed on untouched. Nothing on a panel
/// writes one, which is the difference from a key: a key can be pressed on a drawn keyboard and
/// a wheel cannot be dragged on a drawn panel.
///
/// The two halves of a press and the two wheel positions are all it holds. What a note went on to
/// sound, and for how long, is a different question with a different answer, and it is not this
/// one.
///
/// Written from whichever thread the port delivers on and read by the drawing thread, so the set
/// is locked and handed out as a copy. It is a handful of notes: a copy is cheaper than making
/// everybody who reads it hold a lock.
/// </remarks>
public sealed class MidiMonitor : INoteTrigger, IWheels, IPlays, IMidiMonitor
{
    /// <summary>Whoever really plays the notes. Every one is passed on untouched.</summary>
    private readonly INoteTrigger _next;

    /// <summary>And whoever the wheels were going to, passed on the same way.</summary>
    private readonly IWheels _turning;

    /// <summary>The keys held down now, whatever put them there.</summary>
    private readonly HashSet<int> _down = new();

    /// <summary>Written from the port's thread and read from the drawing one.</summary>
    private readonly object _lock = new();

    /// <param name="next">
    /// Where the notes were going anyway. Left out for a monitor standing on its own, which is
    /// what a test wants and what a keyboard with nothing behind it gets.
    /// </param>
    /// <param name="turning">
    /// And where the wheels were going. Left out, the wheels are watched and reach nothing,
    /// which is the same arrangement and is what a monitor on its own has.
    /// </param>
    public MidiMonitor(INoteTrigger? next = null, IWheels? turning = null)
    {
        var nobody = new Nobody();

        _next = next ?? nobody;
        _turning = turning ?? nobody;
    }

    /// <summary>Nowhere for a note or a wheel to go, for a monitor standing on its own.</summary>
    private sealed class Nobody : INoteTrigger, IWheels
    {
        /// <inheritdoc/>
        public void TriggerNote(Note note, int volume) { }

        /// <inheritdoc/>
        public void ReleaseNote(Note note) { }

        /// <inheritdoc/>
        public void Bend(double lean) { }

        /// <inheritdoc/>
        public void Modulate(double amount) { }
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
    public event EventHandler? Moved;

    /// <inheritdoc/>
    /// <remarks>
    /// A plain field rather than a locked one. It is a double written by the port's thread and
    /// read by the drawing thread, and the worst either can see is the position from a
    /// millisecond ago, which is a wheel arriving one frame late and is exactly what a picture
    /// of a moving thing is anyway.
    /// </remarks>
    public double Lean { get; private set; }

    /// <inheritdoc cref="Lean"/>
    public double Amount { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// **Passed on before the onlookers are told**, and that order is the whole of what this
    /// class promises. Passing the message on is the contract; saying so to whatever is drawing
    /// a picture of it is a courtesy, and a courtesy may not cost the thing it is about. Told
    /// first, one listener that throws takes the sound with it: the note is not bent, and from a
    /// chair the wheel simply does nothing.
    ///
    /// Said only when something really moved, since a device holding a wheel still sends the
    /// same value over and over and what listens to this draws.
    /// </remarks>
    public void Bend(double lean)
    {
        bool moved = Lean != lean;

        Lean = lean;

        _turning.Bend(lean);

        if (moved) Moved?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>Passed on before the onlookers are told, for the reason <see cref="Bend(double)"/> gives.</remarks>
    public void Modulate(double amount)
    {
        bool moved = Amount != amount;

        Amount = amount;

        _turning.Modulate(amount);

        if (moved) Moved?.Invoke(this, EventArgs.Empty);
    }

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

    /// <inheritdoc cref="IPlays.Press"/>
    /// <remarks>
    /// The lights' face on the router, and a thin one: a light is a light whatever track the
    /// note was going to, so the track is read and not kept. What it is told is what somebody's
    /// hand is doing, which is the one thing a drawn keyboard draws.
    ///
    /// Nothing is passed on from here. A sink on the router is told by the router, and the
    /// router is what tells the others: passing it on as well would sound everything twice,
    /// which is the very fault the one road exists to end.
    /// </remarks>
    public void Press(int track, Note note, int volume) => Pressed(note.Semitone);

    /// <inheritdoc cref="IPlays.Let"/>
    /// <remarks>Both halves, for the reason <see cref="Press"/> gives.</remarks>
    public void Let(int track, Note note) => Released(note.Semitone);

    /// <inheritdoc cref="IPlays.Bend"/>
    /// <remarks>Where the wheel is, which is all a picture of one needs. See <see cref="Press"/>.</remarks>
    public void Bend(int track, double lean) => Turned(lean, Amount);

    /// <inheritdoc cref="IPlays.Modulate"/>
    /// <remarks>See <see cref="Bend(int, double)"/>.</remarks>
    public void Modulate(int track, double amount) => Turned(Lean, amount);

    /// <summary>Writes both wheels down and says so once if either moved.</summary>
    private void Turned(double lean, double amount)
    {
        bool moved = Lean != lean || Amount != amount;

        Lean = lean;
        Amount = amount;

        if (moved) Moved?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Said out loud, and that is not symmetry for its own sake. A key on the hardware is
    /// written down twice on its way here, by the wire and by the router; a key pressed on a
    /// drawn keyboard passes neither, so until this line existed a log taken while somebody was
    /// clicking keys showed nothing whatever and read as a keyboard that was never touched. The
    /// two are the commonest thing to have to tell apart.
    /// </remarks>
    public void Pressed(int semitone)
    {
        Log.Write(LogArea.Midi, () => "panel: key " + semitone + " pressed on a drawn keyboard");

        Hold(semitone, true);
    }

    /// <inheritdoc/>
    /// <remarks>Both halves, for the reason <see cref="Pressed"/> gives.</remarks>
    public void Released(int semitone)
    {
        Log.Write(LogArea.Midi, () => "panel: key " + semitone + " let go on a drawn keyboard");

        Hold(semitone, false);
    }

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
