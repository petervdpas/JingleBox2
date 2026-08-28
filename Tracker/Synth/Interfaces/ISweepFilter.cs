using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// A filter whose cutoff can be moved while it runs, open at either end.
/// </summary>
/// <remarks>
/// <see cref="IToneFilter"/> works out its coefficients once and keeps them, which is right for
/// a voice whose filter is set and left. A filter with an envelope or an LFO on it is a
/// different problem: the cutoff moves every sample, so the coefficients have to move with it.
///
/// The same state variable arrangement either way, which is what makes both ends available: the
/// low pass and the high pass fall out of the same two state values, so switching between them
/// costs a different line of arithmetic rather than a different filter.
///
/// The coefficients are worked out every sixteenth sample rather than every one. A tangent per
/// sample per voice is real money on the audio thread, and a cutoff that catches up a third of
/// a millisecond later is not a thing anybody has heard.
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
public interface ISweepFilter
{
    /// <summary>
    /// Where the filter should be. Cheap enough to call every sample and safe to call once.
    /// </summary>
    /// <remarks>
    /// Only writes down what was asked for. A value that is not a number at all reads as wide
    /// open rather than being carried into the coefficients, where it would poison every sample
    /// after it for the life of the voice.
    /// </remarks>
    /// <param name="cutoffHz">Where the filter should turn over, in hertz.</param>
    /// <param name="resonance">How hard it should ring at the cutoff, 0 for a plain roll off.</param>
    void Set(double cutoffHz, double resonance);

    /// <summary>
    /// One sample through, out of whichever end is asked for.
    /// </summary>
    /// <remarks>
    /// The low pass is the second state value; the high pass is what is left of the input once
    /// the low pass and the resonant band have been taken out of it, which is why both ends
    /// come off one filter rather than needing two.
    /// </remarks>
    /// <param name="input">The value going in.</param>
    /// <param name="mode">Which end to take, the low pass or the high pass.</param>
    double Process(double input, FilterMode mode);

    /// <summary>Forgets what it was ringing with, for a voice being started again.</summary>
    void Reset();
}
