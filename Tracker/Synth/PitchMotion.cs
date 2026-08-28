using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class PitchMotion : IPitchMotion
{
    /// <inheritdoc/>
    public double Tuning(SynthPatch patch) =>
        patch is null ? 0 : patch.TuneSemitones + patch.FineCents / 100.0;

    /// <inheritdoc/>
    public double MotionAt(SynthPatch patch, double seconds)
    {
        if (patch is null) return 0;

        double offset = 0;

        if (patch.VibratoDepthCents > 0 && patch.VibratoRateHz > 0)
            offset += patch.VibratoDepthCents / 100.0 * Math.Sin(2 * Math.PI * patch.VibratoRateHz * seconds);

        double envelopeSeconds = patch.PitchEnvMs / 1000.0;
        if (patch.PitchEnvSemitones != 0 && envelopeSeconds > 0 && seconds < envelopeSeconds)
            offset += patch.PitchEnvSemitones * (1.0 - seconds / envelopeSeconds);

        return offset;
    }

    /// <inheritdoc/>
    public double Ratio(double semitones) => Math.Pow(2.0, semitones / 12.0);
}
