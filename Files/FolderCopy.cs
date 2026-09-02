using System.IO;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Files;

/// <inheritdoc/>
public sealed class FolderCopy : IFolderCopy
{
    /// <inheritdoc/>
    public void Into(string from, string into)
    {
        Directory.CreateDirectory(into);

        foreach (string folder in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(into, Path.GetRelativePath(from, folder)));

        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string to = Path.Combine(into, Path.GetRelativePath(from, file));

            if (Path.GetDirectoryName(to) is { Length: > 0 } folder) Directory.CreateDirectory(folder);

            File.Copy(file, to, overwrite: true);
        }
    }
}
