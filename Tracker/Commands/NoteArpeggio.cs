using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary>
/// <c>Axy</c>: the note, the note plus x semitones and the note plus y, one a tick, round and
/// round, without the note being started again.
/// </summary>
/// <remarks>
/// The chip-tune chord: one voice moving fast enough to be heard as three. Said as a shift of
/// pitch rather than as new notes, so an envelope runs on under it; only a tick where the pitch
/// actually moves is said. It steps around the pitch the voice is heard at, so after a glide it
/// is built on where the glide arrived. It ends on whichever step the line's last tick reached,
/// and the next line puts the note back unless it carries on arpeggiating.
///
/// On a line with no note it moves the one still ringing. <c>A00</c> is nothing, which is what
/// every tracker has read it as.
/// </remarks>
public sealed class NoteArpeggio : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Arpeggio;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, VoiceState voice, int ticks, ICollection<TickEvent> into)
    {
        voice.Take(cell);

        into.Add(new TickEvent(0, cell));

        if (cell.Effect.Parameter == 0 || !voice.Sounds) return;

        int[] steps = { 0, (cell.Effect.Parameter >> 4) & 0x0F, cell.Effect.Parameter & 0x0F };
        float was = voice.Rest;

        for (int tick = 1; tick < ticks; tick++)
        {
            float shift = voice.Rest + steps[tick % steps.Length];

            if (shift == was) continue;

            into.Add(new TickEvent(tick, voice.ShiftTo(cell, shift)));

            was = shift;
        }
    }
}
