using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// The drive curve, shared by the voice that plays a patch and the scope that draws it, so the
/// picture is the same shape as the sound.
/// </summary>
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

    public static double Apply(double sample, double drive) => Apply(sample, drive, Makeup(drive));
}
