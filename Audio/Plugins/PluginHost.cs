using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The one place that knows both plugin standards, so nothing above here has to.
/// </summary>
/// <remarks>
/// CLAP and VST3 are different enough underneath to be worth keeping apart, and similar enough
/// above to be worth hiding. A picker, a chain and a saved song all deal in
/// <see cref="PluginInfo"/> and <see cref="IPluginEffect"/>; only this class chooses which
/// loader to call.
/// </remarks>
public static class PluginHost
{
    /// <summary>Opens a plugin, whichever standard it speaks.</summary>
    public static IPluginEffect? Load(string path, string id, PluginFormat format, int sampleRate, int maxFrames)
    {
        return format switch
        {
            PluginFormat.Vst3 => Vst3Effect.Load(path, id, sampleRate, maxFrames),
            _ => ClapEffect.Load(path, id, sampleRate, maxFrames)
        };
    }

    public static IPluginEffect? Load(PluginInfo plugin, int sampleRate, int maxFrames)
    {
        return plugin == null ? null : Load(plugin.Path, plugin.Id, plugin.Format, sampleRate, maxFrames);
    }

    /// <summary>Every directory either standard keeps plugins in, plus the user's own.</summary>
    public static IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ClapScanner.SearchPaths(extra))
        {
            if (seen.Add(path)) paths.Add(path);
        }

        foreach (var path in Vst3Scanner.SearchPaths(extra))
        {
            if (seen.Add(path)) paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// True when a plugin is still where it was found. A CLAP plugin is a file and a VST3
    /// plugin is usually a folder, so both have to be asked about.
    /// </summary>
    public static bool Exists(PluginInfo plugin)
    {
        if (plugin == null || string.IsNullOrWhiteSpace(plugin.Path)) return false;

        return File.Exists(plugin.Path) || Directory.Exists(plugin.Path);
    }

    /// <summary>
    /// Looks in every standard place, opens what it finds, and asks each bundle what is in it.
    /// A bundle that will not open is skipped rather than stopping the scan: one bad plugin is
    /// not a machine with no plugins.
    /// </summary>
    public static List<PluginInfo> Scan(IReadOnlyList<string> folders)
    {
        var found = new List<PluginInfo>();

        foreach (var path in ClapScanner.Bundles(folders))
        {
            var bundle = ClapBundle.Acquire(path);
            if (bundle == null) continue;

            found.AddRange(bundle.Plugins());

            // The reference goes back straight away. The library itself stays loaded, which is
            // deliberate and explained where that is decided.
            bundle.Dispose();
        }

        foreach (var path in Vst3Scanner.Bundles(folders))
        {
            var module = Vst3Module.Acquire(path);
            if (module == null) continue;

            found.AddRange(module.Plugins());

            module.Dispose();
        }

        found.Sort((first, second) =>
        {
            int byName = string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : first.Format.CompareTo(second.Format);
        });

        return found;
    }
}
