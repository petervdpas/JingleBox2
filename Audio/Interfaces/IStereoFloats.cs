namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What arrives from a capture, as the interleaved stereo floats an effect and a bus deal in.
/// </summary>
/// <remarks>
/// Everything that captures here hands over 16 bit samples, whichever path it came down: a
/// microphone through BASS, an output's monitor, or a program of its own through the loopback.
/// <see cref="ISixteenBit"/> is the journey the other way, which is how a take is written, and
/// this is the one back, which is how what is coming in is heard.
///
/// **Stereo whatever arrived**, because the two things at the far end of this both say stereo:
/// <see cref="Plugins.Interfaces.IAudioInsert"/> is documented as an interleaved stereo block,
/// and the bus is opened two wide. A mono capture is written to both sides rather than left
/// half a signal, which is what a desk does with a microphone on a mono channel.
///
/// A rule of its own because it is arithmetic over bytes and every way it goes wrong is quiet:
/// the two halves of a sample the wrong way round is noise, an unsigned read is a signal sitting
/// half a scale off nought, and the wrong divisor is a monitor that is nearly right and always
/// too loud. None of those looks like a fault in code.
/// </remarks>
public interface IStereoFloats
{
    /// <summary>
    /// Turns a block of 16 bit samples into interleaved stereo floats, -1 to 1.
    /// </summary>
    /// <remarks>
    /// A byte on the end with nothing to pair with is left, since half a sample is not one. What
    /// is written is <paramref name="into"/> from nought, and what is not written is untouched:
    /// the answer is how many entries are real rather than the length of the array, so one
    /// buffer can serve every block whatever size they arrive in.
    /// </remarks>
    /// <param name="data">The captured bytes, little end first, signed.</param>
    /// <param name="bytes">How many of them are real.</param>
    /// <param name="channels">How wide the capture is, one or more.</param>
    /// <param name="into">Where the floats go, which must hold two for every frame.</param>
    /// <returns>How many entries of <paramref name="into"/> were written.</returns>
    int Read(byte[] data, int bytes, int channels, float[] into);

    /// <summary>How many floats a block of that many bytes at that width comes to.</summary>
    /// <param name="bytes">How many bytes are real.</param>
    /// <param name="channels">How wide the capture is.</param>
    int Room(int bytes, int channels);
}
