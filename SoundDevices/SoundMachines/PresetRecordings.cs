using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <param name="paths">How a path is tested for being inside a folder. Left out, the ordinary one.</param>
public sealed class PresetRecordings(ISoundMachinePaths? paths = null) : IPresetRecordings
{
    /// <summary>How a path is tested for being inside a folder.</summary>
    private readonly ISoundMachinePaths _paths = paths ?? new SoundMachinePaths();

    /// <inheritdoc/>
    public void Gather(TrackerInstrument sound, string beside)
    {
        if (sound is null || string.IsNullOrWhiteSpace(beside)) return;

        var moved = new Dictionary<string, string>(StringComparer.Ordinal);

        string Copied(string path) => Copy(path, beside, moved);

        if (sound.Kit is { } kit)
        {
            foreach (var pad in kit.Pads) pad.FilePath = Copied(pad.FilePath);

            kit.Source = Copied(kit.Source);
        }

        foreach (var zone in sound.Zones?.Zones ?? new List<SampleZone>()) zone.FilePath = Copied(zone.FilePath);

        sound.FilePath = Copied(sound.FilePath ?? "");
    }

    /// <summary>Where that recording is kept in the preset's folder, copying it there the first time it is met.</summary>
    private string Copy(string path, string beside, Dictionary<string, string> moved)
    {
        if (string.IsNullOrEmpty(path)) return path;

        try
        {
            if (!File.Exists(path)) return path;

            string full = Path.GetFullPath(path);

            if (_paths.Under(full, beside)) return full;

            if (moved.TryGetValue(full, out string? already)) return already;

            Directory.CreateDirectory(beside);

            string target = Free(full, beside);

            if (!File.Exists(target)) File.Copy(full, target);

            moved[full] = target;

            return target;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A recording could not be kept with its preset: " + path, ex);

            return path;
        }
    }

    /// <summary>
    /// The file in the folder that recording goes to: its own name, or the same file already there, or its name numbered.
    /// </summary>
    private static string Free(string full, string beside)
    {
        string name = Path.GetFileName(full);
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);

        for (int count = 1; ; count++)
        {
            string tried = Path.Combine(beside, count == 1 ? name : stem + " " + count + extension);

            if (!File.Exists(tried) || Same(full, tried)) return tried;
        }
    }

    /// <summary>Whether two files hold the same bytes.</summary>
    private static bool Same(string one, string other)
    {
        var first = new FileInfo(one);
        var second = new FileInfo(other);

        if (first.Length != second.Length) return false;

        using var a = first.OpenRead();
        using var b = second.OpenRead();

        var left = new byte[81920];
        var right = new byte[81920];

        while (true)
        {
            int read = a.Read(left, 0, left.Length);
            int also = b.Read(right, 0, right.Length);

            if (read != also) return false;
            if (read == 0) return true;
            if (!left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, read))) return false;
        }
    }
}
