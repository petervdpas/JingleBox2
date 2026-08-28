namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// A resonant low pass, one per channel of a voice. Takes the top off a bright sample or a
/// square wave, and with resonance up, rings at the cutoff the way a synth filter should.
/// </summary>
/// <remarks>
/// A topology-preserving state variable filter: stable at any cutoff, including the ones a
/// naive one-pole design blows up at, and cheap enough to run per sample per voice. The state
/// is two numbers, so a voice carries one of these per side and nothing else.
///
/// The coefficients are worked out once, when the filter is made, which is what makes it cheap
/// and also what makes it the wrong filter for anything with an envelope or an LFO on it. That
/// is <see cref="ISweepFilter"/>.
///
/// <see cref="Process"/> runs on the audio thread: no allocation, no lock, and no waiting on
/// anything, since the whole of its state belongs to the one voice that owns it.
/// </remarks>
public interface IToneFilter
{
    /// <summary>At or above this the filter is out of the way and is skipped entirely.</summary>
    double OpenHz { get; }

    /// <summary>The bottom of the range: closed as far as a control is allowed to close it.</summary>
    double MinHz { get; }

    /// <summary>No ringing at all, which is a plain roll off.</summary>
    double MinResonance { get; }

    /// <summary>Short of self-oscillation: past this it rings louder than the note.</summary>
    double MaxResonance { get; }

    /// <summary>
    /// How close to half the sample rate a cutoff may be put before it counts as no filter.
    /// </summary>
    /// <remarks>
    /// Nothing above half the sample rate exists to be filtered, so a cutoff up there is the
    /// same as no filter at all. The margin keeps the tangent away from the point where it
    /// runs off to infinity, which is exactly at the half.
    /// </remarks>
    double NyquistMargin { get; }

    /// <summary>Wide open, so the voice can skip it rather than paying for a filter doing nothing.</summary>
    bool IsOpen { get; }

    /// <summary>One sample through. On the audio thread, and open, it is a single comparison.</summary>
    /// <param name="input">The value going in.</param>
    double Process(double input);

    /// <summary>
    /// Empties the two integrators, so the next sample is heard by a filter with no memory.
    /// </summary>
    /// <remarks>
    /// The coefficients are fixed and are not touched: what is cleared is the history, not the
    /// setting. The state feeds back into itself, so one value that is not a number stays in
    /// both integrators for ever, and this is the only way out of that short of a new voice.
    /// Both of the other two filters have had one from the start.
    /// </remarks>
    void Reset();
}
