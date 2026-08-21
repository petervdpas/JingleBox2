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
/// </remarks>
public sealed class ToneFilter
{
    /// <summary>At or above this the filter is out of the way and is skipped entirely.</summary>
    public const double OpenHz = 20000;

    public const double MinHz = 20;

    public const double MinResonance = 0;

    /// <summary>Short of self-oscillation: past this it rings louder than the note.</summary>
    public const double MaxResonance = 0.98;

    private readonly bool _bypass;
    private readonly double _a1;
    private readonly double _a2;
    private readonly double _a3;

    private double _first;
    private double _second;

    public ToneFilter(double cutoffHz, double resonance, int sampleRate)
    {
        double rate = sampleRate <= 0 ? 44100 : sampleRate;

        // Nothing above half the sample rate exists to be filtered, so a cutoff up there is
        // the same as no filter at all.
        double nyquist = rate / 2;
        double cutoff = Math.Clamp(double.IsNaN(cutoffHz) ? OpenHz : cutoffHz, MinHz, OpenHz);

        _bypass = cutoff >= OpenHz || cutoff >= nyquist * 0.99;
        if (_bypass) return;

        double g = Math.Tan(Math.PI * cutoff / rate);
        double k = 2.0 - 1.9 * Math.Clamp(resonance, MinResonance, MaxResonance);

        _a1 = 1.0 / (1.0 + g * (g + k));
        _a2 = g * _a1;
        _a3 = g * _a2;
    }

    public bool IsOpen => _bypass;

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
