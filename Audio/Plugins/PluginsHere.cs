using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins;

/// <inheritdoc/>
public sealed class PluginsHere : IPluginsHere
{
    /// <inheritdoc/>
    /// <remarks>
    /// Walked rather than kept in a dictionary: this is asked when a plugin is loaded, which is
    /// seconds of somebody else's start-up code away, and a song has a handful of plugins rather
    /// than thousands.
    /// </remarks>
    public PluginInfo Same(PluginInfo asked, IReadOnlyList<PluginInfo>? known, bool byPath = true)
    {
        if (asked == null) return asked!;
        if (known == null || known.Count == 0) return asked;

        foreach (var here in known)
            if (here.Format == asked.Format && Named(here.Id, asked.Id))
                return here;

        foreach (var here in known)
            if (here.Format == asked.Format && Named(here.Name, asked.Name))
                return here;

        if (!byPath) return asked;

        PluginInfo? only = null;

        foreach (var here in known)
        {
            if (here.Format != asked.Format || !Named(here.Path, asked.Path)) continue;

            if (only != null) return asked;

            only = here;
        }

        return only ?? asked;
    }

    /// <summary>Whether two names are the same one, neither of them being nothing.</summary>
    /// <remarks>
    /// The emptiness test is the whole of it. A song written before ids were kept has none, and
    /// two of those matching each other would hand back whichever plugin happened to be first in
    /// the list, which is a different plugin playing the part with nothing anywhere saying so.
    /// </remarks>
    /// <param name="here">What this installation calls it.</param>
    /// <param name="asked">What the song wrote down.</param>
    private static bool Named(string here, string asked) =>
        !string.IsNullOrWhiteSpace(here)
        && !string.IsNullOrWhiteSpace(asked)
        && string.Equals(here, asked, StringComparison.OrdinalIgnoreCase);
}
