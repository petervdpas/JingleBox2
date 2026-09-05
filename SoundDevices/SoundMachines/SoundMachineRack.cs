using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Synth;
using System.Text.Json;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Files.Interfaces;
using JingleBox2.Files;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <remarks>
/// A folder under the application data directory, walked on every question. There is no index
/// and nothing is held between calls, which is what lets the folder be somewhere a person can
/// open: a file dropped in shows up, and one taken out stops showing up.
/// </remarks>
public sealed class SoundMachineRack : ISoundMachineRack
{
    /// <summary>Which instruments play a given recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISampleUsers Usage = new SampleUsers();

    /// <summary>What an instrument file is called. JSON, so it can be read and edited by hand.</summary>
    public const string Extension = ".json";

    /// <summary>
    /// Indented on purpose. These files are somewhere a person can go and look, and a patch
    /// written on one line is a file nobody can read or diff.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <inheritdoc/>
    public string Folder { get; }

    /// <summary>
    /// Opens the rack under the application data folder, making it if it is not there.
    /// </summary>
    /// <param name="appName">
    /// Which application folder to sit in. Given rather than fixed so the tests can point the
    /// whole rack at a temporary one.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="files">How a file is written whole, defaulted to the real one.</param>
    /// <param name="portable">
    /// How a path under the application folder is stored, defaulted to the real one. What a
    /// device on the rack names is almost always inside that folder, and a full path written
    /// there is a path that means nothing on another machine or after the folder moves.
    /// </param>
    public SoundMachineRack(string appName = AppFolder.AppName, IAppFolder? folder = null,
                            ISafeFile? files = null, ISongPaths? portable = null)
    {
        _files = files ?? new SafeFile();
        _portable = portable ?? new SongPaths(folder: folder);

        Folder = Path.Combine((folder ?? new AppFolder()).Path(appName), "instruments");
        Directory.CreateDirectory(Folder);
    }

    /// <summary>How a file is written whole, so an instrument save cannot leave half of one.</summary>
    private readonly ISafeFile _files;

    /// <summary>
    /// How the recordings a device names are stored, so the rack survives moving machine.
    /// </summary>
    /// <remarks>
    /// The same rule a song already kept, applied one layer along. What a device on the rack
    /// names is a take off the shelf or a wave inside a device's own folder, and both of those
    /// are inside the application folder, which is somewhere different on every machine and
    /// spelled differently on every platform. Written whole, a kit carried to another computer is
    /// sixteen pads pointing at somebody else's home directory, and nothing says so: the pads are
    /// simply silent.
    /// </remarks>
    private readonly ISongPaths _portable;

    /// <inheritdoc/>
    public string PathFor(string id) => Path.Combine(Folder, id + Extension);

    /// <inheritdoc/>
    public IReadOnlyList<TrackerInstrument> List()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<TrackerInstrument>();

        var instruments = new List<TrackerInstrument>();

        foreach (var path in Directory.GetFiles(Folder, "*" + Extension))
        {
            var instrument = Read(path);
            if (instrument != null) instruments.Add(instrument);
        }

        return instruments
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public TrackerInstrument? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return Read(PathFor(id));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Packed for the file and put straight back, rather than copied: the rack hands out the
    /// instruments it holds and the pages on screen are looking at these very objects, so an
    /// instrument left holding <c>{app}/</c> names after a save is one that plays nothing until
    /// it is read again. In a <c>finally</c>, since a write that throws must not leave it that
    /// way either.
    /// </remarks>
    public void Save(TrackerInstrument instrument)
    {
        if (instrument is null) return;

        instrument.EnsureId();

        try
        {
            _portable.PackInto(instrument);

            _files.Write(PathFor(instrument.Id), JsonSerializer.Serialize(instrument, JsonOptions));
        }
        finally
        {
            _portable.UnpackInto(instrument);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> InstrumentsUsing(string filePath) => Usage.By(List(), filePath);

    /// <inheritdoc/>
    /// <remarks>
    /// The shelf only. A song holds its own copies of the instruments it uses, so a song that
    /// is open is repointed by whoever is holding it and a song on disc keeps the old path
    /// until it is opened and its instrument taken from the shelf again.
    /// </remarks>
    public int Repoint(string from, string to)
    {
        int moved = 0;

        foreach (var instrument in List())
        {
            if (!Usage.Repoint(instrument, from, to)) continue;

            Save(instrument);
            moved++;
        }

        return moved;
    }

    /// <inheritdoc/>
    public string RetiredDirectory => Path.Combine(Folder, "retired");

    /// <inheritdoc/>
    /// <remarks>
    /// Never overwrites: two instruments with the same name can have been through here before,
    /// and the whole point of moving rather than deleting is lost the moment one lands on
    /// another. A number is added until the name is free.
    /// </remarks>
    public bool Retire(string id)
    {
        string path = PathFor(id);

        if (!File.Exists(path)) return false;

        Directory.CreateDirectory(RetiredDirectory);

        string landed = Path.Combine(RetiredDirectory, Path.GetFileName(path));

        int at = 2;

        while (File.Exists(landed))
        {
            landed = Path.Combine(RetiredDirectory, id + " " + at + Extension);
            at++;
        }

        File.Move(path, landed);

        return true;
    }

    /// <inheritdoc/>
    public bool Delete(string id)
    {
        string path = PathFor(id);
        if (!File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// One instrument file, brought back into shape on the way in.
    /// </summary>
    /// <remarks>
    /// A file may have been written by an older version, edited by hand, or half written by a
    /// crash. It is given an id if it has none, its patch is straightened, and its shape is
    /// worked out from the loop flag an older build wrote. Anything that will not read at all
    /// is one instrument missing rather than a rack that refuses to open.
    /// </remarks>
    private TrackerInstrument? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var instrument = JsonSerializer.Deserialize<TrackerInstrument>(File.ReadAllText(path), JsonOptions);
            if (instrument == null) return null;

            instrument.EnsureId();
            instrument.Patch ??= new SynthPatch();
            instrument.Patch.Clamp();
            instrument.EnsureShape();

            _portable.UnpackInto(instrument);

            return instrument;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>What the record of shelved machines is called, beside the instruments it is about.</summary>
    private const string ShelvedFile = "shelved.txt";

    /// <inheritdoc/>
    /// <remarks>
    /// A plain list of ids, one to a line, so it can be read and corrected by hand the way the
    /// registry's own record can. A missing file reads as nothing shelved, which is what a rack
    /// that has never been opened has.
    /// </remarks>
    public IReadOnlyCollection<string> Shelved
    {
        get
        {
            string path = Path.Combine(Folder, ShelvedFile);

            try
            {
                return File.Exists(path)
                    ? new HashSet<string>(
                        File.ReadAllLines(path).Select(one => one.Trim()).Where(one => one.Length > 0),
                        StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Appended rather than rewritten, and a machine already on the list is left alone.</remarks>
    public void Shelve(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || Shelved.Contains(id)) return;

        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, ShelvedFile), id + Environment.NewLine);
        }
        catch (Exception)
        {
        }
    }
}