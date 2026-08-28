using System;
using System.Collections.Generic;
using System.Globalization;
using JingleBox2.Audio.Plugins;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
public sealed class RunMarker : IRunMarker
{
    /// <summary>The word the start time is written behind, and looked for behind.</summary>
    /// <remarks>
    /// Written out once and used by both halves, since a marker written under one word and read
    /// under another is a marker that always says it does not know when the run began.
    /// </remarks>
    private const string Began = "started ";

    /// <summary>The word the build is written behind.</summary>
    private const string Built = "version ";

    /// <summary>How a moment is written down, which is a shape both halves agree on.</summary>
    private const string Stamp = "yyyy-MM-dd HH:mm:ss";

    /// <inheritdoc/>
    public string Compose(DateTime started, string version) =>
        Began + started.ToString(Stamp, CultureInfo.InvariantCulture) + "\n" +
        Built + (string.IsNullOrWhiteSpace(version) ? "?" : version) + "\n";

    /// <inheritdoc/>
    public DateTime? StartedFrom(IEnumerable<string> lines)
    {
        if (lines is null) return null;

        foreach (string line in lines)
        {
            if (line is null || !line.StartsWith(Began, StringComparison.Ordinal)) continue;

            if (DateTime.TryParse(line[Began.Length..], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var when))
                return when;
        }

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PluginCrash> Since(IReadOnlyList<PluginCrash> blocked, DateTime since)
    {
        var held = new List<PluginCrash>();

        if (blocked is null) return held;

        foreach (var mark in blocked)
            if (mark.When >= since) held.Add(mark);

        return held;
    }
}
