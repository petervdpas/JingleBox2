using System;

namespace JingleBox2.UI;

/// <summary>
/// Where a level sits on a meter. Amplitude is what the audio gives you; a meter that plots it
/// straight spends most of its length on the loudest few decibels and shows nothing useful
/// below half scale, so the decibel scale is the one worth reading.
/// </summary>
public static class MeterScale
{
    /// <summary>Quiet enough to be the bottom of the meter without hiding a soft take.</summary>
    public const double DefaultMinimumDecibels = -60;

    /// <summary>Amplitude at or above this is at the top, and worth a warning.</summary>
    public const double ClipAmplitude = 0.999;

    /// <summary>Amplitude as decibels below full scale. Silence is treated as the floor.</summary>
    public static double Decibels(double amplitude, double minimumDecibels = DefaultMinimumDecibels)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return minimumDecibels;

        double decibels = 20 * Math.Log10(Math.Min(amplitude, 1.0));
        return Math.Max(decibels, minimumDecibels);
    }

    /// <summary>How far up the meter a level reaches, 0 to 1.</summary>
    public static double Position(double amplitude, double minimumDecibels = DefaultMinimumDecibels, bool decibels = true)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return 0;
        if (!decibels) return Math.Clamp(amplitude, 0, 1);

        double floor = minimumDecibels >= 0 ? DefaultMinimumDecibels : minimumDecibels;

        return Math.Clamp((Decibels(amplitude, floor) - floor) / -floor, 0, 1);
    }

    /// <summary>
    /// A peak mark that falls back at a steady rate rather than sticking. Held for a moment
    /// first, so a transient is readable before it starts to drop.
    /// </summary>
    public static double DecayPeak(
        double peak,
        double level,
        double secondsSincePeak,
        double holdSeconds,
        double decibelsPerSecond)
    {
        if (level >= peak) return Math.Clamp(level, 0, 1);
        if (secondsSincePeak <= holdSeconds) return peak;

        double fallen = Decibels(peak) - (secondsSincePeak - holdSeconds) * decibelsPerSecond;

        return Math.Max(level, Math.Pow(10, fallen / 20));
    }
}
