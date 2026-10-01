using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands.Interfaces;

/// <summary>
/// The pattern commands that say what happens inside a line: each line is split into ticks, and
/// this says at which tick each thing a line asks for happens and what the commands add between.
/// </summary>
/// <remarks>
/// A module on top of the tracker rather than a part of it. The sequencer reads a line into
/// events and knows nothing about ticks; the player plays events at the moments it is told and
/// knows nothing about letters. This sits between the two, so a command is added by adding one
/// <see cref="ITickCommand"/> and neither side has to learn anything.
///
/// A line with no command in it comes back as its own events, all at tick nought, so a song that
/// uses none plays exactly as it did before this existed.
///
/// It keeps a memory per note column, which is why it is a seam rather than a function: which
/// note a column is sounding, so a command on a line with no note acts on the one still ringing,
/// and which columns an arpeggio left off their pitch, so the next line can put them back.
/// </remarks>
public interface ILineCommands
{
    /// <summary>How many ticks a line is split into.</summary>
    int TicksPerLine { get; }

    /// <summary>Forgets every column's memory, which is what a pass starting asks for.</summary>
    void Reset();

    /// <summary>
    /// What one line's events come to, each at the tick it happens on, in the order they happen.
    /// </summary>
    /// <param name="line">The line's events, as the sequencer read them.</param>
    IReadOnlyList<TickEvent> Ticks(IReadOnlyList<TrackerEvent> line);
}
