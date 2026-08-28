using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Audio.Plugins;

/// <summary>Where CLAP plugins live, and what is actually there.</summary>
/// <remarks>
/// The search paths are the ones the format specifies, so a plugin installed the normal way
/// for the platform is found without being told where it is. A .clap file is a shared library
/// on Linux and Windows, and a bundle directory on macOS.
/// </remarks>
public static class ClapScanner
{
    /// <summary>What a CLAP bundle is called: a shared library on Linux and Windows, a bundle
    /// directory on macOS, and the same four letters either way.</summary>
    public const string Extension = ".clap";

    /// <summary>
    /// Every directory this platform keeps plugins in, plus any the user has added, whether
    /// or not they exist.
    /// </summary>
    /// <remarks>
    /// The list is offered whole rather than filtered, since a folder that does not exist today
    /// is a folder a plugin can be installed into tomorrow. <c>CLAP_PATH</c> is read as well: the
    /// format says the environment may name more places to look, and some distributions rely on
    /// nothing else.
    /// </remarks>
    /// <param name="extra">
    /// Folders somebody has added in SETTINGS. They come first, because a person who names a
    /// folder means it, and a plugin found in two places should be the one they pointed at.
    /// </param>
    public static IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null)
    {
        var paths = new List<string>();

        if (extra != null)
        {
            foreach (var folder in extra)
            {
                if (!string.IsNullOrWhiteSpace(folder)) paths.Add(folder.Trim());
            }
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            Add(paths, Environment.GetEnvironmentVariable("COMMONPROGRAMFILES"), "CLAP");
            Add(paths, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Common", "CLAP");
        }
        else if (OperatingSystem.IsMacOS())
        {
            Add(paths, home, "Library", "Audio", "Plug-Ins", "CLAP");
            paths.Add("/Library/Audio/Plug-Ins/CLAP");
        }
        else
        {
            Add(paths, home, ".clap");
            paths.Add("/usr/lib/clap");
            paths.Add("/usr/local/lib/clap");
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable("CLAP_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            foreach (var part in fromEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                paths.Add(part.Trim());
        }

        return paths;
    }

    /// <summary>Every .clap found on the search paths, sorted by name. Unreadable ones are skipped.</summary>
    /// <remarks>
    /// Followed all the way down, because vendors habitually keep their plugins in a folder of
    /// their own. A directory that cannot be read is one place with no plugins in it rather than
    /// a reason for the application to have no plugins at all, so it is stepped over in silence.
    /// </remarks>
    public static IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in SearchPaths(extra))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension, SearchOption.AllDirectories))
                {
                    if (seen.Add(file)) found.Add(file);
                }
            }
            catch (Exception)
            {
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>
    /// Joins a root that may be missing to the rest of a path. A root the platform does not
    /// define adds nothing rather than adding a relative path nobody meant.
    /// </summary>
    private static void Add(List<string> paths, string? root, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(root)) return;

        paths.Add(Path.Combine(root, Path.Combine(parts)));
    }
}
