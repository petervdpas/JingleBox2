using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
/// <remarks>
/// Every tangent in here goes through <see cref="ITangent"/> rather than through the system's own
/// call, so which curve is being used is one decision taken in one place for the whole
/// application. Handed one, this uses it and nothing else, which is what lets both answers be put
/// a question to side by side; handed nothing, it asks <see cref="TangentSwitch"/> each time.
///
/// **Asked each time rather than taken once**, deliberately, because the voices share one of
/// these in a static field that is built before a settings file has been read: one taken at
/// construction would be the curve this application shipped with for the rest of the session,
/// whatever anybody ticked.
/// </remarks>
/// <param name="tangent">The curve to bend with, or nothing to follow the application's switch.</param>
public sealed class Saturation(ITangent? tangent = null) : ISaturation
{
    /// <summary>The curve this one bends with, which is the switch's unless it was handed one.</summary>
    private ITangent Curve => tangent ?? TangentSwitch.Now;

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
    public double Makeup(double drive) => Driven(drive) ? 1.0 / Curve.Of(drive) : 1.0;

    /// <inheritdoc/>
    /// <remarks>
    /// Measured against the curve at full strength rather than against the faded blend, which is
    /// what <see cref="Makeup"/> does and for the same reason: the fade is there to stop the knob
    /// stepping as it leaves its stop, and a makeup that moved with it would be correcting the
    /// correction.
    ///
    /// Root mean square rather than anything weighted. What is being held is the level the knob
    /// used to add, and the ear reads that as loudness over the handful of harmonics a drive
    /// moves; a loudness measure with a curve in it would be a second opinion nobody asked for
    /// inside a control that has to be predictable.
    /// </remarks>
    public double Evenly(double drive, ReadOnlySpan<double> shape)
    {
        if (!Driven(drive) || shape.Length == 0) return 1.0;

        double dry = 0;
        double wet = 0;

        foreach (double sample in shape)
        {
            if (!double.IsFinite(sample)) continue;

            double driven = Curve.Of(sample * drive);

            dry += sample * sample;
            wet += driven * driven;
        }

        if (dry <= 0 || wet <= 0) return 1.0;

        double makeup = Math.Sqrt(dry / wet);

        return double.IsFinite(makeup) ? makeup : 1.0;
    }

    /// <inheritdoc/>
    public double Fade(double drive) =>
        Driven(drive) ? Math.Clamp((drive - 1.0) / FadeIn, 0, 1) : 0;

    /// <inheritdoc/>
    public double Apply(double sample, double drive, double makeup) =>
        Apply(sample, drive, makeup, Fade(drive));

    /// <inheritdoc/>
    public double Apply(double sample, double drive, double makeup, double fade)
    {
        if (sample == 0 || !Driven(drive)) return sample;

        double wet = Curve.Of(sample * drive) * makeup;

        return sample + (wet - sample) * fade;
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
