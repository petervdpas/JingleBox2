using System;
using System.IO;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <remarks>
/// The path rule is taken once, when one of these is made, and every test below is a prefix
/// comparison under it.
/// </remarks>
/// <param name="paths">
/// How this system decides two paths are the same. Left out, the rule this system really has,
/// which is what the application wants; given, whatever a test wants to hold it to.
/// </param>
public sealed class SoundMachinePaths(IFilePaths? paths = null) : ISoundMachinePaths
{
    /// <summary>How two paths are compared, which is a fact about the disc and not about here.</summary>
    private readonly IFilePaths _paths = paths ?? new FilePaths();

    /// <inheritdoc/>
    public bool Under(string path, string folder)
    {
        if (path.Length == 0 || folder.Length == 0) return false;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Root(folder);

            return full.StartsWith(root, _paths.Comparison) && full.Length > root.Length;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public string? Named(string path, string folder)
    {
        if (path.Length == 0 || folder.Length == 0) return null;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Root(folder);

            if (!full.StartsWith(root, _paths.Comparison) || full.Length <= root.Length) return null;

            return full[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string Outside(string named, string folder)
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
