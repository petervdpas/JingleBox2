using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
public sealed class LogAreas : ILogAreas
{
    /// <summary>The word each area is written under, in the file and in the environment variable.</summary>
    private static readonly Dictionary<LogArea, string> Named = new()
    {
        [LogArea.App] = "app",
        [LogArea.Audio] = "audio",
        [LogArea.Plugins] = "plugin",
        [LogArea.Tracker] = "tracker",
        [LogArea.Midi] = "midi",
        [LogArea.Machines] = "machines"
    };

    /// <inheritdoc/>
    public IReadOnlyDictionary<LogArea, string> Everywhere => Named;

    /// <inheritdoc/>
    public string Short(LogArea area) => Named.TryGetValue(area, out var name) ? name : "log";

    /// <inheritdoc/>
    public LogArea Asked(string? said)
    {
        if (string.IsNullOrWhiteSpace(said)) return LogArea.None;

        said = said.Trim();

        if (said == "1" || said.Equals("all", StringComparison.OrdinalIgnoreCase)) return LogArea.Everything;
        if (said == "0") return LogArea.None;

        var wanted = LogArea.None;

        foreach (string part in said.Split(',', ' ', ';'))
        {
            string name = part.Trim();
            if (name.Length == 0) continue;

            foreach (var (area, called) in Named)
                if (name.Equals(called, StringComparison.OrdinalIgnoreCase)) wanted |= area;
        }

        return wanted;
    }

    /// <inheritdoc/>
    public LogArea Wanted(bool on, LogArea areas, string? said)
    {
        var asked = Asked(said);

        if (asked != LogArea.None) return asked;

        return on ? areas : LogArea.None;
    }
}
