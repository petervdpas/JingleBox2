using System;
using System.IO;
using System.Text;
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

        return path + Suffix;
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
