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

        Seed();
    }

    /// <summary>
    /// Puts a handful of sounds on the shelf the first time there is nothing on it.
    /// </summary>
    /// <remarks>
    /// An empty library is a wall with no answer to "what do I start from". These are six
    /// ordinary starting points, not presets: once one is in the library it is an instrument
    /// like any other, to be renamed, rebuilt or thrown away. Only ever written when the shelf
    /// is bare, so deleting one keeps it deleted.
    /// </remarks>
    private void Seed()
    {
        try
        {
            if (Directory.EnumerateFiles(InstrumentsDirectory, "*" + Extension).Any()) return;

            foreach (var instrument in Starters()) Save(instrument);
        }
        catch (Exception)
        {
            // A shelf that could not be stocked is an empty shelf, not a failure to start.
        }
    }

    /// <summary>
    /// The sounds a fresh library is stocked with: a drum kit's worth and two to play.
    /// </summary>
    /// <remarks>
    /// Written out as instruments rather than kept as a separate kind of thing. A sound you
    /// start from and a sound you own turned out to be the same object once a song stopped
    /// taking its instruments from here and started keeping its own.
    /// </remarks>
    public static IReadOnlyList<TrackerInstrument> Starters() => new List<TrackerInstrument>
    {
        Starter("Kick", new SynthPatch
        {
            Wave = SynthWave.Sine,
            AttackMs = 0, DecayMs = 150, Sustain = 0, ReleaseMs = 40,
            PitchEnvSemitones = 30, PitchEnvMs = 55
        }),
        Starter("Hihat", new SynthPatch
        {
            Wave = SynthWave.Noise,
            AttackMs = 0, DecayMs = 35, Sustain = 0, ReleaseMs = 12
        }),
        Starter("Snare", new SynthPatch
        {
            Wave = SynthWave.Noise,
            AttackMs = 0, DecayMs = 130, Sustain = 0, ReleaseMs = 20,
            PitchEnvSemitones = 8, PitchEnvMs = 35
        }),
        Starter("Bass", new SynthPatch
        {
            Wave = SynthWave.Square,
            AttackMs = 0, DecayMs = 160, Sustain = 0.82, ReleaseMs = 70,
            PitchEnvSemitones = 5, PitchEnvMs = 30
        }),
        Starter("Lead", new SynthPatch
        {
            Wave = SynthWave.Pulse, Duty = 0.5,
            AttackMs = 4, DecayMs = 70, Sustain = 0.55, ReleaseMs = 90,
            VibratoRateHz = 5, VibratoDepthCents = 18
        }),
        Starter("Pad", new SynthPatch
        {
            Wave = SynthWave.Saw,
            AttackMs = 220, DecayMs = 300, Sustain = 0.7, ReleaseMs = 450,
            VibratoRateHz = 3, VibratoDepthCents = 8
        })
    };

    private static TrackerInstrument Starter(string name, SynthPatch patch)
    {
        var instrument = new TrackerInstrument
        {
            Name = name,
            Kind = TrackerInstrumentKind.Synth,
            Patch = patch
        };

        instrument.EnsureId();
        instrument.EnsureShape();

        return instrument;
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
