using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Synth;
using System.Text.Json;

namespace JingleBox2.Tracker;

/// <summary>
/// The instruments you own, kept outside any song: where a sound starts. One file per
/// instrument, named by its id, so renaming one costs nothing and breaks no song.
/// </summary>
/// <remarks>
/// Taking an instrument into a song copies it, and from then on the copy is the song's. Editing
/// it there changes that song and nothing else, and editing the one here changes what the next
/// song will start from. Two songs can therefore use the same kick sounding differently, which
/// is what anyone who has built a kick for one track and not for another expects.
///
/// The shelf starts empty and stays that way until you put something on it. What a new
/// instrument starts from is its machine's presets, which belong to the machine and are never
/// written here: see <see cref="MachinePreset"/>. Everything on this shelf is yours.
///
/// A synth or a plugin travels inside the song that way, patch and all. A recording does not:
/// the instrument keeps the path it was made from and the audio stays where it is, so a song
/// moved to another machine finds a sample instrument pointing at nothing. Making an instrument
/// hold its own recordings is what would finish this, and it has not been done.
/// </remarks>
public sealed class InstrumentLibrary : ISampleUsage
{
    public const string Extension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string InstrumentsDirectory { get; }

    public InstrumentLibrary(string appName = "JingleBox2")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        InstrumentsDirectory = Path.Combine(baseDir, appName, "instruments");
        Directory.CreateDirectory(InstrumentsDirectory);

    }

    public string PathFor(string id) => Path.Combine(InstrumentsDirectory, id + Extension);

    /// <summary>Everything in the library, by name. Unreadable files are skipped, not fatal.</summary>
    public IReadOnlyList<TrackerInstrument> List()
    {
        if (!Directory.Exists(InstrumentsDirectory)) return Array.Empty<TrackerInstrument>();

        var instruments = new List<TrackerInstrument>();

        foreach (var path in Directory.GetFiles(InstrumentsDirectory, "*" + Extension))
        {
            var instrument = Read(path);
            if (instrument != null) instruments.Add(instrument);
        }

        return instruments
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TrackerInstrument? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return Read(PathFor(id));
    }

    public void Save(TrackerInstrument instrument)
    {
        if (instrument is null) return;

        instrument.EnsureId();
        File.WriteAllText(PathFor(instrument.Id), JsonSerializer.Serialize(instrument, JsonOptions));
    }

    /// <summary>
    /// The instruments that play a given recording. A sample instrument owns no copy of its
    /// file, so this is what a recording has to be asked about before it is thrown away.
    /// </summary>
    public IReadOnlyList<string> InstrumentsUsing(string filePath) => SampleUsage.By(List(), filePath);

    /// <summary>
    /// Follows a recording to its new place, for every instrument on the shelf that plays it.
    /// </summary>
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
            if (!SampleUsage.Repoint(instrument, from, to)) continue;

            Save(instrument);
            moved++;
        }

        return moved;
    }

    /// <summary>False when there was nothing to remove.</summary>
    public bool Delete(string id)
    {
        string path = PathFor(id);
        if (!File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }

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
