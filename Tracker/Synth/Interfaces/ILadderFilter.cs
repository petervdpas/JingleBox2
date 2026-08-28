namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// A four pole resonant low pass: two sweeping filters, one after the other.
/// </summary>
/// <remarks>
/// What an Emulator II put every voice through, and most of why one sounds like one. The
/// machine was a digital sampler with an analogue synthesiser behind it: an SSM2045 four pole
/// low pass per voice with its own envelope, so a recording was material to be shaped rather
/// than a finished sound to be replayed.
///
/// Two poles roll off six decibels an octave apiece, so a pair of them is the twenty four the
/// chip gave. Cascading the sweeping filter rather than writing a ladder keeps one filter in
/// the codebase rather than two, and the ear cannot tell a cascaded pair from a ladder without
/// the ladder's own distortion, which is a separate thing to add and not this.
///
/// One per side of one voice, run on the audio thread. It holds only the two stages' own
/// state, allocates nothing per sample and takes no lock: a voice belongs to whichever thread
/// is rendering it.
/// </remarks>
public interface ILadderFilter
{
    /// <summary>
    /// Where it turns over and how hard it rings there.
    /// </summary>
    /// <remarks>
    /// The resonance goes on the first stage alone. Put on both, a pair of ringing filters at
    /// the same frequency multiply into a peak that swamps everything under it long before the
    /// control reaches the top.
    /// </remarks>
    /// <param name="cutoffHz">Where the filter turns over, in hertz.</param>
    /// <param name="resonance">How hard it rings at the cutoff, 0 for a plain roll off.</param>
    void Set(double cutoffHz, double resonance);

    /// <summary>One sample through both stages, out of the low end.</summary>
    /// <param name="input">The value going in.</param>
    double Process(double input);

    /// <summary>Forgets what both stages were ringing with, for a voice being started again.</summary>
    void Reset();
}
