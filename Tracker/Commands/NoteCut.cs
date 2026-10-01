using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary><c>Cxx</c>: the column goes silent xx ticks into the line, with no release.</summary>
/// <remarks>
/// On a line with no note it cuts the one still ringing. A cut past the end of the line is not a
/// cut at all, since the line after it is where somebody would write one.
/// </remarks>
public sealed class NoteCut : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Cut;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, Note sounding, int ticks, ICollection<TickEvent> into)
    {
        into.Add(new TickEvent(0, cell));

        int at = cell.Effect.Parameter;

        if (at < 0 || at >= ticks) return;

        into.Add(new TickEvent(at, cell with { Kind = TrackerEventKind.Cut, Note = Note.Off }));
    }
}
