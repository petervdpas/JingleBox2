using System;

namespace JingleBox2.Rack.Ui.Records;

/// <summary>
/// An ADSR as a curve over time, for drawing. The same segments the voice's envelope walks,
/// with the note held for a given time before it is let go.
/// </summary>
/// <remarks>
/// A patch with no sustain ends on its decay, so its curve ends there too rather than trailing
/// a flat line nobody hears. That way the picture is as long as the sound.
/// </remarks>
/// <param name="AttackSeconds">How long it takes to reach the top from the key going down.</param>
/// <param name="DecaySeconds">And how long it takes to fall from there to the sustain.</param>
/// <param name="Sustain">Where it rests while the key is held, nought to one.</param>
/// <param name="ReleaseSeconds">How long it takes to reach silence once the key comes up.</param>
/// <param name="HoldSeconds">
/// How long the key is held down for, which is the drawing's own and not the patch's: a note
/// played by hand holds for a fixed moment and a note in a pattern holds for its own length.
/// </param>
public readonly record struct EnvelopeShape(
    double AttackSeconds,
    double DecaySeconds,
    double Sustain,
    double ReleaseSeconds,
    double HoldSeconds)
{
    /// <summary>Short enough to be a moment, long enough that a spike is still visible.</summary>
    public const double MinimumLength = 0.05;

    /// <summary>
    /// Builds one from the numbers a patch holds, which are milliseconds where this works in
    /// seconds.
    /// </summary>
    /// <remarks>
    /// Everything is brought into range on the way in. A patch can arrive out of a file with a
    /// negative time or a NaN sustain, and a curve is drawn on the drawing thread where neither
    /// has anywhere useful to go.
    /// </remarks>
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
    /// <remarks>
    /// Past the decay with nothing to sustain the note is already over, so this reads nought
    /// there rather than walking a hold and a release that no sound is left in.
    /// </remarks>
    public double LevelAt(double seconds)
    {
        if (double.IsNaN(seconds) || seconds <= 0) return 0;

        if (seconds < AttackSeconds)
            return seconds / AttackSeconds;

        double afterAttack = seconds - AttackSeconds;
        if (afterAttack < DecaySeconds)
            return 1 - (1 - Sustain) * (afterAttack / DecaySeconds);

        if (Sustain <= 0) return 0;

        double afterDecay = afterAttack - DecaySeconds;
        if (afterDecay < HoldSeconds) return Sustain;

        double afterRelease = afterDecay - HoldSeconds;
        if (ReleaseSeconds <= 0 || afterRelease >= ReleaseSeconds) return 0;

        return Sustain * (1 - afterRelease / ReleaseSeconds);
    }
}
