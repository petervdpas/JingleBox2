using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>Brings a block of captured audio to the form everything here meets.</summary>
/// <remarks>
/// **Sixteen bit interleaved, whatever the capture handed over.** The gain, the clip lamp, the
/// meter, the take buffer and the WAV writer are all written against that one shape, which is
/// what lets a capture device, an output's monitor and one program's audio go through the same
/// path without any of them knowing about the others.
///
/// A rule of its own because it is arithmetic on bytes and can be put a question to without a
/// sound card, and because the case that matters cannot be tested any other way: a per-process
/// capture on Windows cannot be asked what the device mixes at, so what arrives is whatever was
/// asked for and is commonly floats.
/// </remarks>
public interface ISixteenBit
{
    /// <summary>
    /// Reads a block into sixteen bit interleaved samples.
    /// </summary>
    /// <remarks>
    /// The channel count is left as it is: how many channels a take holds is the capture's
    /// business and the meter is already told. A block that is already sixteen bit integers is
    /// handed straight back, since a copy there would be work done on the capture's own thread
    /// for nothing.
    ///
    /// A float past full scale is held rather than allowed to wrap, which is the one difference
    /// that matters: the wrap is a loud crack and the hold is what every converter does anyway.
    /// Anything that is not a number is written as silence, the same rule the take effects keep.
    /// </remarks>
    /// <param name="block">The audio as it arrived.</param>
    /// <param name="count">How many bytes of it are real.</param>
    /// <param name="from">What those bytes are made of.</param>
    byte[] Down(byte[] block, int count, CaptureFormat from);

    /// <summary>
    /// The same reading, into a buffer the caller keeps.
    /// </summary>
    /// <remarks>
    /// **For the one caller that is on the mixing thread**, which is where this application is
    /// careful to allocate nothing: the render path is measured at a hundredth of a megabyte a
    /// second and the whole of the pattern grid's history is what happens when something on a
    /// drawing thread stops being careful. A block per block is small, and small on that thread
    /// is still a decision rather than an accident.
    ///
    /// Not a second spelling of the arithmetic. <see cref="Down(byte[], int, CaptureFormat)"/>
    /// works out the room, allocates and calls this, so there is one place that reads a float,
    /// one place that rounds it and one place that says what full scale is.
    ///
    /// Nought back where the buffer is too small, rather than as much as would fit: half a block
    /// written into a take is a hole nobody would hear as a fault.
    /// </remarks>
    /// <param name="block">The audio as it arrived.</param>
    /// <param name="count">How many bytes of it are real.</param>
    /// <param name="from">What those bytes are made of.</param>
    /// <param name="into">Where the samples go, which must hold <see cref="Room"/> of them.</param>
    /// <returns>How many bytes of <paramref name="into"/> were written.</returns>
    int Down(byte[]? block, int count, CaptureFormat from, byte[]? into);

    /// <summary>How many bytes that many of that shape come to once read.</summary>
    /// <param name="count">How many bytes are real.</param>
    /// <param name="from">What those bytes are made of.</param>
    int Room(int count, CaptureFormat from);
}
