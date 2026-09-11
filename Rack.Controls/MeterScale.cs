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
    public double Decibels(double amplitude, double minimumDecibels = DefaultMinimumDecibels)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return minimumDecibels;

        double decibels = 20 * Math.Log10(Math.Min(amplitude, 1.0));
        return Math.Max(decibels, minimumDecibels);
    }

    /// <inheritdoc/>
    public double Position(double amplitude, double minimumDecibels = DefaultMinimumDecibels, bool decibels = true)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return 0;
        if (!decibels) return Math.Clamp(amplitude, 0, 1);

        double floor = minimumDecibels >= 0 ? DefaultMinimumDecibels : minimumDecibels;

        return Math.Clamp((Decibels(amplitude, floor) - floor) / -floor, 0, 1);
    }

    /// <inheritdoc/>
    public double DecayPeak(
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

    /// <inheritdoc/>
    /// <remarks>
    /// Rounded down, so the answer is the pixel the bar fills to rather than the one it is
    /// nearest. Anything that is not a number, and a meter with no room, land on nought: there
    /// is nothing to draw either way and nothing to redraw for.
    /// </remarks>
    public int Step(double amplitude, double minimumDecibels, double pixels)
    {
        if (double.IsNaN(amplitude) || double.IsNaN(pixels) || pixels < 1) return 0;

        return (int)(Position(amplitude, minimumDecibels) * pixels);
    }
}
