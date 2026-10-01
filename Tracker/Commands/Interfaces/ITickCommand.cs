using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands.Interfaces;

/// <summary>One pattern command: the letter it answers to and what it does with a line.</summary>
/// <remarks>
/// Handed the event whose cell carries its letter and writes what that comes to across the ticks
/// of the line, the event itself included: a command that moves the note leaves it out of tick
/// nought, and one that adds to it puts it there first and adds after.
///
/// It is handed the column's <see cref="VoiceState"/> as well and keeps it true, since only the
/// command knows what it did to the voice. A command that plays its note the ordinary way says so
/// with <see cref="VoiceState.Take"/>; a glide is the one that does not, because its note is a
/// destination rather than a fresh start.
/// </remarks>
public interface ITickCommand
{
    /// <summary>The letter in the command column, upper case.</summary>
    char Letter { get; }

    /// <summary>Writes what the event comes to across one line, and keeps the voice true.</summary>
    /// <param name="cell">The event whose cell carries this command.</param>
    /// <param name="voice">What the column is sounding before this event is played.</param>
    /// <param name="ticks">How many ticks the line has.</param>
    /// <param name="into">Where the events go, each with its tick.</param>
    void Spread(TrackerEvent cell, VoiceState voice, int ticks, ICollection<TickEvent> into);
}
