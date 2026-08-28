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
    /// True when plugins are given a process of their own, which is Linux and macOS. Windows
    /// loads them into this one.
    /// </summary>
    /// <remarks>
    /// A plugin in its own process cannot take the application down, so everything the crash
    /// guard was written for stops applying: nothing needs blocking, because nothing that goes
    /// wrong in a plugin is fatal any more. See <see cref="PluginCrashGuard"/>, which stands
    /// down while this is true.
    ///
    /// Windows is the exception because of how a plugin's window is handed over there: the
    /// embedding used here only works within one process, so a VST3 plugin has to be loaded into
    /// this one for its own interface to answer a mouse at all.
    ///
    /// <c>JB_PLUGINS_INPROCESS=1</c> turns it off everywhere, which is how a plugin is debugged
    /// with the application's own debugger attached.
    /// </remarks>
    public static bool Isolated => !OperatingSystem.IsWindows() && !InProcessAsked;

    /// <summary>True when somebody has asked for plugins in this process, whatever the platform.</summary>
    private static bool InProcessAsked =>
        Environment.GetEnvironmentVariable(PluginBridge.InProcessVariable) == "1";

    /// <summary>Opens a plugin as an effect, whichever standard it speaks.</summary>
    /// <remarks>
    /// The block size is a promise: a plugin allocates against it and may not be handed a bigger
    /// one afterwards.
    /// </remarks>
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
    ///
    /// None of that applies on the normal path, where the plugin gets a process of its own:
    /// nothing is written down beforehand because nothing in this process is at risk.
    /// </remarks>
    private static object? Open(PluginInfo? plugin, int sampleRate, int maxFrames, bool asInstrument)
    {
        if (plugin == null) return null;

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            $"Opening {plugin.Name} ({plugin.FormatName}), Isolated={Isolated}, InstrumentMode={asInstrument}");

        if (Isolated)
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () => "Using isolated (out-of-process) loading");
            return BridgedPlugin.Load(plugin, sampleRate, maxFrames, asInstrument);
        }

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () => "Using in-process loading");

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
    /// <remarks>
    /// In a process of its own on every platform, Windows included, where plugins are otherwise
    /// loaded into this one. Scanning is the one thing that needs nothing from the window
    /// embedding that keeps Windows in-process: the child opens each bundle, asks what is in it,
    /// writes a list and goes away. And it is the worst place to be running somebody else's code
    /// unprotected, because a plugin that dies while being asked what it is would take the
    /// application down every time it started, before anybody had chosen to use it.
    /// </remarks>
    public static List<PluginInfo> Scan(IReadOnlyList<string> folders)
    {
        return InProcessAsked ? ScanHere(folders) : ScanElsewhere(folders);
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
    ///
    /// The child is given no window and nothing to show in one: it writes a file and goes away.
    /// Without that, a console flashes up on Windows every time somebody scans.
    ///
    /// The application may be running as <c>dotnet something.dll</c> rather than as its own
    /// executable, in which case the assembly has to be named as the first argument or the child
    /// would be a bare runtime with nothing to run.
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

            CreateNoWindow = true,
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
    /// <remarks>
    /// Each bundle's reference goes back as soon as it has been asked what it holds. The
    /// libraries themselves stay loaded for the life of the process, which is deliberate and is
    /// explained on <see cref="ClapBundle.Dispose"/>.
    ///
    /// Sorted by name and then by format, so a vendor who ships both a CLAP and a VST3 of the
    /// same plugin has them next to each other rather than at opposite ends of the list.
    /// </remarks>
    internal static List<PluginInfo> ScanHere(IReadOnlyList<string> folders)
    {
        var found = new List<PluginInfo>();

        foreach (var path in ClapScanner.Bundles(folders))
        {
            var bundle = ClapBundle.Acquire(path);
            if (bundle == null) continue;

            found.AddRange(bundle.Plugins());

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
