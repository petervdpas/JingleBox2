using System;

namespace JingleBox2.Tracker.Synth;

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
public sealed class LadderFilter
{
    private readonly SweepFilter _first;
    private readonly SweepFilter _second;

    /// <summary>Both stages start wide open, so a voice is never born filtered by accident.</summary>
    public LadderFilter(int sampleRate)
    {
        _first = new SweepFilter(sampleRate);
        _second = new SweepFilter(sampleRate);
    }

    /// <summary>
    /// Where it turns over and how hard it rings there.
    /// </summary>
    /// <remarks>
    /// The resonance goes on the first stage alone. Put on both, a pair of ringing filters at
    /// the same frequency multiply into a peak that swamps everything under it long before the
    /// control reaches the top.
    /// </remarks>
    public void Set(double cutoffHz, double resonance)
    {
        _first.Set(cutoffHz, resonance);
        _second.Set(cutoffHz, 0);
    }

    /// <summary>One sample through both stages, out of the low end.</summary>
    public double Process(double input) =>
        _second.Process(_first.Process(input, FilterMode.LowPass), FilterMode.LowPass);

    /// <summary>Forgets what both stages were ringing with, for a voice being started again.</summary>
    public void Reset()
    {
        _first.Reset();
        _second.Reset();
    }
}
