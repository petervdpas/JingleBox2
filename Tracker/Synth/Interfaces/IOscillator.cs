using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// The waveform shapes, as one function of phase. Naive shapes, not band limited: a tracker
/// running square and saw waves is meant to sound like this, and the aliasing is part of it.
/// </summary>
/// <remarks>
/// Called once per sample per voice on the audio thread, so it allocates nothing, takes no
/// lock and holds no state of its own: the phase belongs to the voice that is reading it.
/// </remarks>
public interface IOscillator
{
    /// <summary>All waves are read at a phase in 0..1 and return -1..1.</summary>
    /// <param name="wave">Which shape to read.</param>
    /// <param name="phase">Where in the cycle, 0 to 1.</param>
    /// <param name="duty">How much of a pulse's cycle is high. Ignored by every other shape.</param>
    /// <param name="noise">
    /// The random value to hand back for <see cref="SynthWave.Noise"/>. Passed in rather than
    /// generated here so each voice keeps its own generator and two noise hits started at the
    /// same instant are not the same noise.
    /// </param>
    double Sample(SynthWave wave, double phase, double duty, double noise);

    /// <summary>Keeps a running phase inside 0..1 without a modulo on every sample.</summary>
    /// <param name="phase">Where the voice's phase has got to, which may have run off either end.</param>
    double Wrap(double phase);

    /// <summary>
    /// One period of a wave, sampled evenly, for anything that wants the shape rather than the
    /// samples somebody is hearing.
    /// </summary>
    /// <remarks>
    /// Two callers want exactly this and would otherwise each write out the loop: the voice
    /// working out what a drive costs the loudness of the wave it is about to play, and the scope
    /// working out the same thing so the picture is the shape of the sound. Written twice they
    /// would eventually disagree, and the way that fails is a drawn wave that is not the one you
    /// can hear.
    ///
    /// Not on the audio thread. A period is walked when a note starts and when a picture is
    /// repainted, never per sample.
    ///
    /// Phase is taken at the middle of each step rather than at its edge, so a square is described
    /// by its two levels rather than by landing exactly on the instant it changes.
    /// </remarks>
    /// <param name="wave">Which shape to read.</param>
    /// <param name="duty">How much of a pulse's cycle is high. Ignored by every other shape.</param>
    /// <param name="into">Filled with one period. Its length is how finely the period is described.</param>
    /// <param name="noise">
    /// Where <see cref="SynthWave.Noise"/> gets its values, or nothing for silence there. A
    /// generator of its own rather than the voice's, since taking values out of that one would
    /// take them out of the noise somebody is about to hear.
    /// </param>
    void Period(SynthWave wave, double duty, System.Span<double> into, System.Random? noise);
}
