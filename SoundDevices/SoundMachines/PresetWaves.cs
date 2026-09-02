using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <param name="paths">
/// The two questions asked of a path inside a machine. Left out, the ordinary one, built on
/// <paramref name="files"/>.
/// </param>
/// <param name="files">
/// How this system decides two paths are the same. Left out, the rule this system really has;
/// given, whatever a test wants to hold it to.
/// </param>
public sealed class PresetWaves(ISoundMachinePaths? paths = null, IFilePaths? files = null) : IPresetWaves
{
    /// <summary>How two paths are compared, which is a fact about the disc and not about here.</summary>
    private readonly IFilePaths _files = files ?? new FilePaths();

    /// <summary>Where a name written inside a machine really is, and back again.</summary>
    private readonly ISoundMachinePaths _paths = paths ?? new SoundMachinePaths(files);

    /// <summary>What a recording is called at the end, and the whole of how one is recognised.</summary>
    private const string Kind = ".wav";

    /// <inheritdoc/>
    public IReadOnlyList<string> Named(string presetPath)
    {
        var found = new List<string>();

        try
        {
            Walk(JsonNode.Parse(File.ReadAllText(presetPath)), found);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }

        return found.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc/>
    public bool IsWave(string? said) =>
        said is { Length: > 4 } && said.EndsWith(Kind, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string? Folder(string presetPath, string home)
    {
        foreach (string named in Named(presetPath))
        {
            string full = _paths.Outside(named, home);

            if (Path.GetDirectoryName(full) is not { Length: > 0 } folder) continue;

            if (_paths.Under(folder, home)) return folder;
        }

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Users(string folder, string home, IEnumerable<string> presets)
    {
        var found = new List<string>();

        if (folder.Length == 0) return found;

        foreach (string preset in presets)
        {
            foreach (string named in Named(preset))
            {
                string full = _paths.Outside(named, home);

                if (!_files.SameFile(Path.GetDirectoryName(full), folder)) continue;

                found.Add(preset);

                break;
            }
        }

        return found;
    }

    /// <summary>Every string anywhere in the document that looks like a recording.</summary>
    /// <remarks>
    /// The whole tree rather than the keys a machine happens to use, which is the point: this
    /// has to work for a machine written after it, whose keys nobody here has ever seen.
    /// </remarks>
    private void Walk(JsonNode? node, List<string> found)
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
