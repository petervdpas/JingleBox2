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

    /// <summary>
    /// What to multiply a driven sample by to leave the given shape as loud as it arrived, rather
    /// than as tall as it arrived.
    /// </summary>
    /// <remarks>
    /// <see cref="Makeup"/> maps full scale to full scale, which holds the peak and says nothing
    /// about the loudness. That is the right answer for a curve and the wrong one for a knob: a
    /// saw driven hard is nearly a square, the square has the same peak and far more area, and the
    /// measured cost is about five and a half decibels of loudness from a control whose whole
    /// point is that it changes the tone rather than the level.
    ///
    /// So the shape has to be handed in. There is no closed form that covers a sine, a saw, a
    /// pulse at any width and noise, and there is no need for one: the caller knows what it is
    /// about to play, this is worked out once when a note starts, and a few hundred points of a
    /// wave is a truer answer than any formula would be.
    ///
    /// One where there is no drive, where the shape is silent, or where the curve somehow flattens
    /// it to nothing, since every one of those is a makeup that would either do nothing or divide
    /// by nought.
    /// </remarks>
    /// <param name="drive">How hard the wave is pushed into the curve. One or less is no drive.</param>
    /// <param name="shape">
    /// One period of what is about to be played, or a fair sample of it, in -1..1.
    /// </param>
    double Evenly(double drive, System.ReadOnlySpan<double> shape);

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
