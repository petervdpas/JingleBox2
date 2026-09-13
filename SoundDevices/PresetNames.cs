using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices;

/// <inheritdoc/>
public sealed class PresetNames : IPresetNames
{
    /// <summary>What a preset file is called on disc, in both worlds.</summary>
    public const string Extension = ".json";

    /// <summary>The characters no preset name may hold, since the name is the file's name on every system.</summary>
    private static readonly char[] Unfiled = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

    /// <inheritdoc/>
    public string Refusal(string name, string device, IEnumerable<(string Name, string File)> theirs)
    {
        string called = (name ?? "").Trim();

        if (called.Length == 0) return "A preset needs a name.";

        if (called.IndexOfAny(Unfiled) >= 0 || called.Any(char.IsControl) || called.StartsWith('.'))
            return "A preset name cannot hold / \\ : * ? \" < > | or start with a dot.";

        foreach (var (shown, file) in theirs ?? Enumerable.Empty<(string, string)>())
            if (string.Equals(shown, called, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileNameWithoutExtension(file), called, StringComparison.OrdinalIgnoreCase))
                return "'" + shown + "' is one of " + device + "'s own presets. Give yours another name.";

        return "";
    }

    /// <inheritdoc/>
    public string FileFor(string name) => (name ?? "").Trim() + Extension;
}
