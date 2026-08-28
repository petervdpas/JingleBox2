using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class FrequencyScale : IFrequencyScale
{
    /// <inheritdoc cref="IFrequencyScale.MinHz"/>
    public const double MinHz = 20;

    /// <inheritdoc cref="IFrequencyScale.MaxHz"/>
    public const double MaxHz = 20000;

    /// <inheritdoc/>
    double IFrequencyScale.MinHz => MinHz;

    /// <inheritdoc/>
    double IFrequencyScale.MaxHz => MaxHz;

    /// <inheritdoc/>
    public double ToPosition(double hz)
    {
        if (double.IsNaN(hz)) return 1;

        double clamped = Math.Clamp(hz, MinHz, MaxHz);
        return Math.Log(clamped / MinHz) / Math.Log(MaxHz / MinHz);
    }

    /// <inheritdoc/>
    public double ToHz(double position)
    {
        if (double.IsNaN(position)) return MaxHz;

        double clamped = Math.Clamp(position, 0, 1);
        return MinHz * Math.Pow(MaxHz / MinHz, clamped);
    }

    /// <inheritdoc/>
    public string Text(double hz)
    {
        if (double.IsNaN(hz)) return "-";
        if (hz >= MaxHz) return "off";

        return hz >= 1000
            ? (hz / 1000).ToString("0.0") + " kHz"
            : hz.ToString("0") + " Hz";
    }
}
