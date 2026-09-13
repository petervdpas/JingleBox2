using System;
using System.Diagnostics;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <param name="frequency">How many stopwatch ticks make a second; the real stopwatch's where left out.</param>
public sealed class ClockTempo(long frequency = 0) : IClockTempo
{
    /// <summary>How many tick gaps a tempo is worked out over: two beats, which answers a tempo knob within a couple of seconds.</summary>
    public const int Window = 48;

    /// <summary>Clock ticks to the quarter note, which is what the standard says and every sequencer assumes.</summary>
    private const int PerBeat = 24;

    /// <summary>How far a tempo has to move before it is said again: past what jitter fitted away can leave.</summary>
    private const double Moved = 0.15;

    private readonly long _frequency = frequency > 0 ? frequency : Stopwatch.Frequency;

    /// <summary>The last <see cref="Window"/> plus one moments, oldest first once full.</summary>
    private readonly long[] _moments = new long[Window + 1];

    private int _count;

    private int _next;

    private double? _said;

    /// <inheritdoc/>
    public double? Heard(long timestamp)
    {
        if (_count > 0)
        {
            long last = _moments[(_next + _moments.Length - 1) % _moments.Length];

            if (timestamp <= last || timestamp - last > _frequency) _count = 0;
        }

        _moments[_next] = timestamp;
        _next = (_next + 1) % _moments.Length;
        if (_count < _moments.Length) _count++;

        if (_count < _moments.Length) return null;

        double perTick = Slope() / _frequency;
        if (perTick <= 0) return null;

        double bpm = Math.Round(60.0 / (perTick * PerBeat), 1);

        if (bpm < TrackerTiming.MinBpm || bpm > TrackerTiming.MaxBpm) return null;
        if (_said is { } was && Math.Abs(bpm - was) < Moved) return null;

        _said = bpm;
        return bpm;
    }

    /// <inheritdoc/>
    public void Forget()
    {
        _count = 0;
        _said = null;
    }

    /// <summary>The stopwatch ticks between two clock ticks, as the least squares line through the window has it.</summary>
    private double Slope()
    {
        int n = _moments.Length;
        long origin = _moments[_next];
        double meanX = (n - 1) / 2.0;
        double meanY = 0;

        for (int i = 0; i < n; i++) meanY += _moments[(_next + i) % n] - origin;
        meanY /= n;

        double above = 0, below = 0;

        for (int i = 0; i < n; i++)
        {
            double x = i - meanX;
            above += x * (_moments[(_next + i) % n] - origin - meanY);
            below += x * x;
        }

        return above / below;
    }
}
