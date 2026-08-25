using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// An ADSR as a curve over time, for drawing. The same segments the voice's envelope walks,
/// with the note held for a given time before it is let go.
/// </summary>
/// <remarks>
/// A patch with no sustain ends on its decay, so its curve ends there too rather than trailing
/// a flat line nobody hears. That way the picture is as long as the sound.
/// </remarks>
public readonly record struct EnvelopeShape(
    double AttackSeconds,
    double DecaySeconds,
    double Sustain,
    double ReleaseSeconds,
    double HoldSeconds)
{
    /// <summary>Short enough to be a moment, long enough that a spike is still visible.</summary>
    public const double MinimumLength = 0.05;

    public static EnvelopeShape FromMilliseconds(
        double attackMs, double decayMs, double sustain, double releaseMs, double holdSeconds) => new(
        Math.Max(0, attackMs) / 1000.0,
        Math.Max(0, decayMs) / 1000.0,
        Math.Clamp(double.IsNaN(sustain) ? 0 : sustain, 0, 1),
        Math.Max(0, releaseMs) / 1000.0,
        Math.Max(0, holdSeconds));

    /// <summary>Where the note is let go of, which is also where the release starts.</summary>
    public double ReleaseStarts => AttackSeconds + DecaySeconds + (Sustain > 0 ? HoldSeconds : 0);

    /// <summary>How long the whole thing lasts, from the key going down to silence.</summary>
    public double Length =>
        Math.Max(MinimumLength, ReleaseStarts + (Sustain > 0 ? ReleaseSeconds : 0));

    /// <summary>The level at a point in the note, 0 to 1.</summary>
    public double LevelAt(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0) return 0;

        if (seconds < AttackSeconds)
            return seconds / AttackSeconds;

        double afterAttack = seconds - AttackSeconds;
        if (afterAttack < DecaySeconds)
            return 1 - (1 - Sustain) * (afterAttack / DecaySeconds);

        // Past the decay with nothing to sustain, the note is already over.
        if (Sustain <= 0) return 0;

        double afterDecay = afterAttack - DecaySeconds;
        if (afterDecay < HoldSeconds) return Sustain;

        double afterRelease = afterDecay - HoldSeconds;
        if (ReleaseSeconds <= 0 || afterRelease >= ReleaseSeconds) return 0;

        return Sustain * (1 - afterRelease / ReleaseSeconds);
    }
}
