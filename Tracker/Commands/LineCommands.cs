using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands;

/// <inheritdoc/>
/// <remarks>
/// Twelve ticks a line, which is Renoise's own default. Twelve divides by two, three and four,
/// so a delay can land on a half, a third or a quarter of a line and an arpeggio's three steps
/// come round evenly.
///
/// The commands are found by their letter, and the four this ships with are the ones handed in
/// when nothing is: delay, cut, retrigger and arpeggio.
/// </remarks>
public sealed class LineCommands : ILineCommands
{
    /// <summary>Ticks a line, unless told otherwise.</summary>
    public const int DefaultTicks = 12;

    /// <summary>The commands, by letter.</summary>
    private readonly Dictionary<char, ITickCommand> _commands;

    /// <summary>The note each column is sounding, by track and column.</summary>
    private readonly Dictionary<(int Track, int Column), Note> _sounding = new();

    /// <summary>The columns an arpeggio left off their own pitch at the end of the last line.</summary>
    private readonly HashSet<(int Track, int Column)> _shifted = new();

    /// <summary>A module answering to the commands given, or to the four it ships with.</summary>
    /// <param name="commands">The commands, or nothing for delay, cut, retrigger and arpeggio.</param>
    /// <param name="ticks">How many ticks a line has.</param>
    public LineCommands(IEnumerable<ITickCommand>? commands = null, int ticks = DefaultTicks)
    {
        TicksPerLine = Math.Max(1, ticks);

        _commands = (commands ?? new ITickCommand[]
        {
            new NoteDelay(), new NoteCut(), new NoteRetrigger(), new NoteArpeggio()
        }).ToDictionary(command => char.ToUpperInvariant(command.Letter));
    }

    /// <inheritdoc/>
    public int TicksPerLine { get; }

    /// <inheritdoc/>
    public void Reset()
    {
        _sounding.Clear();
        _shifted.Clear();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A column an arpeggio left off its pitch is put back at the start of the next line, before
    /// anything else on it, unless that line starts a new note there: a new note starts at its own
    /// pitch anyway, and putting the old one back first would sound it again on a plugin.
    ///
    /// Sorted by tick and nothing else, so events on one tick stay in the order they were written,
    /// which is what lets a cut at tick nought come after the note it cuts.
    /// </remarks>
    public IReadOnlyList<TickEvent> Ticks(IReadOnlyList<TrackerEvent> line)
    {
        var spread = new List<TickEvent>(line.Count);

        foreach (var column in _shifted)
        {
            if (line.Any(e => e.Track == column.Track && e.Column == column.Column && e.Kind == TrackerEventKind.Trigger))
                continue;

            var note = _sounding.TryGetValue(column, out var was) ? was : Note.Empty;

            spread.Add(new TickEvent(0, new TrackerEvent(column.Track, column.Column, TrackerEventKind.Shift,
                note, TrackerCell.NoInstrument, null, TrackerCommand.None)));
        }

        _shifted.Clear();

        foreach (var e in line)
        {
            var at = (e.Track, e.Column);

            if (e.Kind == TrackerEventKind.Trigger) _sounding[at] = e.Note;
            else if (e.Kind is TrackerEventKind.Stop or TrackerEventKind.Cut) _sounding.Remove(at);

            if (!e.Effect.IsNone && _commands.TryGetValue(e.Effect.Command, out var command))
            {
                var sounding = _sounding.TryGetValue(at, out var note) ? note : Note.Empty;

                command.Spread(e, sounding, TicksPerLine, spread);
            }
            else
            {
                spread.Add(new TickEvent(0, e));
            }
        }

        var ordered = spread.OrderBy(one => one.Tick).ToArray();

        foreach (var one in ordered)
        {
            if (one.Event.Kind != TrackerEventKind.Shift) continue;

            if (one.Event.Shift != 0) _shifted.Add((one.Event.Track, one.Event.Column));
            else _shifted.Remove((one.Event.Track, one.Event.Column));
        }

        return ordered;
    }
}
