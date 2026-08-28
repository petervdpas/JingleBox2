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
/// <param name="Name">
/// What it is called in the picker, which is the name inside the file rather than the file's
/// own. A filename starts with a number only to hold the order they are offered in.
/// </param>
/// <param name="Sound">
/// The settings, as a whole instrument, because a preset file is an instrument file: the same
/// shape, read by the same reader, so one saved off the rack can be dropped straight in.
/// </param>
public sealed record MachinePreset(string Name, TrackerInstrument Sound)
{
    /// <summary>Its name, so a preset can be dropped straight into a picker.</summary>
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
    /// <summary>
    /// What has already been read, by machine name. The folder does not change under us, so a
    /// machine is walked once a run.
    /// </summary>
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

    /// <summary>
    /// Walks the machine's presets folder, in filename order.
    /// </summary>
    /// <remarks>
    /// A machine whose presets cannot be read is a machine with none, not a crash on the way to
    /// the panel: the folder may not be there at all, which is ordinary for a machine that
    /// ships without any.
    /// </remarks>
    private static IReadOnlyList<MachinePreset> Read(Machine machine)
    {
        string folder = Folder(machine);

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
            return Array.Empty<MachinePreset>();
        }
    }

    /// <summary>
    /// Which folder holds that machine's presets, or nothing when it is not installed.
    /// </summary>
    /// <remarks>
    /// Inside the machine, which is where a machine keeps everything else it ships: the panel,
    /// the pictures, and the recordings a kit is built out of. There was a folder beside the
    /// program for the machines waiting to become projects, and it went with the last of them:
    /// presets that sat out there arrived on somebody else's disc with an empty picker.
    ///
    /// By id and not by name, because the name is what the machine calls itself and can be
    /// changed by whoever imports a new version of it. The id is what it is.
    /// </remarks>
    private static string Folder(Machine machine) =>
        Machines.MachineProjects.For(machine.SlotId) is { Folder.Length: > 0 } project
            ? Path.Combine(project.Folder, Machines.MachineProject.PresetsFolder)
            : "";

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

    /// <summary>
    /// One preset file, in whichever of the two shapes it was written.
    /// </summary>
    /// <remarks>
    /// The newer shape is written the way the machine is drawn, if that machine has been
    /// converted: one small piece of JSON per control, keyed by what the control is called. The
    /// older shape is a whole instrument, which is what every unconverted machine's presets
    /// still are, and it is what the reader falls back to.
    ///
    /// A file in a machine's folder is that machine's, whatever it says inside, so the kind is
    /// overwritten on the way past: dropping an instrument in as a preset should not need it
    /// edited first.
    ///
    /// One unreadable preset is one preset, not the whole folder.
    /// </remarks>
    private static TrackerInstrument? Load(string path, Machine machine)
    {
        try
        {
            if (Machines.MachineProjects.For(machine.SlotId) is { } project
                && Machines.MachinePresetFile.Read(path, project) is { } keyed)
            {
                if (string.IsNullOrWhiteSpace(keyed.Name))
                    keyed.Name = Path.GetFileNameWithoutExtension(path);

                return keyed;
            }

            var sound = JsonSerializer.Deserialize<TrackerInstrument>(File.ReadAllText(path));
            if (sound == null) return null;

            sound.Kind = machine.Kind;
            sound.Patch.Clamp();
            sound.MonoSynth?.Clamp();
            sound.Kit?.Clamp();
            sound.Zones?.Clamp();
            sound.Sampler?.Clamp();

            Locate(sound, Path.GetDirectoryName(path));

            if (string.IsNullOrWhiteSpace(sound.Name))
                sound.Name = Path.GetFileNameWithoutExtension(path);

            return sound;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
