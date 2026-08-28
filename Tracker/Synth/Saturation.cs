using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class Saturation : ISaturation
{
    /// <summary>
    /// Over how much of the knob the curve is faded in, above the drive at which there is none.
    /// </summary>
    /// <remarks>
    /// The curve does not meet the dry signal where it starts. A tangent normalised by its own
    /// value at full scale maps the two ends to themselves and lifts everything between them,
    /// so at a drive a hair above one, half scale came out at 0.6068 rather than 0.5: the knob
    /// stepped 1.6 dB as it left its own minimum and was smooth from there on. Fading the curve
    /// in over the first unit of the range costs one multiply and is inaudible against what the
    /// drive is doing anyway. A drive of two or more is exactly what it always was, and one is
    /// still nothing at all, so only the narrow band between them moves.
    /// </remarks>
    private const double FadeIn = 1.0;

    /// <inheritdoc/>
    public double Makeup(double drive) => Driven(drive) ? 1.0 / Math.Tanh(drive) : 1.0;

    /// <inheritdoc/>
    public double Apply(double sample, double drive, double makeup)
    {
        if (sample == 0 || !Driven(drive)) return sample;

        double wet = Math.Tanh(sample * drive) * makeup;
        double amount = Math.Clamp((drive - 1.0) / FadeIn, 0, 1);

        return sample + (wet - sample) * amount;
    }

    /// <inheritdoc/>
    public double Apply(double sample, double drive) => Apply(sample, drive, Makeup(drive));

    /// <summary>
    /// Whether there is a curve to apply at all. A drive that is not a number reads as none.
    /// </summary>
    /// <remarks>
    /// An infinite drive is allowed through and is a hard clip, which is where the curve was
    /// always heading. Silence is answered before the curve is reached rather than by it: every
    /// curve here maps nought to nought, and the tangent does not, because nought times infinity
    /// is not a number, so the one sample no drive can touch was the one that came back poisoned.
    /// </remarks>
    private static bool Driven(double drive) => drive > 1;
}
