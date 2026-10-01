using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <summary>
/// <c>Gxx</c>: the note already sounding slides to this cell's note over xx ticks, without being
/// started again; <c>G00</c> takes one line.
/// </summary>
/// <remarks>
/// The acid bass line: one voice, its envelope running on, sliding between notes. The amount is
/// how long the slide takes rather than how fast it goes, so a fifth and a semitone arrive at the
/// same moment, which is what a hand on a keyboard expects. A slide longer than a line goes on
/// over the lines after it until it arrives or another cell is written in its column.
///
/// The cell's volume and pan still reach the voice. With nothing sounding there is nothing to
/// slide from, so the note simply starts, which is what every tracker does with a glide on the
/// first note of a phrase.
/// </remarks>
public sealed class NoteGlide : ITickCommand
{
    /// <inheritdoc/>
    public char Letter => TrackerCommand.Glide;

    /// <inheritdoc/>
    public void Spread(TrackerEvent cell, VoiceState voice, int ticks, ICollection<TickEvent> into)
    {
        if (cell.Kind != TrackerEventKind.Trigger || !voice.Sounds)
        {
            voice.Take(cell);
            into.Add(new TickEvent(0, cell));
            return;
        }

        into.Add(new TickEvent(0, cell with { Kind = TrackerEventKind.Adjust, Note = Note.Empty }));

        int length = cell.Effect.Parameter <= 0 ? ticks : cell.Effect.Parameter;
        var slide = new Slide(cell, voice.Rest, cell.Note.Semitone - voice.Base.Semitone, length);

        voice.Carry = slide.Go(voice, ticks, into) ? slide : null;
    }

    /// <summary>One slide on its way: where it started, where it is going, and how far it has got.</summary>
    /// <param name="cell">The cell that asked for it, for its track, column and instrument.</param>
    /// <param name="from">Semitones off the voice's own note it started at.</param>
    /// <param name="to">Semitones off the voice's own note it is going to.</param>
    /// <param name="length">How many ticks it takes.</param>
    private sealed class Slide(TrackerEvent cell, float from, float to, int length) : ICarry
    {
        /// <summary>How many of its ticks have gone by.</summary>
        private int _done;

        /// <inheritdoc/>
        public bool Go(VoiceState voice, int ticks, ICollection<TickEvent> into)
        {
            for (int tick = 0; tick < ticks && _done < length; tick++)
            {
                _done++;

                float shift = from + (to - from) * _done / length;

                into.Add(new TickEvent(tick, voice.ShiftTo(cell, shift)));

                voice.Rest = shift;
            }

            return _done < length;
        }
    }
}
