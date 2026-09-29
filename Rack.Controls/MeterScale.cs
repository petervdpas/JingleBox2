using System;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class MeterScale : IMeterScale
{
    /// <inheritdoc cref="IMeterScale.DefaultMinimumDecibels"/>
    public const double DefaultMinimumDecibels = IMeterScale.DefaultMinimumDecibels;

    /// <inheritdoc cref="IMeterScale.ClipAmplitude"/>
    public const double ClipAmplitude = IMeterScale.ClipAmplitude;



    /// <inheritdoc/>
    public double Decibels(double amplitude, double minimumDecibels = DefaultMinimumDecibels,
                           double maximumDecibels = IMeterScale.FullScaleDecibels)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return minimumDecibels;

        double decibels = 20 * Math.Log10(Math.Min(amplitude, Ceiling(maximumDecibels)));
        return Math.Max(decibels, minimumDecibels);
    }

    /// <summary>The amplitude the top of a scale stands for.</summary>
    /// <param name="maximumDecibels">The top, never under full scale.</param>
    /// <returns>1 for full scale, more for a scale with room above it.</returns>
    private static double Ceiling(double maximumDecibels) =>
        double.IsNaN(maximumDecibels) || maximumDecibels <= 0 ? 1.0 : Math.Pow(10, maximumDecibels / 20);

    /// <inheritdoc/>
    public double Position(double amplitude, double minimumDecibels = DefaultMinimumDecibels, bool decibels = true,
                           double maximumDecibels = IMeterScale.FullScaleDecibels)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return 0;
        if (!decibels) return Math.Clamp(amplitude, 0, 1);

        double floor = minimumDecibels >= 0 ? DefaultMinimumDecibels : minimumDecibels;
        double top = double.IsNaN(maximumDecibels) || maximumDecibels < 0 ? 0 : maximumDecibels;

        return Math.Clamp((Decibels(amplitude, floor, top) - floor) / (top - floor), 0, 1);
    }

    /// <inheritdoc/>
    public double DecayPeak(
        double peak,
        double level,
        double secondsSincePeak,
        double holdSeconds,
        double decibelsPerSecond,
        double maximumDecibels = IMeterScale.FullScaleDecibels)
    {
        if (level >= peak) return Math.Clamp(level, 0, Ceiling(maximumDecibels));
        if (secondsSincePeak <= holdSeconds) return peak;

        double fallen = Decibels(peak, DefaultMinimumDecibels, maximumDecibels)
                        - (secondsSincePeak - holdSeconds) * decibelsPerSecond;

        return Math.Max(level, Math.Pow(10, fallen / 20));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Rounded down, so the answer is the pixel the bar fills to rather than the one it is
    /// nearest. Anything that is not a number, and a meter with no room, land on nought: there
    /// is nothing to draw either way and nothing to redraw for.
    /// </remarks>
    public int Step(double amplitude, double minimumDecibels, double pixels,
                    double maximumDecibels = IMeterScale.FullScaleDecibels)
    {
        if (double.IsNaN(amplitude) || double.IsNaN(pixels) || pixels < 1) return 0;

        return (int)(Position(amplitude, minimumDecibels, true, maximumDecibels) * pixels);
    }
}
