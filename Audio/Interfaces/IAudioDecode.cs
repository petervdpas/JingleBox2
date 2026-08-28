using System.Collections.Generic;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Turns a compressed recording into the samples this app works in.
/// </summary>
/// <remarks>
/// One thing only: read somebody's mp3, ogg or flac and hand back sixteen bit samples. Nothing
/// downstream ever meets one of those files, because nothing downstream is ever given one:
/// <see cref="IRecordingImport"/> decodes at the door and writes a WAV, and the shelf stays the
/// single format it has always been.
///
/// Through BASS, which is already here for playing pads and already knows these formats. Writing
/// three decoders would be three decoders to be wrong in, and the one already loaded is the one
/// the pads have been playing mp3s through all along.
/// </remarks>
public interface IAudioDecode
{
    /// <summary>
    /// What can be read, which depends on which add-ons are beside the program.
    /// </summary>
    /// <remarks>
    /// WAV is left out. It is read here, but this app has its own reader for it and that reader
    /// knows what a file was stored as, which is what decides whether it is copied or rewritten.
    /// </remarks>
    IReadOnlyList<string> Kinds { get; }

    /// <summary>True when this is a file to be decoded rather than read as a WAV.</summary>
    /// <param name="path">The file, wherever it is.</param>
    bool Handles(string path);

    /// <summary>
    /// Reads the whole thing, or nothing when it cannot be read.
    /// </summary>
    /// <remarks>
    /// A decoding channel rather than a playing one: BASS hands the samples back instead of
    /// sending them to a device, so this neither makes a sound nor needs one. Sixteen bit is
    /// what a channel gives without being asked, which is what this app keeps anyway.
    ///
    /// Nought back from a read is the end of the file, and below nought is BASS saying it went
    /// wrong, which for a file that is already open means a truncated one: what was read before
    /// it is still good and is kept.
    /// </remarks>
    /// <param name="path">The recording, in whichever of <see cref="Kinds"/> it is.</param>
    /// <returns>The samples and how to read them, or null when nothing could be read.</returns>
    (short[] Samples, int SampleRate, int Channels)? Read(string path);

    /// <summary>What a file could not be read as, in the words somebody is shown.</summary>
    /// <remarks>
    /// Two different messages, because they are two different problems with two different
    /// answers: a file of a kind this build can read that will not read is a damaged file, and a
    /// file of a kind it cannot read is a missing add-on.
    /// </remarks>
    /// <param name="path">The file that would not read.</param>
    string Trouble(string path);
}
