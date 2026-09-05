using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// What this installation found when it last scanned, for whoever is about to load one.
/// </summary>
/// <remarks>
/// Static, and one of the few here that is, for the reason the other doors are: an installation
/// has one set of plugins and handing the list about would be handing the same list about under
/// another name. The half that decides anything is <see cref="Interfaces.IPluginsHere"/>, which
/// takes the list as an argument and can be asked without a settings file, a scan or a plugin.
///
/// Told rather than read, so nothing on the loading path opens the settings: the list is put here
/// when the settings are read at startup and again whenever a scan finishes.
///
/// Empty until somebody says otherwise, and empty means a song is loaded exactly as it was before
/// this existed, which is by the path it wrote down.
/// </remarks>
public static class PluginShelf
{
    /// <summary>What was found here, or nothing when nobody has scanned.</summary>
    public static IReadOnlyList<PluginInfo> Known { get; private set; } = new List<PluginInfo>();

    /// <summary>Says what this installation has, for everything after this.</summary>
    /// <param name="known">The scan's own list.</param>
    public static void Wants(IReadOnlyList<PluginInfo>? known) =>
        Known = known ?? new List<PluginInfo>();
}
