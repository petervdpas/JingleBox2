using System;

namespace JingleBox2.Tracker.Synth;

public enum SynthWave
{
    Sine = 0,
    Square = 1,
    Saw = 2,
    Triangle = 3,
    Pulse = 4,
    Noise = 5
}

/// <summary>
/// The waveform shapes, as one function of phase. Naive shapes, not band limited: a tracker
/// running square and saw waves is meant to sound like this, and the aliasing is part of it.
/// </summary>
public static class Oscillator
{
    /// <summary>All waves are read at a phase in 0..1 and return -1..1.</summary>
    public static double Sample(SynthWave wave, double phase, double duty, double noise) => wave switch
    {
        SynthWave.Sine => Math.Sin(2 * Math.PI * phase),
        SynthWave.Square => phase < 0.5 ? 1.0 : -1.0,
        SynthWave.Saw => 2.0 * phase - 1.0,
        SynthWave.Triangle => 4.0 * Math.Abs(phase - 0.5) - 1.0,
        SynthWave.Pulse => phase < duty ? 1.0 : -1.0,
        SynthWave.Noise => noise,
        _ => 0.0
    };

    /// <summary>Keeps a running phase inside 0..1 without a modulo on every sample.</summary>
    public static double Wrap(double phase)
    {
        if (phase >= 1.0) phase -= Math.Floor(phase);
        else if (phase < 0.0) phase += Math.Ceiling(-phase);

        return phase;
    }
}
