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
    /// <remarks>
    /// A moment that is not a finite number, or is before the note began, reads as the start of
    /// the note. The envelope's only bound was the far end of it, and a note is never asked for
    /// before it starts, so a negative moment ran the ramp backwards past its own full depth and
    /// away without limit: at minus a second on a hundred millisecond envelope the bend is eleven
    /// times what the patch asked for. Nothing produces one today, and the arithmetic should not
    /// be waiting for something that does.
    /// </remarks>
    public double MotionAt(SynthPatch patch, double seconds)
    {
        if (patch is null) return 0;

        double at = double.IsFinite(seconds) ? Math.Max(0, seconds) : 0;
        double offset = 0;

        if (patch.VibratoDepthCents > 0 && patch.VibratoRateHz > 0)
            offset += patch.VibratoDepthCents / 100.0 * Math.Sin(2 * Math.PI * patch.VibratoRateHz * at);

        double envelopeSeconds = patch.PitchEnvMs / 1000.0;
        if (patch.PitchEnvSemitones != 0 && envelopeSeconds > 0 && at < envelopeSeconds)
            offset += patch.PitchEnvSemitones * (1.0 - at / envelopeSeconds);

        return offset;
    }

    /// <inheritdoc/>
    public double Ratio(double semitones) => Math.Pow(2.0, semitones / 12.0);
}
