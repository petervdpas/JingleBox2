using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Brings recordings in from anywhere on the disc by copying them into JingleBox's own folder.
/// </summary>
/// <remarks>
/// The machines only play recordings JingleBox holds, and this is the one door in. A sample
/// that lives in somebody's downloads folder is a song waiting to break: the folder gets tidied
/// and the kit goes silent, which is exactly what happened to the first kit built here. Copied
/// in, the file is ours, and a song depending on it depends on something that will still be
/// there.
///
/// It is also the Emulator's own arrangement, and where the word comes from: you loaded your
/// sounds onto the machine's disk, and after that the machine played from its disk.
/// </remarks>
public interface IRecordingImport
{
    /// <summary>
    /// What can be brought in.
    /// </summary>
    /// <remarks>
    /// WAV first, because that is what the shelf holds, then everything the decoder can turn
    /// into one. A machine still only ever plays a WAV: an instrument is read into memory by
    /// the sample store, which decodes WAV alone, and the shelf is what it reads from.
    ///
    /// So this is not a list of what a machine can play. It is a list of what can be made into
    /// something a machine can play, on the way in, once, before the file is on the shelf at
    /// all. What is offered follows what is really installed, so nothing is offered here that
    /// would then fail.
    /// </remarks>
    string[] Kinds { get; }

    /// <summary>Where JingleBox keeps its recordings.</summary>
    string Directory { get; }

    /// <summary>True when this is something worth offering to bring in.</summary>
    /// <param name="path">The file, wherever it is.</param>
    bool Playable(string path);

    /// <summary>
    /// True when a file will be rewritten on the way in rather than copied.
    /// </summary>
    /// <remarks>
    /// For a panel that wants to say what it did with a file. The answer is the same one the
    /// import acts on, asked without doing anything. Anything that is not a WAV is decoded and
    /// written out as one, whatever is inside it; a file that cannot be read as a WAV either is
    /// copied as it is, so nothing is converted.
    /// </remarks>
    /// <param name="path">The file, wherever it is.</param>
    bool Converts(string path);

    /// <summary>
    /// Brings files in, and answers with what is now on the shelf.
    /// </summary>
    /// <remarks>
    /// A file already on the shelf byte for byte is skipped rather than copied again, which is
    /// what makes opening a packed song twice add nothing. A file that will not read at all is
    /// passed over: one bad file in a folder somebody dragged in must not stop the other forty.
    /// </remarks>
    /// <param name="paths">The files, wherever they are.</param>
    IReadOnlyList<Recording> Take(IEnumerable<string> paths);

    /// <summary>What a file is, in the words a panel shows beside it.</summary>
    /// <param name="path">The file, wherever it is.</param>
    string Describe(string path);
}
