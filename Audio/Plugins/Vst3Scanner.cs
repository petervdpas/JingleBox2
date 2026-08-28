using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Audio.Plugins;

/// <summary>Where VST3 plugins live, and what is actually there.</summary>
/// <remarks>
/// The search paths are the ones Steinberg's own specification names, so a plugin installed the
/// normal way for the platform is found without anybody being told where it is. A .vst3 is a
/// directory on every platform this runs on, which is what makes the walk below different from
/// the CLAP one: the thing being looked for is itself a folder.
/// </remarks>
public static class Vst3Scanner
{
    /// <summary>What a VST3 bundle is called. A directory rather than a file, on every platform.</summary>
    public const string Extension = ".vst3";

    /// <summary>How far down a folder is followed before giving up on finding bundles.</summary>
    private const int MaxDepth = 5;

    /// <summary>
    /// Every directory this platform keeps plugins in, plus any the user has added, whether
    /// or not they exist.
    /// </summary>
    /// <remarks>
    /// Offered whole rather than filtered, since a folder that does not exist today is a folder
    /// a plugin can be installed into tomorrow. <c>VST3_PATH</c> is read as well, the same way
    /// the CLAP scanner reads <c>CLAP_PATH</c>.
    /// </remarks>
    /// <param name="extra">
    /// Folders somebody has added in SETTINGS. They come first, because a person who names a
    /// folder means it.
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
            Add(paths, Environment.GetEnvironmentVariable("COMMONPROGRAMFILES"), "VST3");
            Add(paths, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Common", "VST3");
        }
        else if (OperatingSystem.IsMacOS())
        {
            Add(paths, home, "Library", "Audio", "Plug-Ins", "VST3");
            paths.Add("/Library/Audio/Plug-Ins/VST3");
        }
        else
        {
            Add(paths, home, ".vst3");
            paths.Add("/usr/lib/vst3");
            paths.Add("/usr/local/lib/vst3");
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable("VST3_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            foreach (var part in fromEnvironment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                paths.Add(part.Trim());
        }

        return paths;
    }

    /// <summary>Every .vst3 found on the search paths, sorted by name.</summary>
    /// <remarks>Unreadable folders are skipped rather than reported: see <see cref="Walk"/>.</remarks>
    public static IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in SearchPaths(extra))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) continue;

            Walk(directory, found, seen, 0);
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>
    /// Looks through a folder for bundles, and stops at each one it finds.
    /// </summary>
    /// <remarks>
    /// A bundle is a folder, and a bundle full of resources can hold thousands of files. There
    /// is nothing below one worth finding, so a match ends the descent rather than starting a
    /// new one. Vendors do keep their bundles in a folder of their own, so the walk carries on
    /// past anything that is not a bundle, down to a depth that covers that habit.
    ///
    /// A directory that cannot be read is one place with no plugins in it and is stepped over in
    /// silence, rather than being a reason for the application to have no plugins at all.
    /// </remarks>
    private static void Walk(string directory, List<string> found, HashSet<string> seen, int depth)
    {
        if (depth > MaxDepth) return;

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (entry.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                {
                    if (seen.Add(entry)) found.Add(entry);
                    continue;
                }

                if (Directory.Exists(entry)) Walk(entry, found, seen, depth + 1);
            }
        }
        catch (Exception)
        {
        }
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
