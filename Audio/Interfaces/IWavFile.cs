using System;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Reads the WAV files people have, writes the 16-bit ones this app keeps.
/// </summary>
/// <remarks>
/// Reading is generous and writing is not, on purpose. A sample folder is full of 24-bit and
/// float files saved by editors that default to them, and refusing those means a quarter of
/// somebody's own samples will not load with nothing said about why. What the app keeps is one
/// format, because everything downstream, the trim, the normalise, the voices, works in shorts.
/// Anything else is turned into that on the way in and never seen again.
///
/// Everything here throws rather than answering with nothing, because a file that will not read
/// is something to be said out loud: a take that quietly becomes silence is worse than a take
/// that refuses, and the caller is always somewhere that can put a message on a page.
/// </remarks>
public interface IWavFile
{
    /// <summary>What this app keeps, and the only width it writes.</summary>
    int BitsPerSample { get; }

    /// <summary>Reads just the headers, without pulling the audio into memory.</summary>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
    WavInfo ReadInfo(string filePath);

    /// <summary>How the samples are laid out in the file, before anything is turned into shorts.</summary>
    /// <remarks>
    /// For the one caller that has to know whether a file is already what this app keeps: an
    /// import of a 16-bit file is a copy, and an import of anything else has to be decoded and
    /// written out again.
    /// </remarks>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
    WavStored StoredAs(string filePath);

    /// <summary>The whole file, as shorts, whatever it was written as.</summary>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
    (short[] Samples, WavInfo Info) Read(string filePath);

    /// <summary>Writes shorts out as a 16-bit WAV.</summary>
    /// <param name="filePath">Where it goes. Its folder is expected to be there already.</param>
    /// <param name="samples">The audio, interleaved when there is more than one channel.</param>
    /// <param name="sampleRate">Frames a second.</param>
    /// <param name="channels">How many samples one frame holds.</param>
    void Write(string filePath, short[] samples, int sampleRate, int channels);

    /// <summary>The same, for audio that is already the bytes a 16-bit WAV holds.</summary>
    /// <remarks>
    /// For the recorder, which is handed blocks by the capture device and has no reason to widen
    /// them into shorts and narrow them again on the way out.
    /// </remarks>
    /// <param name="filePath">Where it goes. Its folder is expected to be there already.</param>
    /// <param name="pcmData">The audio, as it will sit in the file.</param>
    /// <param name="sampleRate">Frames a second.</param>
    /// <param name="channels">How many samples one frame holds.</param>
    void Write(string filePath, byte[] pcmData, int sampleRate, int channels);
}
