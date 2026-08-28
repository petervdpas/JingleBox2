using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// The drive curve, shared by the voice that plays a patch and the scope that draws it, so the
/// picture is the same shape as the sound.
/// </summary>
/// <remarks>
/// Applied per sample per voice on the audio thread. Nothing is kept between calls and nothing
/// is allocated: the makeup a voice needs is worked out once when the note starts and handed
/// back in.
/// </remarks>
public static class Saturation
{
    /// <summary>
    /// What to multiply a driven sample by to bring it back to the level it came in at. Worked
    /// out once per voice, since it only depends on the drive amount.
    /// </summary>
    public static double Makeup(double drive) => drive > 1 ? 1.0 / Math.Tanh(drive) : 1.0;

    /// <summary>Rounds the wave off into itself. A drive of one or less leaves it untouched.</summary>
    public static double Apply(double sample, double drive, double makeup) =>
        drive > 1 ? Math.Tanh(sample * drive) * makeup : sample;

    /// <summary>
    /// The same for a caller with no makeup to hand, which works it out on the spot.
    /// </summary>
    /// <remarks>
    /// For a scope drawing one picture, and not for a voice: a hyperbolic tangent per sample
    /// per voice on top of the one the curve already costs is real money on the audio thread,
    /// which is why a voice keeps its makeup and passes it in.
    /// </remarks>
    public static double Apply(double sample, double drive) => Apply(sample, drive, Makeup(drive));
}
