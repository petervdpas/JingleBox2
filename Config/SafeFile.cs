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
/// </remarks>
public static class SafeFile
{
    /// <summary>What the half-written file is called while it is being written.</summary>
    private const string Suffix = ".writing";

    private static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes text to a path, all of it or none of it.
    /// </summary>
    /// <remarks>
    /// Falls back to writing straight to the path if the move will not go through, since a file
    /// written the risky way is worth more than a file not written at all.
    /// </remarks>
    public static void Write(string path, string text)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string folder = Path.GetDirectoryName(path) ?? "";

        if (folder.Length > 0) Directory.CreateDirectory(folder);

        string writing = path + Suffix;

        try
        {
            File.WriteAllText(writing, text, Text);

            // Over the top of whatever is there. On both platforms this is the one step that
            // makes the new file the file.
            File.Move(writing, path, overwrite: true);
        }
        catch (Exception)
        {
            try { if (File.Exists(writing)) File.Delete(writing); } catch (Exception) { }

            File.WriteAllText(path, text, Text);
        }
    }
}
