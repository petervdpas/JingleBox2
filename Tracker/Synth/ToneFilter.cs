using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
/// <remarks>
/// The coefficients are worked out in the constructor and are readonly after it, so a voice
/// that never moves its filter pays for one tangent and nothing else.
/// </remarks>
public sealed class ToneFilter : IToneFilter
{
    /// <inheritdoc cref="IToneFilter.OpenHz"/>
    public const double OpenHz = 20000;

    /// <inheritdoc/>
    double IToneFilter.OpenHz => OpenHz;

    /// <inheritdoc cref="IToneFilter.MinHz"/>
    public const double MinHz = 20;

    /// <inheritdoc/>
    double IToneFilter.MinHz => MinHz;

    /// <inheritdoc cref="IToneFilter.MinResonance"/>
    public const double MinResonance = 0;

    /// <inheritdoc/>
    double IToneFilter.MinResonance => MinResonance;

    /// <inheritdoc cref="IToneFilter.MaxResonance"/>
    public const double MaxResonance = 0.98;

    /// <inheritdoc/>
    double IToneFilter.MaxResonance => MaxResonance;

    /// <inheritdoc cref="IToneFilter.NyquistMargin"/>
    public const double NyquistMargin = 0.99;

    /// <inheritdoc/>
    double IToneFilter.NyquistMargin => NyquistMargin;

    private readonly bool _bypass;

    /// <summary>The three coefficients the state variable form needs, fixed for this voice.</summary>
    private readonly double _a1;
    private readonly double _a2;
    private readonly double _a3;

    /// <summary>The whole of the filter's memory: two integrators, one sample deep each.</summary>
    private double _first;
    private double _second;

    /// <summary>
    /// Works the filter out for one voice, at that voice's rate.
    /// </summary>
    /// <remarks>
    /// A cutoff that is not a number at all reads as wide open rather than poisoning every
    /// sample the voice will ever produce: a patch off disc can hold anything.
    /// </remarks>
    /// <param name="cutoffHz">Where the filter turns over, in hertz, as the patch holds it.</param>
    /// <param name="resonance">How hard it rings at the cutoff, held between <see cref="MinResonance"/> and <see cref="MaxResonance"/>.</param>
    /// <param name="sampleRate">The rate the voice is rendered at, which the coefficients are worked out against.</param>
    public ToneFilter(double cutoffHz, double resonance, int sampleRate)
    {
        double rate = sampleRate <= 0 ? 44100 : sampleRate;

        double nyquist = rate / 2;
        double cutoff = Math.Clamp(double.IsNaN(cutoffHz) ? OpenHz : cutoffHz, MinHz, OpenHz);

        _bypass = cutoff >= OpenHz || cutoff >= nyquist * NyquistMargin;
        if (_bypass) return;

        double g = Math.Tan(Math.PI * cutoff / rate);
        double k = 2.0 - 1.9 * Math.Clamp(resonance, MinResonance, MaxResonance);

        _a1 = 1.0 / (1.0 + g * (g + k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    /// <inheritdoc/>
    public bool IsOpen => _bypass;

    /// <inheritdoc/>
    public double Process(double input)
    {
        if (_bypass) return input;

        double third = input - _second;
        double v1 = _a1 * _first + _a2 * third;
        double v2 = _second + _a2 * _first + _a3 * third;

        _first = 2.0 * v1 - _first;
        _second = 2.0 * v2 - _second;

        return v2;
    }
}
