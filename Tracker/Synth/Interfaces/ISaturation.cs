namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// The drive curve, shared by the voice that plays a patch and the scope that draws it, so the
/// picture is the same shape as the sound.
/// </summary>
/// <remarks>
/// Applied per sample per voice on the audio thread. Nothing is kept between calls and nothing
/// is allocated: the makeup a voice needs is worked out once when the note starts and handed
/// back in.
/// </remarks>
public interface ISaturation
{
    /// <summary>
    /// What to multiply a driven sample by to bring it back to the level it came in at. Worked
    /// out once per voice, since it only depends on the drive amount.
    /// </summary>
    /// <param name="drive">How hard the wave is pushed into the curve. One or less is no drive.</param>
    double Makeup(double drive);

    /// <summary>Rounds the wave off into itself. A drive of one or less leaves it untouched.</summary>
    /// <param name="sample">The value going in, which is expected to be in -1..1.</param>
    /// <param name="drive">How hard the wave is pushed into the curve. One or less is no drive.</param>
    /// <param name="makeup">What the drive costs in level, from <see cref="Makeup"/>, worked out once per voice.</param>
    double Apply(double sample, double drive, double makeup);

    /// <summary>
    /// The same for a caller with no makeup to hand, which works it out on the spot.
    /// </summary>
    /// <remarks>
    /// For a scope drawing one picture, and not for a voice: a hyperbolic tangent per sample
    /// per voice on top of the one the curve already costs is real money on the audio thread,
    /// which is why a voice keeps its makeup and passes it in.
    /// </remarks>
    /// <param name="sample">The value going in, which is expected to be in -1..1.</param>
    /// <param name="drive">How hard the wave is pushed into the curve. One or less is no drive.</param>
    double Apply(double sample, double drive);
}
