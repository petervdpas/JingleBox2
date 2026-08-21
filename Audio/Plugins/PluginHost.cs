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
    public static IPluginEffect? Load(PluginInfo plugin, int sampleRate, int maxFrames)
    {
        return Open(plugin, sampleRate, maxFrames) as IPluginEffect;
    }

    /// <summary>
    /// Opens a plugin with the crash guard around it.
    /// </summary>
    /// <remarks>
    /// Loading is where a plugin gets to run its own start-up code, which is exactly the sort
    /// of thing that goes wrong on somebody else's machine. Written down before and rubbed out
    /// after, so that a plugin which kills the application on the way in is not tried again on
    /// the way back up. See <see cref="PluginCrashGuard"/>.
    /// </remarks>
    private static object? Open(PluginInfo? plugin, int sampleRate, int maxFrames)
    {
        if (plugin == null) return null;

        // A plugin that killed the last run while loading does not get to load this one.
        if (PluginCrashGuard.IsLoadBlocked(plugin)) return null;

        PluginCrashGuard.Risky(plugin, PluginStage.Load);

        try
        {
            return plugin.Format == PluginFormat.Vst3
                ? Vst3Plugin.Load(plugin.Path, plugin.Id, sampleRate, maxFrames)
                : ClapEffect.Load(plugin.Path, plugin.Id, sampleRate, maxFrames);
        }
        finally
        {
            PluginCrashGuard.Survived(plugin);
        }
    }

    /// <summary>
    /// Opens a plugin as an instrument: something that takes notes and gives audio back.
    /// </summary>
    /// <remarks>
    /// Only VST3 for now. CLAP carries notes just as well and the plumbing here is the same
    /// shape, but nothing has been written for it yet, so a CLAP instrument is refused rather
    /// than loaded and then found to be silent. <see cref="CanPlay"/> is what a picker should
    /// ask so nobody is offered one.
    /// </remarks>
    public static IPluginInstrument? LoadInstrument(PluginInfo plugin, int sampleRate, int maxFrames)
    {
        if (plugin == null || plugin.Format != PluginFormat.Vst3) return null;

        return Open(plugin, sampleRate, maxFrames) as IPluginInstrument;
    }

    /// <summary>True when this host can play notes into a plugin of this kind.</summary>
    public static bool CanPlay(PluginInfo plugin) =>
        plugin != null && plugin.IsInstrument && plugin.Format == PluginFormat.Vst3 &&
        !PluginCrashGuard.IsLoadBlocked(plugin);

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
