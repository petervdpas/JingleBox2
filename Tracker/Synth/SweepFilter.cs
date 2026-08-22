using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// A filter whose cutoff can be moved while it runs, open at either end.
/// </summary>
/// <remarks>
/// <see cref="ToneFilter"/> works out its coefficients once and keeps them, which is right for
/// a voice whose filter is set and left. A filter with an envelope or an LFO on it is a
/// different problem: the cutoff moves every sample, so the coefficients have to move with it.
///
/// The same state variable arrangement either way, which is what makes both ends available: the
/// low pass and the high pass fall out of the same two state values, so switching between them
/// costs a different line of arithmetic rather than a different filter.
///
/// The coefficients are worked out every <see cref="Interval"/> samples rather than every one.
/// A tangent per sample per voice is real money on the audio thread, and a cutoff that catches
/// up a third of a millisecond later is not a thing anybody has heard.
/// </remarks>
public sealed class SweepFilter
{
    /// <summary>How many samples pass between one working out of the coefficients and the next.</summary>
    private const int Interval = 16;

    private readonly double _rate;
    private readonly double _nyquist;

    private double _g;
    private double _k;
    private double _a1, _a2, _a3;

    private double _first;
    private double _second;

    private double _setCutoff = -1;
    private double _setResonance = -1;
    private int _since;

    public SweepFilter(int sampleRate)
    {
        _rate = sampleRate <= 0 ? 44100 : sampleRate;
        _nyquist = _rate / 2;

        Set(20000, 0);
    }

    /// <summary>Where the filter is now. Called per sample; most calls do nothing.</summary>
    public void Set(double cutoffHz, double resonance)
    {
        if (--_since > 0) return;

        _since = Interval;

        double cutoff = Math.Clamp(
            double.IsNaN(cutoffHz) ? 20000 : cutoffHz,
            ToneFilter.MinHz,
            Math.Min(ToneFilter.OpenHz, _nyquist * 0.99));

        double q = Math.Clamp(double.IsNaN(resonance) ? 0 : resonance, 0, ToneFilter.MaxResonance);

        if (Math.Abs(cutoff - _setCutoff) < 0.5 && Math.Abs(q - _setResonance) < 0.001) return;

        _setCutoff = cutoff;
        _setResonance = q;

        _g = Math.Tan(Math.PI * cutoff / _rate);
        _k = 2.0 - 1.9 * q;

        _a1 = 1.0 / (1.0 + _g * (_g + _k));
        _a2 = _g * _a1;
        _a3 = _g * _a2;
    }

    /// <summary>One sample through, out of whichever end is asked for.</summary>
    public double Process(double input, FilterMode mode)
    {
        double third = input - _second;
        double v1 = _a1 * _first + _a2 * third;
        double v2 = _second + _a2 * _first + _a3 * third;

        _first = 2.0 * v1 - _first;
        _second = 2.0 * v2 - _second;

        // The low pass is the second state value; the high pass is what is left of the input
        // once the low pass and the resonant band have been taken out of it.
        return mode == FilterMode.HighPass ? input - _k * v1 - v2 : v2;
    }

    /// <summary>Forgets what it was ringing with, for a voice being started again.</summary>
    public void Reset()
    {
        _first = 0;
        _second = 0;
    }
}
