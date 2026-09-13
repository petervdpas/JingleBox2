using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
public sealed class SlowOscillator : ISlowOscillator
{
    /// <summary>Frames a second, which is what a rate in hertz is turned into.</summary>
    private readonly double _rate;

    /// <summary>Builds one at the rate it will be stepped at.</summary>
    /// <param name="sampleRate">What the mix runs at.</param>
    public SlowOscillator(int sampleRate) => _rate = sampleRate > 0 ? sampleRate : 48000;

    /// <inheritdoc/>
    public double Phase { get; private set; }

    /// <inheritdoc/>
    public double Step(double hertz)
    {
        if (double.IsFinite(hertz) && hertz > 0)
        {
            double at = Phase + (hertz / _rate);

            Phase = at - Math.Floor(at);
        }

        return Math.Sin(2 * Math.PI * Phase);
    }

    /// <inheritdoc/>
    public double Beside(double turn) =>
        Math.Sin(2 * Math.PI * (Phase + (double.IsFinite(turn) ? turn : 0)));
}
