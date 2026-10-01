using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary><c>Qxx</c>: whatever the cell asks for happens xx ticks into its line.</summary>
/// <remarks>
/// The whole cell is moved and not only a note, so a delayed OFF lets go late as well. A delay
/// past the end of the line is held to its last tick, since a note may not leak into the next
/// line where it would land on top of whatever is written there.
/// </remarks>
public sealed class NoteDelay : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Delay;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, VoiceState voice, int ticks, ICollection<TickEvent> into)
    {
        voice.Take(cell);

        into.Add(new TickEvent(Math.Clamp(cell.Effect.Parameter, 0, ticks - 1), cell));
    }
}
