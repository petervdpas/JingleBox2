using System;
using System.IO;

namespace JingleBox2.Files.Interfaces;

/// <summary>
/// Writing a file in a way that survives the application stopping in the middle of it.
/// </summary>
/// <remarks>
/// <c>File.WriteAllText</c> empties the file and then fills it, so anything that ends the
/// process in between leaves nothing behind: a settings file that is now empty, a song that is
/// half a song, an instrument that will not parse. The window for that is small and the cost of
/// landing in it is somebody's afternoon, and on a machine where a plugin can take the whole
/// application down at any moment it is not as small as it sounds.
///
/// So the new text goes to a file beside it and is moved on top of the old one when it is
/// complete. A move over an existing file is one operation as far as anybody watching is
/// concerned: what is there is either all of the old file or all of the new one, never half of
/// either. A crash before the move leaves the old file exactly as it was, and a stray temporary
/// file that the next write cleans up.
///
/// Both overloads fall back to copying the finished file over the old one where the move will
/// not go through, since a file written the risky way is worth more than a file not written at
/// all. That is the case on a folder somebody has mounted oddly.
///
/// The fallback is only ever reached with a whole file to land, and that distinction is the
/// point rather than a detail. A write that fails is not a move that fails: it used to be one
/// attempt covering both, so a writer that threw part way fell into the fallback, which opened
/// the real file, emptied it, ran the same writer again and threw again. The old file was gone,
/// the new one was never written, and the exception said nothing about either. A song is built
/// an entry at a time, so that was reachable by any take that would not read. Now a write that
/// fails leaves the old file untouched and says so by throwing.
/// </remarks>
public interface ISafeFile
{
    /// <summary>
    /// Writes a file through a stream, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// For a file that is not text. A song is a zip, built an entry at a time, and building it
    /// straight over the old one would mean a song half rewritten is a zip with no central
    /// directory: not a song, and not the old song either.
    /// </remarks>
    /// <param name="path">Where it should end up. Its folder is made if it is not there.</param>
    /// <param name="write">
    /// Fills the stream. Called once, and possibly a second time on the fallback path, so it
    /// must be able to write the same thing twice.
    /// </param>
    void Write(string path, Action<Stream> write);

    /// <summary>
    /// Writes text to a path, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// The move over the top of whatever is there is the one step that makes the new file the
    /// file, and it does that on both platforms this runs on.
    /// </remarks>
    /// <param name="path">Where it should end up. Its folder is made if it is not there.</param>
    /// <param name="text">The whole of the new contents.</param>
    void Write(string path, string text);
}
