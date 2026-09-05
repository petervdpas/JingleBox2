namespace JingleBox2.SoundDevices.SoundEffects.Interfaces;

/// <summary>
/// What a saturation curve costs in loudness, measured off what actually went past, so it can be
/// given back.
/// </summary>
/// <remarks>
/// A curve's makeup is usually worked out from the curve: a hyperbolic tangent maps full scale to
/// full scale, so one over its value at full scale puts the peak back. That holds the height of a
/// wave and says nothing about its area, which is the whole of what a listener hears, and a drive
/// squares a wave up. The measured cost on a real synth patch is 5.6 dB of loudness added by a
/// control whose point is that it changes the tone.
///
/// A voice can do better than measuring, since it is handed the wave it is about to play and can
/// walk one period of it. An insert cannot: it is handed somebody's whole track a block at a time
/// and has no idea what is coming, so the only honest answer is what went past. Two running mean
/// squares, one either side of the curve, and the square root of their ratio.
///
/// One of these for a whole effect rather than one a side. A correction worked out per side is a
/// gain that differs between them and drifts with the programme, which moves the stereo image
/// about: a drive is not a place to invent width.
///
/// On the audio thread. Nothing here allocates, locks or branches on anything but its own two
/// numbers.
/// </remarks>
public interface ILoudnessMakeup
{
    /// <summary>
    /// Takes one sample from either side of the curve into the two followers.
    /// </summary>
    /// <remarks>
    /// Anything that is not a real number is passed over rather than let into a follower, since a
    /// running mean poisoned once stays poisoned for the rest of the run.
    /// </remarks>
    /// <param name="dry">What went into the curve.</param>
    /// <param name="wet">What came back out of it, before any makeup.</param>
    void Saw(double dry, double wet);

    /// <summary>
    /// What to multiply the curve's output by to leave it as loud as it arrived.
    /// </summary>
    /// <remarks>
    /// One while either follower is too faint to be believed, which is silence and the run-up to
    /// it: a ratio of two numbers that are both nearly nought is noise, and it would be applied to
    /// the first sample of whatever plays next.
    ///
    /// Bounded either way. The curve can only ever make a signal fuller, so the honest correction
    /// is downwards and the upward half is there for the moment the two followers disagree on the
    /// way into a sound; bounded so neither a silence nor a step hands the rest of the chain a
    /// multiplier nobody asked for.
    /// </remarks>
    double Makeup { get; }
}
