using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <param name="paths">How a path is tested for being inside a folder. Left out, the ordinary one.</param>
public sealed class SoundMachinePack(ISoundMachinePaths? paths = null) : ISoundMachinePack
{
    /// <summary>How a rewritten preset is laid out, the way a preset is written anywhere else.</summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>How a path is tested for being inside a folder.</summary>
    private readonly ISoundMachinePaths _paths = paths ?? new SoundMachinePaths();

    /// <inheritdoc/>
    public void Write(string folder, string zipPath)
    {
        string root = Path.GetFullPath(folder);
        string presets = Path.Combine(root, SoundMachineProject.PresetsFolder);

        var carried = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            used.Add(Slashed(Path.GetRelativePath(root, file)));

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            string entry = Slashed(Path.GetRelativePath(root, file));

            if (IsPreset(file, presets) && Carried(file, root, carried, used, Path.GetFileNameWithoutExtension(file)) is { } rewritten)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry, CompressionLevel.Optimal).Open());

                writer.Write(rewritten);

                continue;
            }

            zip.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
        }

        foreach (var (outside, inside) in carried)
            zip.CreateEntryFromFile(outside, SoundMachineProject.PresetsFolder + "/" + inside, CompressionLevel.Optimal);
    }

    /// <summary>Whether that file is a preset lying directly in the presets folder.</summary>
    private static bool IsPreset(string file, string presets) =>
        file.EndsWith(PresetNames.Extension, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetDirectoryName(file), presets, StringComparison.Ordinal);

    /// <summary>
    /// The preset with every recording it names from outside the folder pointed at its place in the zip,
    /// or nothing when it names none or will not read.
    /// </summary>
    private string? Carried(string file, string root, Dictionary<string, string> carried, HashSet<string> used, string beside)
    {
        JsonNode? read;

        try
        {
            read = JsonNode.Parse(File.ReadAllText(file));
        }
        catch (Exception)
        {
            return null;
        }

        if (read is null) return null;

        bool moved = false;

        Walk(read);

        return moved ? read.ToJsonString(Layout) : null;

        void Walk(JsonNode node)
        {
            if (node is JsonObject held)
            {
                foreach (var (key, child) in new List<KeyValuePair<string, JsonNode?>>(held))
                {
                    if (child is JsonValue value && Moved(value) is { } named)
                        held[key] = named;
                    else if (child is not null)
                        Walk(child);
                }
            }
            else if (node is JsonArray list)
            {
                for (int at = 0; at < list.Count; at++)
                {
                    if (list[at] is JsonValue value && Moved(value) is { } named)
                        list[at] = named;
                    else if (list[at] is { } child)
                        Walk(child);
                }
            }
        }

        string? Moved(JsonValue value)
        {
            if (!value.TryGetValue(out string? said) || !Outside(said, root)) return null;

            string full = Path.GetFullPath(said);

            if (!carried.TryGetValue(full, out string? inside))
            {
                inside = Unused(beside, Path.GetFileName(full), used);
                carried[full] = inside;
            }

            moved = true;

            return inside;
        }
    }

    /// <summary>Whether that string is a recording on this disc that is not inside the machine's folder.</summary>
    private bool Outside(string said, string root)
    {
        try
        {
            return said.Length > 0 && Path.IsPathRooted(said) && File.Exists(said) && !_paths.Under(said, root);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>A name in the preset's own folder that nothing in the zip has yet, relative to the presets folder.</summary>
    /// <param name="beside">The preset's file name without its extension, which is the folder the recording goes in.</param>
    /// <param name="name">The recording's own file name.</param>
    /// <param name="used">Every name in the zip so far.</param>
    private static string Unused(string beside, string name, HashSet<string> used)
    {
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);

        for (int count = 1; ; count++)
        {
            string tried = beside + "/" + (count == 1 ? name : stem + " " + count + extension);

            if (used.Add(SoundMachineProject.PresetsFolder + "/" + tried)) return tried;
        }
    }

    /// <summary>A relative path with the separators a zip uses.</summary>
    private static string Slashed(string path) => path.Replace('\\', '/');
}
