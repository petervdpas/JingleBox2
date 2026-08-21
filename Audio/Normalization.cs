using System;

namespace JingleBox2.Audio;

/// <summary>
/// Peak normalization: finds the loudest moment in a recording and lifts the whole thing so
/// that moment sits just under full scale. One multiply per sample, and nothing about the
/// balance of the recording changes; it only stops being quiet.
/// </summary>
/// <remarks>
/// Peak rather than loudness on purpose. A peak measure is exact, reversible in the head
/// ("this went up 4 dB"), and cannot pump or clip. A loudness measure would sound more even
/// across takes but needs a limiter behind it to be safe, which is a different feature.
/// </remarks>
public static class Normalization
{
    /// <summary>Just under full scale. Room for a resampler to overshoot without clipping.</summary>
    public const double DefaultTargetDecibels = -1;

    public const double MinTargetDecibels = -24;
    public const double MaxTargetDecibels = 0;

    /// <summary>
    /// The most a recording can be lifted, about 40 dB. Past this the file is silence or
    /// close to it, and all that would come up is the noise floor.
    /// </summary>
    public const double MaxGain = 100;

    /// <summary>Below this a file counts as silent and is left alone.</summary>
    public const double SilenceAmplitude = 0.00001;

    /// <summary>The loudest sample in the file, 0 to 1.</summary>
    public static double PeakOf(short[]? samples)
    {
        if (samples == null || samples.Length == 0) return 0;

        int loudest = 0;

        foreach (short sample in samples)
        {
            // Widen before Abs: Math.Abs(short.MinValue) throws.
            int magnitude = Math.Abs((int)sample);
            if (magnitude > loudest) loudest = magnitude;
        }

        return loudest / 32768.0;
    }

    /// <summary>
    /// What to multiply every sample by to put the peak on the target. One means leave it
    /// alone, which is the answer for silence and for a file that is already there.
    /// </summary>
    public static double GainFor(double peak, double targetDecibels)
    {
        if (double.IsNaN(peak) || peak <= SilenceAmplitude) return 1;

        double target = ToAmplitude(Math.Clamp(
            double.IsNaN(targetDecibels) ? DefaultTargetDecibels : targetDecibels,
            MinTargetDecibels,
            MaxTargetDecibels));

        return Math.Clamp(target / peak, 1.0 / MaxGain, MaxGain);
    }

    /// <summary>Applies a gain in place, rounding to the nearest step and never wrapping round.</summary>
    public static void Apply(short[]? samples, double gain)
    {
        if (samples == null || samples.Length == 0) return;
        if (double.IsNaN(gain) || Math.Abs(gain - 1) < 0.000001) return;

        for (int i = 0; i < samples.Length; i++)
        {
            double scaled = Math.Round(samples[i] * gain);
            samples[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }
    }

    public static double ToDecibels(double amplitude) =>
        amplitude <= SilenceAmplitude ? MinTargetDecibels : 20 * Math.Log10(amplitude);

    public static double ToAmplitude(double decibels) => Math.Pow(10, decibels / 20.0);
}
