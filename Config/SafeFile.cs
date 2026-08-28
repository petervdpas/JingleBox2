using System;
using System.IO;
using System.Text;

namespace JingleBox2.Config;

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
/// Both overloads fall back to writing straight to the path where the move will not go through,
/// since a file written the risky way is worth more than a file not written at all. That is the
/// case on a folder somebody has mounted oddly, and it is the only way this can end up doing
/// what it exists to avoid.
/// </remarks>
public static class SafeFile
{
    /// <summary>What the half-written file is called while it is being written.</summary>
    /// <remarks>
    /// Beside the real file rather than in the system's temporary folder, because a move only
    /// counts as one operation within a single volume: across two it is a copy and a delete,
    /// which is the very thing being avoided.
    /// </remarks>
    private const string Suffix = ".writing";

    /// <summary>
    /// UTF-8 without the byte order mark.
    /// </summary>
    /// <remarks>
    /// The mark is legal in front of JSON and several readers choke on it, so it is left off:
    /// these files are read by this application and by whoever opens one in an editor.
    /// </remarks>
    private static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes a file through a stream, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// For a file that is not text. A song is a zip, built an entry at a time, and building it
    /// straight over the old one would mean a song half rewritten is a zip with no central
    /// directory: not a song, and not the old song either.
    /// </remarks>
    /// <param name="path">Where it should end up. Its folder is made if it is not there.</param>
    /// <param name="write">Fills the stream. Called once, and possibly a second time on the
    /// fallback path, so it must be able to write the same thing twice.</param>
    public static void Write(string path, Action<Stream> write)
    {
        if (string.IsNullOrWhiteSpace(path) || write == null) return;

        string folder = Path.GetDirectoryName(path) ?? "";

        if (folder.Length > 0) Directory.CreateDirectory(folder);

        string writing = path + Suffix;

        try
        {
            using (var stream = File.Create(writing)) write(stream);

            File.Move(writing, path, overwrite: true);
        }
        catch (Exception)
        {
            try { if (File.Exists(writing)) File.Delete(writing); } catch (Exception) { }

            using var stream = File.Create(path);
            write(stream);
        }
    }

    /// <summary>
    /// Writes text to a path, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// The move over the top of whatever is there is the one step that makes the new file the
    /// file, and it does that on both platforms this runs on.
    /// </remarks>
    /// <param name="path">Where it should end up. Its folder is made if it is not there.</param>
    /// <param name="text">The whole of the new contents.</param>
    public static void Write(string path, string text)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string folder = Path.GetDirectoryName(path) ?? "";

        if (folder.Length > 0) Directory.CreateDirectory(folder);

        string writing = path + Suffix;

        try
        {
            File.WriteAllText(writing, text, Text);

            File.Move(writing, path, overwrite: true);
        }
        catch (Exception)
        {
            try { if (File.Exists(writing)) File.Delete(writing); } catch (Exception) { }

            File.WriteAllText(path, text, Text);
        }
    }
}
