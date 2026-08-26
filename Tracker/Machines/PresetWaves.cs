using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// What a preset file plays, and who else plays it.
/// </summary>
/// <remarks>
/// Read out of the JSON as text, without knowing what machine it is for. A preset holds its
/// recordings under whatever key the machine calls them, so looking for the key would mean
/// knowing every machine; looking for a name ending in wav means knowing none of them, and a
/// machine somebody else writes tomorrow is covered by that on the day it arrives.
///
/// One place, because three things ask it and they have to agree: renaming a preset moves the
/// folder, deleting one takes the folder with it, and levelling one rewrites what is in it. Two
/// answers to "what does this preset play" would be one preset renamed and one folder left.
/// </remarks>
public static class PresetWaves
{
    private const string Kind = ".wav";

    /// <summary>Every recording a preset names, as written down, each one once.</summary>
    public static IReadOnlyList<string> Named(string presetPath)
    {
        var found = new List<string>();

        try
        {
            Walk(JsonNode.Parse(File.ReadAllText(presetPath)), found);
        }
        catch (Exception)
        {
            // A preset that will not read names nothing, which is the safe answer: nothing gets
            // renamed, nothing gets deleted, nothing gets rewritten.
            return Array.Empty<string>();
        }

        return found.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>True when that name is one of a preset's recordings.</summary>
    public static bool IsWave(string? said) =>
        said is { Length: > 4 } && said.EndsWith(Kind, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The folder inside the machine that a preset plays out of, or nothing when it has none.
    /// </summary>
    /// <remarks>
    /// The first one it names, since a preset written by this program keeps all of its
    /// recordings together. One that names two folders has whichever it named first, and
    /// <see cref="Users"/> is what stops that being acted on.
    /// </remarks>
    public static string? Folder(string presetPath, string home)
    {
        foreach (string named in Named(presetPath))
        {
            string full = MachinePaths.Outside(named, home);

            if (Path.GetDirectoryName(full) is not { Length: > 0 } folder) continue;

            if (MachinePaths.Under(folder, home)) return folder;
        }

        return null;
    }

    /// <summary>
    /// Which of those presets play out of that folder.
    /// </summary>
    /// <remarks>
    /// The question asked before a folder is renamed or removed. Two presets can share one, and
    /// a folder moved out from under the second is a kit that opens with empty pads.
    /// </remarks>
    public static IReadOnlyList<string> Users(string folder, string home, IEnumerable<string> presets)
    {
        var found = new List<string>();

        if (folder.Length == 0) return found;

        foreach (string preset in presets)
        {
            foreach (string named in Named(preset))
            {
                string full = MachinePaths.Outside(named, home);

                if (!FilePaths.SameFile(Path.GetDirectoryName(full), folder)) continue;

                found.Add(preset);

                break;
            }
        }

        return found;
    }

    private static void Walk(JsonNode? node, List<string> found)
    {
        switch (node)
        {
            case JsonObject held:
                foreach (var (_, value) in held) Walk(value, found);
                break;

            case JsonArray list:
                foreach (var value in list) Walk(value, found);
                break;

            case JsonValue value when value.TryGetValue(out string? said) && IsWave(said):
                found.Add(said!);
                break;
        }
    }
}
