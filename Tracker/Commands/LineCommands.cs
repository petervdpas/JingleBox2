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
/// The commands are found by their letter, and the five this ships with are the ones handed in
/// when nothing is: delay, cut, retrigger, arpeggio and glide.
/// </remarks>
public sealed class LineCommands : ILineCommands
{
    /// <summary>Ticks a line, unless told otherwise.</summary>
    public const int DefaultTicks = 12;

    /// <summary>The commands, by letter.</summary>
    private readonly Dictionary<char, ITickCommand> _commands;

    /// <summary>What each note column is sounding, by track and column.</summary>
    private readonly Dictionary<(int Track, int Column), VoiceState> _voices = new();

    /// <summary>The columns the last line left somewhere other than where their voice rests.</summary>
    private readonly HashSet<(int Track, int Column)> _off = new();

    /// <summary>A module answering to the commands given, or to the five it ships with.</summary>
    /// <param name="commands">The commands, or nothing for delay, cut, retrigger, arpeggio and glide.</param>
    /// <param name="ticks">How many ticks a line has.</param>
    public LineCommands(IEnumerable<ITickCommand>? commands = null, int ticks = DefaultTicks)
    {
        TicksPerLine = Math.Max(1, ticks);

        _commands = (commands ?? new ITickCommand[]
        {
            new NoteDelay(), new NoteCut(), new NoteRetrigger(), new NoteArpeggio(), new NoteGlide()
        }).ToDictionary(command => char.ToUpperInvariant(command.Letter));
    }

    /// <inheritdoc/>
    public int TicksPerLine { get; }

    /// <inheritdoc/>
    public void Reset()
    {
        _voices.Clear();
        _off.Clear();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Three things in order. Whatever an earlier line left going goes on, unless this line writes
    /// into that column, in which case it stops where it had got to. A column an arpeggio left off
    /// its resting pitch is put back, unless this line starts a new note there, which begins at its
    /// own pitch anyway. Then the line's own cells.
    ///
    /// Sorted by tick and nothing else, so events on one tick stay in the order they were written,
    /// which is what lets a cut at tick nought come after the note it cuts.
    /// </remarks>
    public IReadOnlyList<TickEvent> Ticks(IReadOnlyList<TrackerEvent> line)
    {
        var spread = new List<TickEvent>(line.Count);
        var written = new HashSet<(int, int)>(line.Select(e => (e.Track, e.Column)));
        var struck = new HashSet<(int, int)>(line.Where(e => e.Kind == TrackerEventKind.Trigger).Select(e => (e.Track, e.Column)));
        var carried = new HashSet<(int, int)>();

        foreach (var (at, voice) in _voices)
        {
            if (voice.Carry is not { } carry) continue;

            if (written.Contains(at))
            {
                voice.Carry = null;
                continue;
            }

            if (!carry.Go(voice, TicksPerLine, spread)) voice.Carry = null;

            carried.Add(at);
        }

        foreach (var at in _off)
        {
            if (struck.Contains(at) || carried.Contains(at) || !_voices.TryGetValue(at, out var voice) || !voice.Sounds)
                continue;

            spread.Add(new TickEvent(0, new TrackerEvent(at.Track, at.Column, TrackerEventKind.Shift,
                voice.Base, TrackerCell.NoInstrument, null, TrackerCommand.None, voice.Rest)));
        }

        _off.Clear();

        foreach (var e in line)
        {
            var voice = Voice((e.Track, e.Column));

            if (!e.Effect.IsNone && _commands.TryGetValue(e.Effect.Command, out var command))
            {
                command.Spread(e, voice, TicksPerLine, spread);
            }
            else
            {
                voice.Take(e);
                spread.Add(new TickEvent(0, e));
            }
        }

        var ordered = spread.OrderBy(one => one.Tick).ToArray();
        var last = new Dictionary<(int, int), float>();

        foreach (var one in ordered)
        {
            if (one.Event.Kind == TrackerEventKind.Shift) last[(one.Event.Track, one.Event.Column)] = one.Event.Shift;
        }

        foreach (var (at, shift) in last)
        {
            if (_voices.TryGetValue(at, out var voice) && voice.Sounds && shift != voice.Rest) _off.Add(at);
        }

        return ordered;
    }

    /// <summary>The memory of one note column, made the first time it is asked for.</summary>
    private VoiceState Voice((int Track, int Column) at)
    {
        if (!_voices.TryGetValue(at, out var voice)) _voices[at] = voice = new VoiceState();

        return voice;
    }
}
