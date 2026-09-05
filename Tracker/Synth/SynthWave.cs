using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class Oscillator : IOscillator
{
    /// <inheritdoc/>
    public double Sample(SynthWave wave, double phase, double duty, double noise) => wave switch
    {
        SynthWave.Sine => Math.Sin(2 * Math.PI * phase),
        SynthWave.Square => phase < 0.5 ? 1.0 : -1.0,
        SynthWave.Saw => 2.0 * phase - 1.0,
        SynthWave.Triangle => 4.0 * Math.Abs(phase - 0.5) - 1.0,
        SynthWave.Pulse => phase < duty ? 1.0 : -1.0,
        SynthWave.Noise => noise,
        _ => 0.0
    };

    /// <inheritdoc/>
    public void Period(SynthWave wave, double duty, Span<double> into, Random? noise)
    {
        for (int at = 0; at < into.Length; at++)
        {
            double random = noise is null ? 0.0 : noise.NextDouble() * 2.0 - 1.0;

            into[at] = Sample(wave, (at + 0.5) / into.Length, duty, random);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A phase that is not a finite number starts again at nought. It cannot be brought back
    /// inside the cycle by arithmetic: every comparison against NaN is false, so it passed
    /// straight through, and infinity took the first branch and came out as infinity less
    /// infinity, which is NaN as well. Either way the phase is fed back into itself on the next
    /// sample, so a voice that reached one of those states was silent for the rest of its life.
    /// </remarks>
    public double Wrap(double phase)
    {
        if (!double.IsFinite(phase)) return 0.0;

        if (phase >= 1.0) phase -= Math.Floor(phase);
        else if (phase < 0.0) phase += Math.Ceiling(-phase);

        return phase;
    }
}
