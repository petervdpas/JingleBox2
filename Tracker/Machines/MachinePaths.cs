using System;
using System.IO;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The two questions asked of every path a machine holds: is it inside the machine, and what is
/// it called in there.
/// </summary>
/// <remarks>
/// A machine travels as a folder, so a recording it carries has to be written down relative to
/// that folder and with forward slashes, or the machine only works on the computer it was built
/// on. Reading one back is the same act in reverse, and both were written out separately in four
/// places, which is four chances for one of them to compare paths the wrong way.
///
/// The wrong way is what this exists to settle. Windows treats two paths that differ only in
/// case as the same path and Linux does not, so a test done with an exact comparison is a test
/// that says no on Windows for a file that is plainly there. What comes of that is not a crash:
/// a machine quietly refuses to be removed, or writes an absolute path into a zip that will not
/// open anywhere else.
/// </remarks>
public static class MachinePaths
{
    /// <summary>True when that path is somewhere inside that folder.</summary>
    /// <remarks>
    /// The folder itself does not count as being inside itself, which is what the length test is
    /// for: a machine cannot be installed over the folder it is being copied from, and a file
    /// that is the folder is not a file in it.
    /// </remarks>
    public static bool Under(string path, string folder)
    {
        if (path.Length == 0 || folder.Length == 0) return false;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Root(folder);

            return full.StartsWith(root, FilePaths.Comparison) && full.Length > root.Length;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// What that path is called inside that folder, or nothing when it is not in there.
    /// </summary>
    /// <remarks>
    /// Forward slashes whatever this system uses, because the answer is going into a file that
    /// another system will read. <see cref="Outside"/> puts it back.
    /// </remarks>
    public static string? Named(string path, string folder)
    {
        if (path.Length == 0 || folder.Length == 0) return null;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Root(folder);

            if (!full.StartsWith(root, FilePaths.Comparison) || full.Length <= root.Length) return null;

            return full[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Where a name written down inside a machine really is on this disc.</summary>
    public static string Outside(string named, string folder)
    {
        if (named.Length == 0 || folder.Length == 0 || Path.IsPathRooted(named)) return named;

        return Path.GetFullPath(Path.Combine(folder, named.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>The folder, ended with a separator so a prefix test cannot match a sibling.</summary>
    private static string Root(string folder)
    {
        string root = Path.GetFullPath(folder);

        return root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
    }
}
