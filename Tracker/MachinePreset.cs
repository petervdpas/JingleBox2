using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JingleBox2.Tracker;

/// <summary>
/// One place a machine can start from, shipped with the machine as a file.
/// </summary>
/// <remarks>
/// A preset is not an instrument. It belongs to the machine, arrives with the program, and is
/// never on your shelf: picking one writes its settings into whatever instrument you are
/// editing and then has nothing more to do with it. What you keep afterwards is yours, called
/// what you called it, and changing it changes nothing here.
///
/// That is the difference the shelf could not express. An instrument seeded into the rack
/// is one more thing to scroll past, to rename by accident, and to wonder whether you made.
/// </remarks>
public sealed record MachinePreset(string Name, TrackerInstrument Sound)
{
    public override string ToString() => Name;
}

/// <summary>
/// What each machine comes with: a folder of files, one preset to a file.
/// </summary>
/// <remarks>
/// Files rather than code, so a preset can be added, edited or taken out without a build, and
/// so an instrument saved off the rack can be dropped straight in as one: a preset file is
/// an instrument file, the same shape, read by the same reader.
///
/// The folder is named after the machine, beside the program. The number a filename starts with
/// is only there to hold the order they are offered in; the name on the panel is the one inside
/// the file.
/// </remarks>
public static class MachinePresets
{
    /// <summary>Where the shipped presets live, beside the program rather than in your data.</summary>
    public static string Directory { get; } =
        Path.Combine(AppContext.BaseDirectory, "Presets");

    private static readonly Dictionary<string, IReadOnlyList<MachinePreset>> Loaded = new();

    /// <summary>
    /// What this machine offers. Read once and kept, since the folder does not change under us.
    /// </summary>
    public static IReadOnlyList<MachinePreset> For(Machine? machine)
    {
        if (machine == null) return Array.Empty<MachinePreset>();

        lock (Loaded)
        {
            if (Loaded.TryGetValue(machine.Name, out var already)) return already;

            var read = Read(machine);
            Loaded[machine.Name] = read;

            return read;
        }
    }

    private static IReadOnlyList<MachinePreset> Read(Machine machine)
    {
        string folder = Path.Combine(Directory, machine.Name);

        try
        {
            if (!System.IO.Directory.Exists(folder)) return Array.Empty<MachinePreset>();

            var presets = new List<MachinePreset>();

            foreach (string path in System.IO.Directory
                         .EnumerateFiles(folder, "*" + MachineRack.Extension)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var sound = Load(path, machine);
                if (sound != null) presets.Add(new MachinePreset(sound.Name, sound));
            }

            return presets;
        }
        catch (Exception)
        {
            // A machine whose presets cannot be read is a machine with none, not a crash on
            // the way to the panel.
            return Array.Empty<MachinePreset>();
        }
    }

    /// <summary>
    /// Turns a preset's relative recordings into real paths, against the folder it came from.
    /// </summary>
    /// <remarks>
    /// A kit that names its recordings relatively carries them: put the files beside the preset
    /// and the whole thing travels, to another machine or to somebody else. A kit that names an
    /// absolute path is left alone and points wherever it points, which is fine for one you
    /// built out of your own rack and no good for one that ships.
    /// </remarks>
    private static void Locate(TrackerInstrument sound, string? folder)
    {
        if (folder == null) return;

        foreach (var pad in sound.Kit?.Pads ?? Enumerable.Empty<DrumPad>())
        {
            if (!pad.HasSound || Path.IsPathRooted(pad.FilePath)) continue;

            pad.FilePath = Path.GetFullPath(Path.Combine(folder, pad.FilePath));
        }

        foreach (var zone in sound.Zones?.Zones ?? Enumerable.Empty<SampleZone>())
        {
            if (!zone.HasSound || Path.IsPathRooted(zone.FilePath)) continue;

            zone.FilePath = Path.GetFullPath(Path.Combine(folder, zone.FilePath));
        }

        if (sound.FilePath.Length > 0 && !Path.IsPathRooted(sound.FilePath))
            sound.FilePath = Path.GetFullPath(Path.Combine(folder, sound.FilePath));
    }

    private static TrackerInstrument? Load(string path, Machine machine)
    {
        try
        {
            var sound = JsonSerializer.Deserialize<TrackerInstrument>(File.ReadAllText(path));
            if (sound == null) return null;

            // A file in a machine's folder is that machine's, whatever it says inside: dropping
            // an instrument in as a preset should not need it edited first.
            sound.Kind = machine.Kind;
            sound.Patch.Clamp();
            sound.Ouroboros?.Clamp();
            sound.Kit?.Clamp();
            sound.Zones?.Clamp();
            sound.Zampler?.Clamp();

            Locate(sound, Path.GetDirectoryName(path));

            if (string.IsNullOrWhiteSpace(sound.Name))
                sound.Name = Path.GetFileNameWithoutExtension(path);

            return sound;
        }
        catch (Exception)
        {
            // One unreadable preset is one preset, not the whole folder.
            return null;
        }
    }
}
