using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class Saturation : ISaturation
{
    /// <inheritdoc/>
    public double Makeup(double drive) => drive > 1 ? 1.0 / Math.Tanh(drive) : 1.0;

    /// <inheritdoc/>
    public double Apply(double sample, double drive, double makeup) =>
        drive > 1 ? Math.Tanh(sample * drive) * makeup : sample;

    /// <inheritdoc/>
    public double Apply(double sample, double drive) => Apply(sample, drive, Makeup(drive));
}
