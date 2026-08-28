using System;
using JingleBox2.Tracker.Synth.Enums;

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
///
/// That counting happens as samples go through rather than as the cutoff is set, which matters:
/// a filter set once and then left alone has to end up where it was put, and one set every
/// sixteenth sample has to as well. Counting the calls to <see cref="Set"/> instead would mean
/// the filter quietly ignoring whoever did not call it often enough.
///
/// One of these belongs to one voice on one side, and both <see cref="Set"/> and
/// <see cref="Process"/> run on the audio thread: nothing allocates, nothing locks, and nobody
/// else may touch it while a block is being rendered.
/// </remarks>
public sealed class SweepFilter
{
    /// <summary>How many samples pass between one working out of the coefficients and the next.</summary>
    private const int Interval = 16;

    private readonly double _rate;
    private readonly double _nyquist;

    /// <summary>The warped cutoff and the damping, which are what the coefficients come from.</summary>
    private double _g;
    private double _k;

    /// <summary>The three coefficients in use, good until the next <see cref="Recompute"/>.</summary>
    private double _a1, _a2, _a3;

    /// <summary>The whole of the filter's memory: two integrators, one sample deep each.</summary>
    private double _first;
    private double _second;

    /// <summary>Where the caller has asked the filter to be, which may be ahead of where it is.</summary>
    private double _wantCutoff;
    private double _wantResonance;

    /// <summary>
    /// Where the coefficients actually stand.
    /// </summary>
    /// <remarks>
    /// Started at a value no cutoff and no resonance can be, so the first
    /// <see cref="Recompute"/> always does the work rather than deciding nothing has moved.
    /// </remarks>
    private double _setCutoff = -1;
    private double _setResonance = -1;

    /// <summary>Samples left before the coefficients are worked out again.</summary>
    private int _since;

    /// <summary>Starts wide open, with the coefficients already worked out for it.</summary>
    public SweepFilter(int sampleRate)
    {
        _rate = sampleRate <= 0 ? 44100 : sampleRate;
        _nyquist = _rate / 2;

        Set(20000, 0);
        Recompute();
    }

    /// <summary>
    /// Where the filter should be. Cheap enough to call every sample and safe to call once.
    /// </summary>
    /// <remarks>
    /// Only writes down what was asked for. A value that is not a number at all reads as wide
    /// open rather than being carried into the coefficients, where it would poison every sample
    /// after it for the life of the voice.
    /// </remarks>
    public void Set(double cutoffHz, double resonance)
    {
        _wantCutoff = Math.Clamp(
            double.IsNaN(cutoffHz) ? 20000 : cutoffHz,
            ToneFilter.MinHz,
            Math.Min(ToneFilter.OpenHz, _nyquist * ToneFilter.NyquistMargin));

        _wantResonance = Math.Clamp(double.IsNaN(resonance) ? 0 : resonance, 0, ToneFilter.MaxResonance);
    }

    /// <summary>
    /// Works the coefficients out again, if the filter has moved since last time.
    /// </summary>
    /// <remarks>
    /// Half a hertz and a thousandth of the resonance range are both below anything a person
    /// could hear move, so a filter sitting still costs two comparisons rather than a tangent.
    /// </remarks>
    private void Recompute()
    {
        if (Math.Abs(_wantCutoff - _setCutoff) < 0.5 && Math.Abs(_wantResonance - _setResonance) < 0.001)
            return;

        _setCutoff = _wantCutoff;
        _setResonance = _wantResonance;

        _g = Math.Tan(Math.PI * _setCutoff / _rate);
        _k = 2.0 - 1.9 * _setResonance;

        _a1 = 1.0 / (1.0 + _g * (_g + _k));
        _a2 = _g * _a1;
        _a3 = _g * _a2;
    }

    /// <summary>
    /// One sample through, out of whichever end is asked for.
    /// </summary>
    /// <remarks>
    /// The low pass is the second state value; the high pass is what is left of the input once
    /// the low pass and the resonant band have been taken out of it, which is why both ends
    /// come off one filter rather than needing two.
    /// </remarks>
    public double Process(double input, FilterMode mode)
    {
        if (--_since <= 0)
        {
            _since = Interval;
            Recompute();
        }

        double third = input - _second;
        double v1 = _a1 * _first + _a2 * third;
        double v2 = _second + _a2 * _first + _a3 * third;

        _first = 2.0 * v1 - _first;
        _second = 2.0 * v2 - _second;

        return mode == FilterMode.HighPass ? input - _k * v1 - v2 : v2;
    }

    /// <summary>Forgets what it was ringing with, for a voice being started again.</summary>
    public void Reset()
    {
        _first = 0;
        _second = 0;
    }
}
