using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Files;

/// <inheritdoc/>
public sealed class SafeFile : ISafeFile
{
    /// <summary>What the half-written file is called while it is being written.</summary>
    /// <remarks>
    /// Beside the real file rather than in the system's temporary folder, because a move only
    /// counts as one operation within a single volume: across two it is a copy and a delete,
    /// which is the very thing being avoided.
    /// </remarks>
    private const string Suffix = ".writing";

    /// <summary>Tells one write from another, so no two ever share a half-written file.</summary>
    /// <remarks>
    /// The name used to be the path and nothing else, which is one name for every writer. Two
    /// threads at one path is not a corner: the settings are written from the drawing thread
    /// whenever anything on a page moves and from the MIDI thread when a knob is learned or a
    /// control's own behaviour is worked out. Sharing the name, the second writer could not
    /// create the file, deleted it on its way out, and left the first with nothing to move into
    /// place; the fallback then opened the real file and could leave that broken too. From
    /// outside it is a settings file that occasionally loses whatever was last put in it.
    ///
    /// The process is in it as well as the count, since this same executable runs again as a
    /// plugin's host and two processes counting from nought would agree.
    /// </remarks>
    private static int _writes;

    /// <summary>
    /// UTF-8 without the byte order mark.
    /// </summary>
    /// <remarks>
    /// The mark is legal in front of JSON and several readers choke on it, so it is left off:
    /// these files are read by this application and by whoever opens one in an editor.
    /// </remarks>
    private static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc/>
    public void Write(string path, Action<Stream> write)
    {
        if (string.IsNullOrWhiteSpace(path) || write == null) return;

        string writing = Prepare(path);

        try
        {
            using var stream = File.Create(writing);
            write(stream);
        }
        catch (Exception)
        {
            Discard(writing);
            throw;
        }

        Land(writing, path);
    }

    /// <inheritdoc/>
    public void Write(string path, string text)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string writing = Prepare(path);

        try
        {
            File.WriteAllText(writing, text, Text);
        }
        catch (Exception)
        {
            Discard(writing);
            throw;
        }

        Land(writing, path);
    }

    /// <summary>Makes the folder around the file and says what the half-written one is called.</summary>
    private static string Prepare(string path)
    {
        string folder = Path.GetDirectoryName(path) ?? "";

        if (folder.Length > 0) Directory.CreateDirectory(folder);

        return path + Suffix
               + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
               + "-"
               + Interlocked.Increment(ref _writes).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Takes the half-written file away, in silence.</summary>
    /// <remarks>
    /// Whatever went wrong is already on its way up, and a second failure while tidying up
    /// after the first would replace the reason with something less useful.
    /// </remarks>
    private static void Discard(string writing)
    {
        try { if (File.Exists(writing)) File.Delete(writing); } catch (Exception) { }
    }

    /// <summary>
    /// Makes the finished file the file, by moving it on top of the old one.
    /// </summary>
    /// <remarks>
    /// A copy where the move will not go through, which happens on a folder somebody has
    /// mounted oddly. A copy is not one operation and so is not safe in the way the move is,
    /// but the content is already complete and sitting on the same disc by this point, so what
    /// is being risked is a copy failing part way rather than a writer failing part way.
    ///
    /// This is only ever reached with a whole file to land, and that is the fix rather than an
    /// aside. It used to be one try around the writing and the moving together, so a writer
    /// that threw part way fell into the fallback, which opened the real file, emptied it, ran
    /// the same writer again and threw again: the old file was gone and the new one was never
    /// written. A song built an entry at a time is exactly the case, and the loss was silent
    /// apart from the exception, which said nothing about the file having been emptied. The
    /// writing is now its own attempt: if it fails, the old file has not been touched at all.
    /// </remarks>
    private static void Land(string writing, string path)
    {
        try
        {
            File.Move(writing, path, overwrite: true);
        }
        catch (Exception)
        {
            try { File.Copy(writing, path, overwrite: true); }
            finally { Discard(writing); }
        }
    }
}
