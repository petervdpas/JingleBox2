using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JingleBox2.Tracker.Synth;

public sealed record SynthPreset(string Name, SynthPatch Patch);

/// <summary>
/// A preset bank kept apart from any song, so a voice you build can be loaded into the next
/// one. Each preset is one file; the built-in starters are always in the list, and a saved
/// preset of the same name takes their place.
/// </summary>
public sealed class SynthPresetStore
{
    public const string Extension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Regex Unsafe = new("[^a-zA-Z0-9-_ ]", RegexOptions.Compiled);

    public string PresetsDirectory { get; }

    public SynthPresetStore(string appName = "JingleBox2")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        PresetsDirectory = Path.Combine(baseDir, appName, "presets");
        Directory.CreateDirectory(PresetsDirectory);
    }

    /// <summary>A file name that cannot escape the preset folder or upset the file system.</summary>
    public static string SafeName(string? name)
    {
        string cleaned = Unsafe.Replace(name ?? "", "").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", "-");
        return cleaned.Length == 0 ? "instrument" : cleaned;
    }

    public string PathFor(string name) => Path.Combine(PresetsDirectory, SafeName(name) + Extension);

    /// <summary>Saved presets first, then whichever starters have not been overridden.</summary>
    public IReadOnlyList<SynthPreset> List()
    {
        var saved = new List<SynthPreset>();

        if (Directory.Exists(PresetsDirectory))
        {
            foreach (var path in Directory.GetFiles(PresetsDirectory, "*" + Extension))
            {
                var patch = Read(path);
                if (patch != null) saved.Add(new SynthPreset(Path.GetFileNameWithoutExtension(path), patch));
            }
        }

        var names = new HashSet<string>(saved.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

        return saved
            .Concat(Starters().Where(p => !names.Contains(p.Name)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SynthPatch? Load(string name)
    {
        var preset = List().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return preset?.Patch.Clone();
    }

    public void Save(string name, SynthPatch patch)
    {
        var stored = patch.Clone();
        stored.Clamp();

        File.WriteAllText(PathFor(name), JsonSerializer.Serialize(stored, JsonOptions));
    }

    public void Delete(string name)
    {
        string path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Puts the starters back by dropping the saved presets that shadow them. Presets of your
    /// own, under other names, are left alone.
    /// </summary>
    public void ResetStarters()
    {
        foreach (var preset in Starters())
            Delete(preset.Name);
    }

    private static SynthPatch? Read(string path)
    {
        try
        {
            var patch = JsonSerializer.Deserialize<SynthPatch>(File.ReadAllText(path), JsonOptions);
            patch?.Clamp();
            return patch;
        }
        catch (Exception)
        {
            // An unreadable preset is skipped rather than breaking the list.
            return null;
        }
    }

    /// <summary>
    /// The built-in bank. Drums have no sustain and end on their decay; the kick is a sine
    /// with a fast pitch drop, and the hat and snare are short noise bursts.
    /// </summary>
    public static IReadOnlyList<SynthPreset> Starters() => new List<SynthPreset>
    {
        new("Kick", new SynthPatch
        {
            Wave = SynthWave.Sine,
            AttackMs = 0, DecayMs = 150, Sustain = 0, ReleaseMs = 40,
            PitchEnvSemitones = 30, PitchEnvMs = 55
        }),
        new("Hihat", new SynthPatch
        {
            Wave = SynthWave.Noise,
            AttackMs = 0, DecayMs = 35, Sustain = 0, ReleaseMs = 12
        }),
        new("Snare", new SynthPatch
        {
            Wave = SynthWave.Noise,
            AttackMs = 0, DecayMs = 130, Sustain = 0, ReleaseMs = 20,
            PitchEnvSemitones = 8, PitchEnvMs = 35
        }),
        new("Bass", new SynthPatch
        {
            Wave = SynthWave.Square,
            AttackMs = 0, DecayMs = 160, Sustain = 0.82, ReleaseMs = 70,
            PitchEnvSemitones = 5, PitchEnvMs = 30
        }),
        new("Lead", new SynthPatch
        {
            Wave = SynthWave.Pulse, Duty = 0.5,
            AttackMs = 4, DecayMs = 70, Sustain = 0.55, ReleaseMs = 90,
            VibratoRateHz = 5, VibratoDepthCents = 18
        }),
        new("Pad", new SynthPatch
        {
            Wave = SynthWave.Saw,
            AttackMs = 220, DecayMs = 300, Sustain = 0.7, ReleaseMs = 450,
            VibratoRateHz = 3, VibratoDepthCents = 8
        })
    };
}
