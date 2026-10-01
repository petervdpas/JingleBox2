using System;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary>
/// What the commands module remembers about one note column: the note its voice was started on,
/// how far off that note it rests, and anything still going on from an earlier line.
/// </summary>
/// <remarks>
/// A voice is started on one note and every move of its pitch after that is a shift from that
/// note, since our own voices are moved where they stand rather than started again. So a glide
/// from C to E leaves the voice still on C, resting four semitones up, and an arpeggio after it
/// steps around E. <see cref="Sounding"/> is the note that is heard, which is what a command on a
/// line with no note acts on.
/// </remarks>
public sealed class VoiceState
{
    /// <summary>The note the voice was started on, or <see cref="Note.Empty"/> while it sounds nothing.</summary>
    public Note Base { get; private set; } = Note.Empty;

    /// <summary>How many semitones off <see cref="Base"/> the voice rests, a fraction partway through a glide.</summary>
    public float Rest { get; set; }

    /// <summary>Something an earlier line started that has not finished, such as a glide, or nothing.</summary>
    public ICarry? Carry { get; set; }

    /// <summary>Whether the column is sounding anything.</summary>
    public bool Sounds => Base.IsPlayable;

    /// <summary>The note that is heard, held to the keyboard, or <see cref="Note.Empty"/> for none.</summary>
    public Note Sounding => Sounds
        ? new Note(Math.Clamp(Base.Semitone + (int)MathF.Round(Rest), Note.MinSemitone, Note.MaxSemitone))
        : Note.Empty;

    /// <summary>
    /// What an event with no command of its own does to the voice: a note starts it afresh at its
    /// own pitch, and an OFF or a cut leaves it sounding nothing.
    /// </summary>
    /// <param name="cell">The event being played.</param>
    public void Take(TrackerEvent cell)
    {
        if (cell.Kind == TrackerEventKind.Trigger) Struck(cell.Note);
        else if (cell.Kind is TrackerEventKind.Stop or TrackerEventKind.Cut) Silenced();
    }

    /// <summary>The voice starts again on a note, at that note's own pitch.</summary>
    /// <param name="note">The note.</param>
    public void Struck(Note note)
    {
        Base = note;
        Rest = 0;
        Carry = null;
    }

    /// <summary>The voice sounds nothing.</summary>
    public void Silenced()
    {
        Base = Note.Empty;
        Rest = 0;
        Carry = null;
    }

    /// <summary>The event that holds this voice that many semitones off the note it was started on.</summary>
    /// <param name="cell">The event it is about, for its track, column and instrument.</param>
    /// <param name="shift">How far off, in semitones.</param>
    public TrackerEvent ShiftTo(TrackerEvent cell, float shift) =>
        cell with { Kind = TrackerEventKind.Shift, Note = Base, Shift = shift };
}
