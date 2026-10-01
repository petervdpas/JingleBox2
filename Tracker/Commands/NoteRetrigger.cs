using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary>
/// <c>Rxy</c>: the note is played again every y ticks, each time x sixteenths quieter than the
/// time before; x of nought keeps the level.
/// </summary>
/// <remarks>
/// On a line with no note it plays the one still ringing again, at the pitch it is heard at, which
/// is how a roll is written across several lines under one note. Every y ticks counting from the
/// start of the line, so <c>R04</c> plays at nought, four and eight. A y of nought is no
/// retrigger, and a column sounding nothing has nothing to play again.
/// </remarks>
public sealed class NoteRetrigger : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Retrigger;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, VoiceState voice, int ticks, ICollection<TickEvent> into)
    {
        voice.Take(cell);

        into.Add(new TickEvent(0, cell));

        int every = cell.Effect.Parameter & 0x0F;
        int fall = (cell.Effect.Parameter >> 4) & 0x0F;
        var note = voice.Sounding;

        if (every == 0 || !note.IsPlayable || every >= ticks) return;

        float gain = cell.Gain ?? 1f;
        float step = 1f - fall / 16f;

        for (int tick = every; tick < ticks; tick += every)
        {
            gain *= step;

            into.Add(new TickEvent(tick, cell with
            {
                Kind = TrackerEventKind.Trigger, Note = note, Gain = gain
            }));
        }

        voice.Struck(note);
    }
}
