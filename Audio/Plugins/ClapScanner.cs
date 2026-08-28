using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio.Plugins;

/// <inheritdoc/>
public sealed class ClapScanner : IClapScanner
{
    /// <summary>What a CLAP bundle is called: a shared library on Linux and Windows, a bundle
    /// directory on macOS, and the same four letters either way.</summary>
    /// <inheritdoc cref="IClapScanner.Extension"/>
    public const string Extension = ".clap";

    /// <inheritdoc/>
    string IClapScanner.Extension => Extension;

    /// <inheritdoc/>
    public IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null)
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

    /// <inheritdoc/>
    public IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null)
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
    private void Add(List<string> paths, string? root, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(root)) return;

        paths.Add(Path.Combine(root, Path.Combine(parts)));
    }
}
