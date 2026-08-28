namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// Everything that moves a voice away from the note it was given: the instrument's own tuning,
/// which holds still, and the vibrato and pitch envelope, which do not.
/// </summary>
/// <remarks>
/// Shared by the voice that plays a patch and the scope that draws it, so a pitch envelope
/// bends the picture exactly as far as it bends the sound.
///
/// Nothing here is kept between calls. The whole of what an answer depends on is the patch and
/// the moment, both handed in, which is what lets the drawing thread ask the same question the
/// audio thread is asking and get the same answer.
/// </remarks>
public interface IPitchMotion
{
    /// <summary>The instrument's fixed offset, in semitones. The same for every note and moment.</summary>
    /// <param name="patch">The instrument being played, or null for no tuning at all.</param>
    double Tuning(SynthPatch patch);

    /// <summary>Vibrato and the pitch envelope, in semitones, at a point in the note.</summary>
    /// <param name="patch">The instrument being played, or null for no movement at all.</param>
    /// <param name="seconds">How far into the note, which is what both the vibrato and the envelope are read at.</param>
    double MotionAt(SynthPatch patch, double seconds);

    /// <summary>What to multiply a frequency by for an offset in semitones.</summary>
    /// <param name="semitones">The offset, which may be a fraction and may be negative.</param>
    double Ratio(double semitones);
}
