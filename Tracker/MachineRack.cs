using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Synth;
using System.Text.Json;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Files.Interfaces;
using JingleBox2.Files;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// A folder under the application data directory, walked on every question. There is no index
/// and nothing is held between calls, which is what lets the folder be somewhere a person can
/// open: a file dropped in shows up, and one taken out stops showing up.
/// </remarks>
public sealed class MachineRack : IMachineRack
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
    public MachineRack(string appName = AppFolder.AppName, IAppFolder? folder = null, ISafeFile? files = null)
    {
        _files = files ?? new SafeFile();

        Folder = Path.Combine((folder ?? new AppFolder()).Path(appName), "instruments");
        Directory.CreateDirectory(Folder);
    }

    /// <summary>How a file is written whole, so an instrument save cannot leave half of one.</summary>
    private readonly ISafeFile _files;

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
    public void Save(TrackerInstrument instrument)
    {
        if (instrument is null) return;

        instrument.EnsureId();
        _files.Write(PathFor(instrument.Id), JsonSerializer.Serialize(instrument, JsonOptions));
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
    private static TrackerInstrument? Read(string path)
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

            return instrument;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
