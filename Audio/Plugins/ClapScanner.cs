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
    public const string Extension = ".clap";

    /// <summary>
    /// Every directory this platform keeps plugins in, plus any the user has added, whether
    /// or not they exist.
    /// </summary>
    public static IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null)
    {
        var paths = new List<string>();

        // The user's own folders come first: someone who names a folder means it.
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

        // The format lets the environment say where else to look, and some distributions
        // rely on it.
        string? fromEnvironment = Environment.GetEnvironmentVariable("CLAP_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            foreach (var part in fromEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                paths.Add(part.Trim());
        }

        return paths;
    }

    /// <summary>Every .clap found on the search paths, sorted by name. Unreadable ones are skipped.</summary>
    public static IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in SearchPaths(extra))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) continue;

            try
            {
                // Two levels: vendors often keep their plugins in a folder of their own.
                foreach (var file in Directory.EnumerateFiles(directory, "*" + Extension, SearchOption.AllDirectories))
                {
                    if (seen.Add(file)) found.Add(file);
                }
            }
            catch (Exception)
            {
                // A directory that cannot be read is one place with no plugins in it, not a
                // reason for the app to have no plugins at all.
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    private static void Add(List<string> paths, string? root, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(root)) return;

        paths.Add(Path.Combine(root, Path.Combine(parts)));
    }
}
