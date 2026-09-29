namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What a block of interleaved stereo floats peaked at, each side.
/// </summary>
/// <remarks>
/// A rule of its own because it is what a meter is, and because it is read on whichever thread
/// is mixing: a walk over a block that allocates nothing can be put a question to here without a
/// sound card, a bus or a driver, which is the one place the answer would otherwise only be
/// visible as a bar that did or did not move.
///
/// <see cref="IStereoFloats"/> is what fills a block like this from a capture. This is the other
/// question anybody asks of one, which is how loud it was.
/// </remarks>
public interface IStereoPeak
{
    /// <summary>
    /// The loudest sample a side, as a magnitude where one is full scale.
    /// </summary>
    /// <remarks>
    /// **A sample that is not a number is passed over rather than becoming the peak.** Every
    /// comparison against NaN is false, so it loses each one it is in, which is what is wanted:
    /// one bad sample must not pin a meter at the top for the rest of the session.
    ///
    /// **Past full scale is reported as it is**, since a meter here is a desk meter whose scale
    /// runs above 0 dB, and a bus summed in floating point really can be there. Held only at
    /// <see cref="Loudest"/>, so an infinite sample reads as very loud rather than as infinity.
    /// A float on the end with nothing to pair with is left,
    /// since half a frame is not one, and a count past the end of the block is held to the block.
    /// </remarks>
    /// <param name="block">Interleaved stereo floats, left then right.</param>
    /// <param name="floats">How many entries of it are real.</param>
    (float Left, float Right) Of(float[]? block, int floats);

    /// <summary>
    /// The most a reading says, +24 dB, which is further past full scale than any meter's top.
    /// </summary>
    const float Loudest = 16f;
}
