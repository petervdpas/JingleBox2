using System;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class MidiClockGrid : IMidiClockGrid
{
    /// <inheritdoc/>
    /// <remarks>
    /// Twenty four, and it is not a choice: it is what the specification says and what every
    /// sequencer that has ever taken clock assumes. Named rather than left as a literal in the
    /// four places below that divide by it.
    /// </remarks>
    public int PerBeat => 24;

    /// <summary>How many ticks make a sixteenth note, which is what a position pointer counts.</summary>
    private const int PerSixteenth = 6;

    /// <summary>
    /// The most a position pointer can carry, since the message holds it as two seven-bit halves.
    /// </summary>
    private const int MostPointer = (1 << 14) - 1;

    /// <summary>
    /// The slowest tempo and the fewest lines a beat worth working anything out for.
    /// </summary>
    /// <remarks>
    /// Floors rather than refusals, so nothing here divides by nought however it is called. A
    /// tempo of nought is not a slow song, it is a caller that has not read one yet, and this is
    /// read on the thread that keeps time where a throw is the music stopping.
    /// </remarks>
    private const double LeastBpm = 1;

    /// <inheritdoc cref="LeastBpm"/>
    private const int LeastLines = 1;

    /// <summary>That tempo, made safe to divide by.</summary>
    private static double Beats(double bpm) =>
        Math.Max(LeastBpm, double.IsFinite(bpm) ? bpm : LeastBpm);

    /// <summary>And that line count.</summary>
    private static int Lines(int linesPerBeat) => Math.Max(LeastLines, linesPerBeat);

    /// <inheritdoc/>
    public double TickSeconds(double bpm) => 60.0 / (Beats(bpm) * PerBeat);

    /// <inheritdoc/>
    /// <remarks>
    /// A floor rather than a rounding, so a tick is due once its moment has passed and never a
    /// hair before it. Anything that is not a number, and any stretch before the pass began,
    /// answers nought rather than throwing.
    /// </remarks>
    public long DueBy(double elapsedSeconds, double tickSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || !double.IsFinite(tickSeconds)) return 0;
        if (elapsedSeconds <= 0 || tickSeconds <= 0) return 0;

        double ticks = elapsedSeconds / tickSeconds;

        return ticks >= long.MaxValue ? long.MaxValue : (long)Math.Floor(ticks);
    }

    /// <inheritdoc/>
    public double TickOfLine(int line, int linesPerBeat) =>
        line <= 0 ? 0 : (double)line * PerBeat / Lines(linesPerBeat);

    /// <inheritdoc/>
    /// <remarks>
    /// The multiplication before the division, so a tick count that is not a whole number of
    /// lines keeps its remainder instead of losing it to integer arithmetic on the way in.
    /// </remarks>
    public int LineAtTick(long ticks, int linesPerBeat)
    {
        if (ticks <= 0) return 0;

        long line = ticks * Lines(linesPerBeat) / PerBeat;

        return (int)Math.Clamp(line, 0, int.MaxValue);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A line is <c>PerBeat / linesPerBeat</c> ticks and a sixteenth is six of them, so a line is
    /// <c>line * 24 / linesPerBeat / 6</c> sixteenths. Written in that order rather than
    /// simplified to <c>line * 4 / linesPerBeat</c>, because the two are the same number only
    /// when the divisions come out whole and the long form is the one that rounds the way the
    /// tick grid does.
    /// </remarks>
    public int PointerFor(int line, int linesPerBeat)
    {
        if (line <= 0) return 0;

        long sixteenths = (long)line * PerBeat / Lines(linesPerBeat) / PerSixteenth;

        return (int)Math.Clamp(sixteenths, 0, MostPointer);
    }

    /// <inheritdoc/>
    public int LineAtPointer(int pointer, int linesPerBeat)
    {
        if (pointer <= 0) return 0;

        long ticks = (long)Math.Clamp(pointer, 0, MostPointer) * PerSixteenth;

        return LineAtTick(ticks, linesPerBeat);
    }
}
