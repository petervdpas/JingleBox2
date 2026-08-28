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
}
