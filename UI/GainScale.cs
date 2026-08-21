using System;

namespace JingleBox2.UI;

/// <summary>
/// A level as a fader reads it and as the audio engine wants it. Faders are marked in decibels
/// with unity at 0, which is what every desk does; the engine multiplies by an amplitude.
/// </summary>
public static class GainScale
{
    /// <summary>The bottom of a fader's travel. Anything at or below it is off.</summary>
    public const double MinimumDecibels = -60;

    /// <summary>Six decibels of headroom above unity, which is very nearly twice the amplitude.</summary>
    public const double MaximumDecibels = 6;

    public static double ToAmplitude(double decibels)
    {
        if (double.IsNaN(decibels) || decibels <= MinimumDecibels) return 0;

        return Math.Pow(10, Math.Min(decibels, MaximumDecibels) / 20);
    }

    public static double ToDecibels(double amplitude)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return MinimumDecibels;

        return Math.Clamp(20 * Math.Log10(amplitude), MinimumDecibels, MaximumDecibels);
    }
}
