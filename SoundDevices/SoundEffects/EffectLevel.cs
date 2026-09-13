using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
public sealed class EffectLevel : IEffectLevel
{
    /// <summary>Where the knob stands, in decibels.</summary>
    private volatile float _db;

    /// <summary>The gain the last block ended at, or below nought before any block has run.</summary>
    private double _was = -1;

    /// <inheritdoc/>
    public double Db => _db;

    /// <inheritdoc/>
    public void Set(double db)
    {
        if (double.IsNaN(db) || double.IsInfinity(db)) return;

        _db = (float)Math.Clamp(db, IEffectLevel.LeastDb, IEffectLevel.MostDb);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Unity with nothing to ramp returns before the loop, which is what makes nought decibels
    /// exactly nothing rather than a multiplication by a number that rounds to one.
    /// </remarks>
    public void Apply(float[] buffer, int frames)
    {
        if (buffer is null) return;

        int block = Math.Min(frames, buffer.Length / 2);

        if (block <= 0) return;

        double want = _db == 0 ? 1 : Math.Pow(10, _db / 20.0);
        double from = _was < 0 ? want : _was;

        _was = want;

        if (from == 1 && want == 1) return;

        double step = (want - from) / block;

        for (int at = 0; at < block; at++)
        {
            double gain = from + (step * (at + 1));

            buffer[at * 2] = (float)(buffer[at * 2] * gain);
            buffer[(at * 2) + 1] = (float)(buffer[(at * 2) + 1] * gain);
        }
    }
}
