using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The ASIO drivers on this machine, and the one that is open.
/// </summary>
/// <remarks>
/// ASIO is a driver written for the card rather than an endpoint offered by the operating
/// system, and that is the whole of why it is worth having: the system's mixer is not in the
/// path, so the buffer is the card's own and the delay is a handful of milliseconds instead of
/// twenty. It is Steinberg's and in practice it is Windows only.
///
/// It is also all or nothing. A driver owns the card while it is open, so there is one of these
/// at a time and picking one takes the audio away from the system's own path completely.
///
/// Behind an interface because it is a native library that is not on every machine: there is no
/// <c>bassasio</c> on Linux, and on Windows it is a file somebody has to have put beside the
/// program. Everything above here therefore has to work when the answer is "none", which is what
/// <see cref="Present"/> says, and the list is empty rather than an error.
/// </remarks>
public interface IAsioDevices
{
    /// <summary>
    /// True when the ASIO library is really here and could be asked.
    /// </summary>
    /// <remarks>
    /// False on a machine with no <c>bassasio</c> beside the program, which is every Linux
    /// machine and any Windows one where the file was not shipped. Said rather than guessed from
    /// the platform, since a Windows machine without the file behaves exactly like a Linux one.
    /// </remarks>
    bool Present { get; }

    /// <summary>Why it is not here, in words for a person, or nothing when it is.</summary>
    string Missing { get; }

    /// <summary>The drivers, in the order ASIO lists them. Empty when there are none.</summary>
    IReadOnlyList<AudioOutput> Devices { get; }

    /// <summary>
    /// Opens one and starts feeding it from a BASS stream.
    /// </summary>
    /// <remarks>
    /// The stream has to be a decoding one: ASIO pulls from it, so anything BASS was playing on
    /// its own would be the same audio coming out twice by two routes.
    ///
    /// The stream is put on the first pair of outputs, joined so one call carries both. A card
    /// with more outputs than that is left alone: what goes where is a routing question and a
    /// stereo mix has one answer.
    ///
    /// **How big a block is is the driver's to say, and there is no parameter for it.** An ASIO
    /// driver has its own control panel and that is where a card's block size is set; the driver
    /// then reports that setting as the one it prefers. A host that overrides it is fighting the
    /// panel, and what comes out is a card running blocks it was never set up for. The size this
    /// program's own buffer slider names is a BASS setting about a shared output path, which is
    /// exactly the path ASIO takes out of the picture, so it has no business here.
    ///
    /// The rate is what the mix is made at rather than an order. A card that will not move to it,
    /// which is any card clocked from outside, keeps the rate it is on and the mix is resampled
    /// into it: the alternative is a stream pulled at a rate it was not made at, which is the
    /// whole song playing sharp with nothing anywhere saying why.
    /// </remarks>
    /// <param name="index">Which driver, counting from nought.</param>
    /// <param name="stream">The decoding BASS stream to pull from.</param>
    /// <param name="rate">The rate the mix is made at.</param>
    bool Open(int index, int stream, int rate);

    /// <summary>Stops and lets the driver go, which gives the card back.</summary>
    void Close();

    /// <summary>How far behind the card is, in frames, or nought when nothing is open.</summary>
    int Latency { get; }

    /// <summary>
    /// How many frames a block is, as the driver has it, or nought when nothing is open.
    /// </summary>
    /// <remarks>
    /// Read back rather than remembered from what was asked for, since nothing is asked for: this
    /// is the number in the driver's own panel, and it is the one a person looking at that panel
    /// expects to see said back to them.
    /// </remarks>
    int Frames { get; }

    /// <summary>
    /// What the card is really running at, in hertz, or nought when nothing is open.
    /// </summary>
    /// <remarks>
    /// Not necessarily the rate it was opened with. A card clocked from something else refuses to
    /// be moved, and this says what it settled on.
    /// </remarks>
    int Rate { get; }
}
