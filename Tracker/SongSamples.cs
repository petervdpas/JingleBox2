using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class SongSamples : ISongSamples
{
    /// <summary>The one door recordings come in through. Holds nothing, so one is enough.</summary>
    private readonly IRecordingImport _import = new RecordingImport();

    /// <summary>The machines folder on disc.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IRackRegistry<SoundMachineProject> Registry = new SoundMachineRegistry();

    /// <summary>How this system decides two names are the same recording.</summary>
    private readonly IFilePaths _paths;

    /// <summary>How a path is written down so it survives the application folder moving.</summary>
    private readonly ISongPaths _songPaths;

    /// <summary>What an instrument plays, and how to point it somewhere else.</summary>
    private readonly ISampleUsers _usage;

    /// <summary>
    /// Takes the three rules this needs, or makes the ones the application really uses.
    /// </summary>
    /// <param name="paths">Which paths count as the same file.</param>
    /// <param name="songPaths">How a recording's path is written into a song and read back.</param>
    /// <param name="usage">What an instrument plays, and how to repoint it.</param>
    public SongSamples(IFilePaths? paths = null, ISongPaths? songPaths = null, ISampleUsers? usage = null)
    {
        _paths = paths ?? new FilePaths();
        _songPaths = songPaths ?? new SongPaths(_paths);
        _usage = usage ?? new SampleUsers(_paths);
    }

    /// <summary>What the container calls the list of what it carries.</summary>
    public const string ManifestEntry = "samples.json";

    /// <inheritdoc/>
    string ISongSamples.ManifestEntry => ManifestEntry;

    /// <summary>Where a carried recording sits inside the container.</summary>
    private const string Folder = "samples/";

    /// <summary>Indented, since the manifest is small and somebody may well open the zip.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>One recording a song carries: where it is in the file, and what it was.</summary>
    public sealed class Carried
    {
        /// <summary>The entry in the container.</summary>
        public string Entry { get; set; } = "";

        /// <summary>Where it was on the machine that packed it, as the song writes paths.</summary>
        public string Path { get; set; } = "";
    }

    /// <summary>The list itself.</summary>
    public sealed class Manifest
    {
        /// <summary>What shape this list is, so a later one can be told from this.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Every recording the container carries.</summary>
        public List<Carried> Files { get; set; } = new();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Wanted(Song song)
    {
        var files = new List<string>();
        if (song == null) return files;

        var seen = new HashSet<string>(_paths.Comparer);

        foreach (var instrument in song.Instruments)
            foreach (string path in _usage.Files(instrument))
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                string full = _paths.Full(path);

                if (!seen.Add(full)) continue;
                if (Registry.Ships(full)) continue;
                if (!File.Exists(full)) continue;

                files.Add(full);
            }

        return files;
    }

    /// <inheritdoc/>
    public void Write(ZipArchive container, IReadOnlyList<string> files)
    {
        if (container == null || files == null || files.Count == 0) return;

        var manifest = new Manifest();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in files)
        {
            try
            {
                string name = Free(System.IO.Path.GetFileName(path), taken);

                var entry = container.CreateEntry(Folder + name, CompressionLevel.NoCompression);

                using (var writing = entry.Open())
                using (var reading = File.OpenRead(path))
                    reading.CopyTo(writing);

                manifest.Files.Add(new Carried { Entry = Folder + name, Path = _songPaths.Pack(path) });
            }
            catch (Exception)
            {
            }
        }

        if (manifest.Files.Count == 0) return;

        var list = container.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
        using var said = list.Open();
        JsonSerializer.Serialize(said, manifest, JsonOptions);
    }

    /// <inheritdoc/>
    public bool Packed(ZipArchive container) =>
        container != null && container.GetEntry(ManifestEntry) != null;

    /// <inheritdoc/>
    public IReadOnlyList<string> Read(ZipArchive container, Song song)
    {
        var landed = new List<string>();

        if (container == null || song == null) return landed;

        var list = container.GetEntry(ManifestEntry);
        if (list == null) return landed;

        Manifest? manifest;

        try
        {
            using var said = list.Open();
            manifest = JsonSerializer.Deserialize<Manifest>(said, JsonOptions);
        }
        catch (Exception)
        {
            return landed;
        }

        if (manifest == null || manifest.Files.Count == 0) return landed;

        string home = _import.Directory;
        Directory.CreateDirectory(home);

        foreach (var carried in manifest.Files)
        {
            try
            {
                var entry = container.GetEntry(carried.Entry);
                if (entry == null) continue;

                string was = _songPaths.Unpack(carried.Path);
                string now = Land(entry, home, out bool fresh);
                if (now.Length == 0) continue;

                foreach (var instrument in song.Instruments)
                    _usage.Repoint(instrument, was, now);

                if (fresh) landed.Add(now);
            }
            catch (Exception)
            {
            }
        }

        return landed;
    }

    /// <summary>
    /// Gets one carried recording onto the shelf, and says where it went.
    /// </summary>
    /// <remarks>
    /// A take already there under that name and byte for byte the same is the same take, and
    /// is used where it lies. Without that, opening the same packed song twice would leave two
    /// copies of every recording in it, and three times, three.
    ///
    /// A different file under the same name is a different file. It gets a name of its own,
    /// the way anything imported does, because two kits can each have a kick.wav and neither
    /// should quietly become the other.
    /// </remarks>
    private static string Land(ZipArchiveEntry entry, string home, out bool fresh)
    {
        fresh = false;

        string stem = System.IO.Path.GetFileNameWithoutExtension(entry.Name);
        string suffix = System.IO.Path.GetExtension(entry.Name);
        string wanted = System.IO.Path.Combine(home, stem + suffix);
        int at = 2;

        while (File.Exists(wanted))
        {
            if (Same(entry, wanted)) return wanted;

            wanted = System.IO.Path.Combine(home, stem + " " + at.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix);
            at++;
        }

        using (var writing = File.Create(wanted))
        using (var reading = entry.Open())
            reading.CopyTo(writing);

        fresh = true;
        return wanted;
    }

    /// <summary>True when what is in the container is what is already on the shelf.</summary>
    private static bool Same(ZipArchiveEntry entry, string path)
    {
        try
        {
            var already = new FileInfo(path);
            if (already.Length != entry.Length) return false;

            using var reading = entry.Open();
            using var held = File.OpenRead(path);

            var one = new byte[64 * 1024];
            var other = new byte[64 * 1024];

            while (true)
            {
                int read = reading.ReadAtLeast(one, one.Length, throwOnEndOfStream: false);
                held.ReadExactly(other, 0, read);

                if (!one.AsSpan(0, read).SequenceEqual(other.AsSpan(0, read))) return false;
                if (read < one.Length) return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>A name nothing else in this container has taken.</summary>
    private static string Free(string name, HashSet<string> taken)
    {
        if (taken.Add(name)) return name;

        string stem = System.IO.Path.GetFileNameWithoutExtension(name);
        string suffix = System.IO.Path.GetExtension(name);

        for (int at = 2; ; at++)
        {
            string tried = stem + " " + at.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
            if (taken.Add(tried)) return tried;
        }
    }
}
