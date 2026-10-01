using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands.Interfaces;

/// <summary>One pattern command: the letter it answers to and what it does with a line.</summary>
/// <remarks>
/// Handed the event whose cell carries its letter and writes what that comes to across the ticks
/// of the line, the event itself included: a command that moves the note leaves it out of tick
/// nought, and one that adds to it puts it there first and adds after.
/// </remarks>
public interface ITickCommand
{
    /// <summary>The letter in the command column, upper case.</summary>
    char Letter { get; }

    /// <summary>Writes what the event comes to across one line.</summary>
    /// <param name="cell">The event whose cell carries this command.</param>
    /// <param name="sounding">
    /// The note the column is sounding once this event is played, or <see cref="Note.Empty"/>
    /// for none: the event's own note where it starts one, otherwise the one still ringing.
    /// </param>
    /// <param name="ticks">How many ticks the line has.</param>
    /// <param name="into">Where the events go, each with its tick.</param>
    void Spread(TrackerEvent cell, Note sounding, int ticks, ICollection<TickEvent> into);
}
