using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Devices.SoundMachines.Interfaces;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Devices;

/// <inheritdoc/>
public sealed class PanelImages : IPanelImages
{
    /// <summary>Where the pictures on a face go, inside the box's own folder.</summary>
    /// <remarks>
    /// Written out rather than built, so the one folder name a face's pictures depend on can be
    /// found by looking for it, here and in every manifest that names one.
    /// </remarks>
    public const string FolderName = "images";

    /// <summary>What every picture is called, before its number.</summary>
    public const string Stem = "image";

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    private readonly IFilePaths _paths;

    /// <summary>Whether a path is inside a folder, which a name out of a file may claim wrongly.</summary>
    private readonly ISoundMachinePaths _inside;

    /// <summary>Takes how paths are compared, or the rules this system really has.</summary>
    /// <param name="paths">How two paths are decided to be the same file.</param>
    /// <param name="inside">Whether a path really is under a folder.</param>
    public PanelImages(IFilePaths? paths = null, ISoundMachinePaths? inside = null)
    {
        _paths = paths ?? new FilePaths();
        _inside = inside ?? new SoundMachinePaths(_paths);
    }

    /// <inheritdoc/>
    public string? Add(string folder, string path)
    {
        if (folder.Length == 0) return null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        string images = Path.Combine(folder, FolderName);

        Directory.CreateDirectory(images);

        string suffix = Path.GetExtension(path);

        for (int at = 1; ; at++)
        {
            string stem = Stem + at;

            if (Directory.GetFiles(images, stem + ".*").Length > 0) continue;

            File.Copy(path, Path.Combine(images, stem + suffix));

            return FolderName + "/" + stem + suffix;
        }
    }

    /// <inheritdoc/>
    public int Sweep(string folder, ISet<string> kept)
    {
        if (folder.Length == 0) return 0;

        string images = Path.Combine(folder, FolderName);

        if (!Directory.Exists(images)) return 0;

        int gone = 0;

        try
        {
            foreach (string file in Directory.GetFiles(images))
            {
                string stem = Path.GetFileNameWithoutExtension(file);

                if (!stem.StartsWith(Stem, StringComparison.OrdinalIgnoreCase)) continue;

                if (!int.TryParse(stem[Stem.Length..], out _)) continue;

                if (kept.Contains(FolderName + "/" + Path.GetFileName(file))) continue;

                File.Delete(file);

                gone++;
            }
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "The pictures could not be swept in " + folder, ex);
        }

        return gone;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Renumber(string folder)
    {
        var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (folder.Length == 0) return moved;

        string images = Path.Combine(folder, FolderName);

        if (!Directory.Exists(images)) return moved;

        try
        {
            var ours = new List<(int At, string Path)>();

            foreach (string file in Directory.GetFiles(images))
            {
                string stem = Path.GetFileNameWithoutExtension(file);

                if (!stem.StartsWith(Stem, StringComparison.OrdinalIgnoreCase)) continue;

                if (!int.TryParse(stem[Stem.Length..], out int at)) continue;

                ours.Add((at, file));
            }

            ours.Sort((one, other) => one.At.CompareTo(other.At));

            for (int i = 0; i < ours.Count; i++)
            {
                string was = ours[i].Path;
                string suffix = Path.GetExtension(was);
                string now = Path.Combine(images, Stem + (i + 1) + suffix);

                if (_paths.Same(was, now)) continue;

                File.Move(was, now);

                moved[FolderName + "/" + Path.GetFileName(was)] = FolderName + "/" + Path.GetFileName(now);
            }
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "The pictures could not be renumbered in " + folder, ex);
        }

        return moved;
    }

    /// <inheritdoc/>
    public bool Remove(string folder, string named)
    {
        if (folder.Length == 0 || string.IsNullOrWhiteSpace(named)) return false;

        try
        {
            string images = Path.GetFullPath(Path.Combine(folder, FolderName));
            string wanted = Path.GetFullPath(Path.Combine(folder, named));

            if (!_inside.Under(wanted, images)) return false;

            if (!File.Exists(wanted)) return false;

            File.Delete(wanted);

            Log.Write(LogArea.Machines, () => "picture removed: " + wanted);

            return true;
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "A picture could not be removed from " + folder, ex);

            return false;
        }
    }
}
