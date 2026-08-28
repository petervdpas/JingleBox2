using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class Normalization : INormalization
{
    /// <inheritdoc cref="INormalization.DefaultTargetDecibels"/>
    public const double Target = -1;

    /// <inheritdoc cref="INormalization.MinTargetDecibels"/>
    public const double Quietest = -24;

    /// <inheritdoc cref="INormalization.MaxTargetDecibels"/>
    public const double Loudest = 0;

    /// <inheritdoc cref="INormalization.MaxGain"/>
    public const double MostGain = 100;

    /// <inheritdoc cref="INormalization.SilenceAmplitude"/>
    public const double Silence = 0.00001;

    /// <inheritdoc/>
    double INormalization.DefaultTargetDecibels => Target;

    /// <inheritdoc/>
    double INormalization.MinTargetDecibels => Quietest;

    /// <inheritdoc/>
    double INormalization.MaxTargetDecibels => Loudest;

    /// <inheritdoc/>
    double INormalization.MaxGain => MostGain;

    /// <inheritdoc/>
    double INormalization.SilenceAmplitude => Silence;

    /// <inheritdoc/>
    public double PeakOf(short[]? samples)
    {
        if (samples == null || samples.Length == 0) return 0;

        int loudest = 0;

        foreach (short sample in samples)
        {
            int magnitude = Math.Abs((int)sample);
            if (magnitude > loudest) loudest = magnitude;
        }

        return loudest / 32768.0;
    }

    /// <inheritdoc/>
    public double GainFor(double peak, double targetDecibels)
    {
        if (double.IsNaN(peak) || peak <= Silence) return 1;

        double target = ToAmplitude(Math.Clamp(
            double.IsNaN(targetDecibels) ? Target : targetDecibels,
            Quietest,
            Loudest));

        return Math.Clamp(target / peak, 1.0 / MostGain, MostGain);
    }

    /// <inheritdoc/>
    public void Apply(short[]? samples, double gain)
    {
        if (samples == null || samples.Length == 0) return;
        if (double.IsNaN(gain) || Math.Abs(gain - 1) < 0.000001) return;

        for (int i = 0; i < samples.Length; i++)
        {
            double scaled = Math.Round(samples[i] * gain);
            samples[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }
    }

    /// <inheritdoc/>
    public double ToDecibels(double amplitude) =>
        amplitude <= Silence ? Quietest : 20 * Math.Log10(amplitude);

    /// <inheritdoc/>
    public double ToAmplitude(double decibels) => Math.Pow(10, decibels / 20.0);
}
