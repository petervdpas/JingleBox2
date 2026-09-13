using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.SoundDevices.SoundMachines.Records;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
public sealed class SoundMachinePresets : IPresetLibrary
{
    /// <summary>The machines this run has, so a preset can be read against its own machine.</summary>
    private readonly ISoundMachineProjects _machines;

    /// <summary>How a preset file is read.</summary>
    private readonly ISoundMachinePresetFile _files;

    /// <summary>What ships, so a preset of the machine's own can be told from one of yours.</summary>
    private readonly IRackRegistry<SoundMachineProject> _registry;

    /// <summary>How a preset of yours is written whole.</summary>
    private readonly ISafeFile _writer;

    /// <summary>The recordings a preset of yours keeps beside it.</summary>
    private readonly IPresetRecordings _recordings = new PresetRecordings();

    /// <summary>Takes the machines this run has, and how to read a preset off the disc.</summary>
    /// <remarks>
    /// The machines are required rather than defaulted. A fresh <c>SoundMachineProjects</c> holds
    /// nothing, so a default would answer that every machine is missing and every preset
    /// belongs to nothing, with no error raised anywhere to say why the shelf came back empty.
    /// </remarks>
    /// <param name="machines">The machines this run has, the one instance everything shares.</param>
    /// <param name="files">How a preset is read. Left out, the ordinary reader.</param>
    /// <param name="registry">What ships. Left out, the folders this installation really has.</param>
    /// <param name="writer">How a preset of yours is written. Left out, the ordinary one.</param>
    public SoundMachinePresets(ISoundMachineProjects machines, ISoundMachinePresetFile? files = null,
                               IRackRegistry<SoundMachineProject>? registry = null, ISafeFile? writer = null)
    {
        _machines = machines;
        _files = files ?? new SoundMachinePresetFile();
        _registry = registry ?? new SoundMachineRegistry();
        _writer = writer ?? new SafeFile();
    }

    /// <summary>
    /// What has already been read, by machine name. A machine is walked once per library, and
    /// again after a preset of yours is kept on it or taken off it.
    /// </summary>
    /// <remarks>
    /// One of these per library and not one per program. As a static it was shared by everything
    /// in the process and outlived whatever it was about: one test's read decided what the next
    /// test saw, in whatever order they happened to run, and a machine reinstalled under the
    /// same name went on offering the presets it used to have until the application was closed.
    /// </remarks>
    private readonly Dictionary<string, IReadOnlyList<SoundMachinePreset>> _loaded = new();

    /// <inheritdoc/>
    public IReadOnlyList<SoundMachinePreset> For(SoundMachine? machine)
    {
        if (machine == null) return Array.Empty<SoundMachinePreset>();

        lock (_loaded)
        {
            if (_loaded.TryGetValue(machine.Name, out var already)) return already;

            var read = Read(machine);
            _loaded[machine.Name] = read;

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
    private IReadOnlyList<SoundMachinePreset> Read(SoundMachine machine)
    {
        string folder = Folder(machine);

        try
        {
            if (!System.IO.Directory.Exists(folder)) return Array.Empty<SoundMachinePreset>();

            var presets = new List<SoundMachinePreset>();

            foreach (string path in System.IO.Directory
                         .EnumerateFiles(folder, "*" + SoundMachineRack.Extension)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var sound = Load(path, machine);
                if (sound != null)
                    presets.Add(new SoundMachinePreset(sound.Name, sound) { File = path, Yours = !_registry.Ships(path) });
            }

            return presets.OrderBy(one => one.Yours).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<SoundMachinePreset>();
        }
    }

    /// <summary>Which names a preset of yours may have, the rule both worlds keep.</summary>
    private static readonly IPresetNames Names = new PresetNames();

    /// <inheritdoc/>
    public string Refusal(SoundMachine? machine, string name)
    {
        if (Folder(machine) is not { Length: > 0 }) return "This machine is not installed here, so it has nowhere to keep a preset.";

        return Names.Refusal(name, machine!.Name, For(machine).Where(one => !one.Yours).Select(one => (one.Name, one.File)));
    }

    /// <inheritdoc/>
    public SoundMachinePreset? Yours(SoundMachine? machine, string name)
    {
        string called = (name ?? "").Trim();

        return For(machine).FirstOrDefault(one =>
            one.Yours && string.Equals(one.Name, called, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public SoundMachinePreset? Keep(SoundMachine? machine, TrackerInstrument sound, string name)
    {
        if (sound is null || Refusal(machine, name).Length > 0) return null;

        if (_machines.For(machine!.SlotId) is not { } project) return null;

        string called = name.Trim();
        string path = Yours(machine, called)?.File is { Length: > 0 } already
            ? already
            : Path.Combine(Folder(machine), Names.FileFor(called));

        var kept = sound.Clone();

        kept.Name = called;

        try
        {
            _recordings.Gather(kept, Path.Combine(Folder(machine), called));

            _writer.Write(path, _files.Write(kept, project));
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A preset could not be kept: " + path, ex);

            return null;
        }

        Forget(machine);

        return For(machine).FirstOrDefault(one => string.Equals(one.File, path, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public bool Remove(SoundMachine? machine, SoundMachinePreset? preset)
    {
        if (machine is null || preset is not { Yours: true, File.Length: > 0 }) return false;

        string folder = Folder(machine);

        if (folder.Length == 0 || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(preset.File)),
                Path.GetFullPath(folder), StringComparison.Ordinal))
            return false;

        try
        {
            if (!File.Exists(preset.File) || _registry.Ships(preset.File)) return false;

            File.Delete(preset.File);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A preset could not be taken off: " + preset.File, ex);

            return false;
        }

        Forget(machine);

        return true;
    }

    /// <inheritdoc/>
    public bool Owns(SoundMachine? machine, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Folder(machine) is not { Length: > 0 } folder) return false;

        try
        {
            return File.Exists(path) && _paths.Under(path, folder) && !_registry.Ships(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>How a path is tested for being inside a folder.</summary>
    private readonly ISoundMachinePaths _paths = new SoundMachinePaths();

    /// <summary>Forgets what was read for that machine, so the next look reads its folder again.</summary>
    private void Forget(SoundMachine machine)
    {
        lock (_loaded) _loaded.Remove(machine.Name);
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
    private string Folder(SoundMachine? machine) =>
        machine is not null && _machines.For(machine.SlotId) is { Folder.Length: > 0 } project
            ? Path.Combine(project.Folder, SoundDevices.SoundMachines.SoundMachineProject.PresetsFolder)
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
    private void Locate(TrackerInstrument sound, string? folder)
    {
        if (folder == null) return;

        if (sound.Kit is { Source.Length: > 0 } chopped && !Path.IsPathRooted(chopped.Source))
            chopped.Source = Path.GetFullPath(Path.Combine(folder, chopped.Source));

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
    private TrackerInstrument? Load(string path, SoundMachine machine)
    {
        try
        {
            if (_machines.For(machine.SlotId) is { } project
                && _files.Read(path, project) is { } keyed)
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
