using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class GainScale : IGainScale
{
    /// <inheritdoc cref="IGainScale.MinimumDecibels"/>
    public const double MinimumDecibels = -60;

    /// <inheritdoc cref="IGainScale.MaximumDecibels"/>
    public const double MaximumDecibels = 6;

    /// <inheritdoc/>
    double IGainScale.MinimumDecibels => MinimumDecibels;

    /// <inheritdoc/>
    double IGainScale.MaximumDecibels => MaximumDecibels;

    /// <inheritdoc/>
    public double ToAmplitude(double decibels)
    {
        if (double.IsNaN(decibels) || decibels <= MinimumDecibels) return 0;

        return Math.Pow(10, Math.Min(decibels, MaximumDecibels) / 20);
    }

    /// <inheritdoc/>
    public double ToDecibels(double amplitude)
    {
        if (double.IsNaN(amplitude) || amplitude <= 0) return MinimumDecibels;

        return Math.Clamp(20 * Math.Log10(amplitude), MinimumDecibels, MaximumDecibels);
    }
}
