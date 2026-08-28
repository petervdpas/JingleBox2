using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// A resonant low pass, one per channel of a voice. Takes the top off a bright sample or a
/// square wave, and with resonance up, rings at the cutoff the way a synth filter should.
/// </summary>
/// <remarks>
/// A topology-preserving state variable filter: stable at any cutoff, including the ones a
/// naive one-pole design blows up at, and cheap enough to run per sample per voice. The state
/// is two numbers, so a voice carries one of these per side and nothing else.
///
/// The coefficients are worked out once, in the constructor, which is what makes it cheap and
/// also what makes it the wrong filter for anything with an envelope or an LFO on it. That is
/// <see cref="SweepFilter"/>.
///
/// <see cref="Process"/> runs on the audio thread: no allocation, no lock, and no waiting on
/// anything, since the whole of its state belongs to the one voice that owns it.
/// </remarks>
public sealed class ToneFilter
{
    /// <summary>At or above this the filter is out of the way and is skipped entirely.</summary>
    public const double OpenHz = 20000;

    /// <summary>The bottom of the range: closed as far as a control is allowed to close it.</summary>
    public const double MinHz = 20;

    /// <summary>No ringing at all, which is a plain roll off.</summary>
    public const double MinResonance = 0;

    /// <summary>Short of self-oscillation: past this it rings louder than the note.</summary>
    public const double MaxResonance = 0.98;

    /// <summary>
    /// How close to half the sample rate a cutoff may be put before it counts as no filter.
    /// </summary>
    /// <remarks>
    /// Nothing above half the sample rate exists to be filtered, so a cutoff up there is the
    /// same as no filter at all. The margin keeps the tangent away from the point where it
    /// runs off to infinity, which is exactly at the half.
    /// </remarks>
    public const double NyquistMargin = 0.99;

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

    /// <summary>Wide open, so the voice can skip it rather than paying for a filter doing nothing.</summary>
    public bool IsOpen => _bypass;

    /// <summary>One sample through. On the audio thread, and open, it is a single comparison.</summary>
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
