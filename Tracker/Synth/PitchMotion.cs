using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Everything that moves a voice away from the note it was given: the instrument's own tuning,
/// which holds still, and the vibrato and pitch envelope, which do not.
/// </summary>
/// <remarks>
/// Shared by the voice that plays a patch and the scope that draws it, so a pitch envelope
/// bends the picture exactly as far as it bends the sound.
/// </remarks>
public static class PitchMotion
{
    /// <summary>The instrument's fixed offset, in semitones. The same for every note and moment.</summary>
    public static double Tuning(SynthPatch patch) =>
        patch is null ? 0 : patch.TuneSemitones + patch.FineCents / 100.0;

    /// <summary>Vibrato and the pitch envelope, in semitones, at a point in the note.</summary>
    public static double MotionAt(SynthPatch patch, double seconds)
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

    /// <summary>What to multiply a frequency by for an offset in semitones.</summary>
    public static double Ratio(double semitones) => Math.Pow(2.0, semitones / 12.0);
}
