using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// A per sample ADSR. Linear segments: at these timescales the shape matters far less than
/// getting the lengths right, and linear keeps the stage boundaries exact.
/// </summary>
/// <remarks>
/// One per voice, and for Zampler two: the loudness and the brightness are not the same shape,
/// so the filter runs a second one of these and only differs in what it does with the answer.
///
/// <see cref="Next"/> is called once per sample per voice on the audio thread. It allocates
/// nothing and takes no lock, because the envelope belongs to the voice being rendered and to
/// nobody else.
/// </remarks>
public interface ISynthEnvelope
{
    /// <summary>Which segment it is in, which is how a voice knows a key is still being held.</summary>
    EnvelopeStage Stage { get; }

    /// <summary>Silent and done, so the voice holding this can be dropped.</summary>
    bool IsFinished { get; }

    /// <summary>Where it stands now, without moving it on.</summary>
    double Level { get; }

    /// <summary>Advances one sample and returns the level to multiply that sample by.</summary>
    double Next();

    /// <summary>
    /// Releases from wherever the level happens to be, so a key up never clicks. A shorter
    /// release can be forced, which is how a retrigger cuts the voice it replaces.
    /// </summary>
    /// <remarks>
    /// There is nothing to hold at nought, so a patch with no sustain ends on its decay and a
    /// note off that arrives after that finds the envelope already finished and does nothing.
    /// </remarks>
    /// <param name="releaseSeconds">A release to use instead of the patch's own, or null for the patch's.</param>
    void NoteOff(double? releaseSeconds = null);

    /// <summary>Cuts the voice dead, for a stop button rather than a note off.</summary>
    void Kill();
}
