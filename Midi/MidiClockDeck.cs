using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class MidiClockDeck : IMidiClockDeck
{
    /// <summary>A clock tick, which is the whole message and the one sent most.</summary>
    private static readonly byte[] Tick = { 0xF8 };

    /// <summary>From the beginning.</summary>
    private static readonly byte[] Start = { 0xFA };

    /// <summary>From wherever the pointer last said.</summary>
    private static readonly byte[] Continue = { 0xFB };

    /// <summary>And stop.</summary>
    private static readonly byte[] Halted = { 0xFC };

    /// <summary>The most ticks worth sending in one go.</summary>
    /// <remarks>
    /// A bound rather than a rule about music. Ticks are asked for as a count against a
    /// stopwatch, so a machine that slept through a debugger breakpoint or a suspend could be
    /// told a hundred thousand are due, and writing them would hold the clock thread for the
    /// length of it. Sixty four is more than two beats at any tempo: past that the other end has
    /// lost the plot anyway and the honest thing is to be a little behind rather than to stop
    /// the music catching up.
    /// </remarks>
    private const int MostAtOnce = 64;

    /// <summary>Where the bytes go out.</summary>
    private readonly IMidiService _midi;

    /// <summary>The tick and line arithmetic, for the pointer.</summary>
    private readonly IMidiClockGrid _grid;

    /// <summary>The ports being driven, already opened. Replaced whole rather than edited.</summary>
    /// <remarks>
    /// An array swapped as one so the clock thread never reads a list somebody is halfway through
    /// changing: the settings are edited on the thread things are drawn on and read on the thread
    /// that keeps time, which is the shape this codebase already keeps for the pattern's cells.
    /// </remarks>
    private volatile string[] _ports = Array.Empty<string>();

    /// <summary>Takes where to send and how to count. Both are stateless, so one of each is enough.</summary>
    /// <param name="midi">Where the bytes go out.</param>
    /// <param name="grid">The tick arithmetic, or the ordinary one.</param>
    public MidiClockDeck(IMidiService midi, IMidiClockGrid? grid = null)
    {
        _midi = midi;
        _grid = grid ?? new MidiClockGrid();
    }

    /// <inheritdoc/>
    public bool IsDriving => _ports.Length > 0;

    /// <inheritdoc/>
    public void Drive(IReadOnlyList<string>? outputs)
    {
        if (outputs is null || outputs.Count == 0)
        {
            if (_ports.Length > 0) Log.Write(LogArea.Midi, () => "clock: driving nothing");

            _ports = Array.Empty<string>();

            return;
        }

        var opened = new List<string>(outputs.Count);

        foreach (string one in outputs)
        {
            if (string.IsNullOrWhiteSpace(one)) continue;

            if (_midi.OpenFor(one)) opened.Add(one);
            else Log.Write(LogArea.Midi, () => "clock: '" + one + "' would not open, so it is not driven");
        }

        _ports = opened.ToArray();

        Log.Write(LogArea.Midi, () =>
            "clock: driving " + _ports.Length + " output" + (_ports.Length == 1 ? "" : "s")
            + (_ports.Length > 0 ? ", " + string.Join(", ", _ports) : ""));
    }

    /// <inheritdoc/>
    public void Play(int line, int linesPerBeat)
    {
        var ports = _ports;

        if (ports.Length == 0) return;

        if (line <= 0)
        {
            Put(ports, Start);

            return;
        }

        int pointer = _grid.PointerFor(line, linesPerBeat);

        Put(ports, new byte[] { 0xF2, (byte)(pointer & 0x7F), (byte)((pointer >> 7) & 0x7F) });
        Put(ports, Continue);
    }

    /// <inheritdoc/>
    public void Ticks(int howMany)
    {
        var ports = _ports;

        if (ports.Length == 0 || howMany <= 0) return;

        int sending = Math.Min(howMany, MostAtOnce);

        for (int at = 0; at < sending; at++) Put(ports, Tick);
    }

    /// <inheritdoc/>
    public void Halt()
    {
        var ports = _ports;

        if (ports.Length > 0) Put(ports, Halted);
    }

    /// <summary>
    /// One message to every port being driven.
    /// </summary>
    /// <remarks>
    /// A port that refuses is left in the list rather than dropped, deliberately, and the
    /// difference matters on the clock: <see cref="IMidiService.Send"/> already forgets a handle
    /// that failed and will open it again on the next write, so a cable knocked out and put back
    /// comes good on its own. Taking it out here would mean a device that stopped for a moment
    /// stays stopped until somebody visits the settings.
    ///
    /// Nothing is logged per message. This runs up to eighty times a second per port and a line
    /// apiece would be the log's whole content.
    /// </remarks>
    private void Put(string[] ports, byte[] message)
    {
        foreach (string one in ports) _midi.Send(one, message);
    }
}
