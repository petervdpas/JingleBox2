using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JingleBox2.Tracker;

/// <summary>
/// The instruments you own, kept outside any song so the same voice can play in all of them.
/// One file per instrument, named by its id: renaming an instrument then costs nothing and
/// breaks no song that uses it.
/// </summary>
/// <remarks>
/// A song stores a copy of every instrument it uses, and rebinds those copies to the library
/// by id when it opens. That way an edit here reaches every song, and a song handed to someone
/// without your library still plays.
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

    /// <summary>False when there was nothing to remove.</summary>
    public bool Delete(string id)
    {
        string path = PathFor(id);
        if (!File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Points a song's instruments back at the library. Each slot keeps its number, so the
    /// pattern cells still refer to the same thing; only the sound is brought up to date.
    /// Slots whose instrument is no longer in the library keep the copy the song was saved with.
    /// </summary>
    public int Rebind(Song song)
    {
        if (song is null) return 0;

        int rebound = 0;

        foreach (var slot in song.Instruments)
        {
            if (string.IsNullOrWhiteSpace(slot.Id)) continue;

            var current = Load(slot.Id);
            if (current == null) continue;

            slot.CopyFrom(current);
            rebound++;
        }

        return rebound;
    }

    private static TrackerInstrument? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var instrument = JsonSerializer.Deserialize<TrackerInstrument>(File.ReadAllText(path), JsonOptions);
            if (instrument == null) return null;

            instrument.EnsureId();
            instrument.Patch ??= new Synth.SynthPatch();
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
