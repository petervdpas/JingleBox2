using System;
using System.IO;
using System.Text;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
public sealed class LogFile : ILogFile
{
    /// <summary>
    /// How the file is encoded: UTF-8, and deliberately without the byte order mark.
    /// </summary>
    /// <remarks>
    /// This is a file people open in a text editor and paste out of, not one anything parses,
    /// and a mark at the front of it is three bytes of rubbish in whatever they paste.
    /// </remarks>
    private static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc/>
    public bool Append(string path, string text)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(text)) return false;

        try
        {
            string folder = Path.GetDirectoryName(path) ?? "";

            if (folder.Length > 0) Directory.CreateDirectory(folder);

            File.AppendAllText(path, text, Text);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Roll(string path, long mostBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || mostBytes <= 0) return false;

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < mostBytes) return false;

            string old = path + ".old";

            if (File.Exists(old)) File.Delete(old);

            File.Move(path, old);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
