using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary>
/// <c>Axy</c>: the note, the note plus x semitones and the note plus y, one a tick, round and
/// round, without the note being started again.
/// </summary>
/// <remarks>
/// The chip-tune chord: one voice moving fast enough to be heard as three. Said as a shift of
/// pitch rather than as new notes, so an envelope runs on under it; only a tick where the pitch
/// actually moves is said. It ends on whichever step the line's last tick reached, and the next
/// line puts the note back where it was played unless it carries on arpeggiating.
///
/// On a line with no note it moves the one still ringing. <c>A00</c> is nothing, which is what
/// every tracker has read it as.
/// </remarks>
public sealed class NoteArpeggio : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Arpeggio;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, Note sounding, int ticks, ICollection<TickEvent> into)
    {
        into.Add(new TickEvent(0, cell));

        if (cell.Effect.Parameter == 0 || !sounding.IsPlayable) return;

        int[] steps = { 0, (cell.Effect.Parameter >> 4) & 0x0F, cell.Effect.Parameter & 0x0F };
        int was = 0;

        for (int tick = 1; tick < ticks; tick++)
        {
            int shift = steps[tick % steps.Length];

            if (shift == was) continue;

            into.Add(new TickEvent(tick, cell with
            {
                Kind = TrackerEventKind.Shift, Note = sounding, Shift = shift
            }));

            was = shift;
        }
    }
}
