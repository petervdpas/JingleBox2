using JingleBox2.Audio.Plugins.Bridge;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>
    /// True when plugins are given a process of their own. On Linux/Mac only; Windows requires
    /// in-process loading for proper window embedding.
    /// </summary>
    /// <remarks>
    /// A plugin in its own process cannot take the application down, so everything the crash
    /// guard was written for stops applying: nothing needs blocking, because nothing that goes
    /// wrong in a plugin is fatal any more. See <see cref="PluginCrashGuard"/>, which stands
    /// down while this is true.
    ///
    /// On Windows, the plugin window embedding architecture doesn't support out-of-process plugins.
    /// VST3 plugins need to run in-process for UI interaction to work properly.
    /// </remarks>
    public static bool Isolated =>
        !OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable(PluginBridge.InProcessVariable) != "1";

    /// <summary>Opens a plugin, whichever standard it speaks.</summary>
    public static IPluginEffect? Load(PluginInfo plugin, int sampleRate, int maxFrames)
    {
        return Open(plugin, sampleRate, maxFrames, false) as IPluginEffect;
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
    private static object? Open(PluginInfo? plugin, int sampleRate, int maxFrames, bool asInstrument)
    {
        if (plugin == null) return null;

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            $"Opening {plugin.Name} ({plugin.FormatName}), Isolated={Isolated}, InstrumentMode={asInstrument}");

        // The normal way: the plugin gets a process of its own and nothing it does can reach
        // this one. Nothing is written down beforehand because nothing here is at risk.
        if (Isolated)
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () => "Using isolated (out-of-process) loading");
            return BridgedPlugin.Load(plugin, sampleRate, maxFrames, asInstrument);
        }

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () => "Using in-process loading");

        // A plugin that killed the last run while loading does not get to load this one.
        if (PluginCrashGuard.IsLoadBlocked(plugin))
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () => $"Plugin blocked by crash guard");
            return null;
        }

        PluginCrashGuard.Risky(plugin, PluginStage.Load);

        try
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
                $"Loading {plugin.Format} plugin at {plugin.Path}");

            object? result;
            if (plugin.Format == PluginFormat.Vst3)
            {
                result = Vst3Plugin.Load(plugin.Path, plugin.Id, sampleRate, maxFrames);
            }
            else
            {
                result = ClapEffect.Load(plugin.Path, plugin.Id, sampleRate, maxFrames);
            }

            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
                result != null ? $"Successfully loaded {plugin.Name}" : $"Failed to load {plugin.Name}");

            return result;
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

        return Open(plugin, sampleRate, maxFrames, true) as IPluginInstrument;
    }

    /// <summary>True when this host can play notes into a plugin of this kind.</summary>
    public static bool CanPlay(PluginInfo plugin) =>
        plugin != null && plugin.IsInstrument && plugin.Format == PluginFormat.Vst3 &&
        (Isolated || !PluginCrashGuard.IsLoadBlocked(plugin));

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
        return Isolated ? ScanElsewhere(folders) : ScanHere(folders);
    }

    /// <summary>
    /// Reads the folders in a process of its own, so a plugin that falls over while being asked
    /// what it is costs one empty list rather than the application.
    /// </summary>
    /// <remarks>
    /// Scanning is the one place a plugin gets to run code before anybody has chosen to use it,
    /// which makes a bad one here worse than a bad one anywhere else: it would go off every time
    /// the program started. Out of process it is somebody else's problem, and if the answer does
    /// not arrive the scan comes back empty and the plugins already known about stay known.
    /// </remarks>
    private static List<PluginInfo> ScanElsewhere(IReadOnlyList<string> folders)
    {
        string? self = Environment.ProcessPath;
        if (string.IsNullOrEmpty(self)) return ScanHere(folders);

        string answer = Path.Combine(Path.GetTempPath(), "jinglebox-scan-" + Guid.NewGuid().ToString("N") + ".json");

        var start = new ProcessStartInfo
        {
            FileName = self,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(self), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string assembly = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
            if (string.IsNullOrEmpty(assembly)) return ScanHere(folders);

            start.ArgumentList.Add(assembly);
        }

        start.ArgumentList.Add(PluginBridge.ScanArgument);
        start.ArgumentList.Add(answer);

        foreach (var folder in folders) start.ArgumentList.Add(folder);

        try
        {
            using var child = Process.Start(start);
            if (child == null) return new List<PluginInfo>();

            if (!child.WaitForExit(ScanSeconds * 1000))
            {
                child.Kill(entireProcessTree: true);
                return new List<PluginInfo>();
            }

            if (!File.Exists(answer)) return new List<PluginInfo>();

            var found = System.Text.Json.JsonSerializer.Deserialize<List<PluginInfo>>(File.ReadAllText(answer));

            return found ?? new List<PluginInfo>();
        }
        catch (Exception)
        {
            return new List<PluginInfo>();
        }
        finally
        {
            try { if (File.Exists(answer)) File.Delete(answer); } catch (Exception) { }
        }
    }

    /// <summary>How long a whole scan is given before it is assumed to have hung.</summary>
    private const int ScanSeconds = 120;

    /// <summary>The scan itself, run wherever it is called: in the child, or in this process
    /// when isolation has been turned off.</summary>
    internal static List<PluginInfo> ScanHere(IReadOnlyList<string> folders)
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
