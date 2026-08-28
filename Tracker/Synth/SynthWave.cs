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
    public double Wrap(double phase)
    {
        if (phase >= 1.0) phase -= Math.Floor(phase);
        else if (phase < 0.0) phase += Math.Ceiling(-phase);

        return phase;
    }
}
