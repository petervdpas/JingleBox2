// ===============================
// Config/ConfigStore.cs
// ===============================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Midi;

namespace JingleBox2.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigStore(string appName = AppFolder.Name)
    {
        var dir = AppFolder.Path(appName);
        Directory.CreateDirectory(dir);

        ConfigPath = Path.Combine(dir, "config.json");
    }

    public AppConfig LoadOrCreateDefault()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                Normalize(cfg);
                return cfg;
            }
        }
        catch
        {
            // ignore and fall back to default
        }

        var fresh = new AppConfig();
        Normalize(fresh);
        Save(fresh);
        return fresh;
    }

    public void Save(AppConfig cfg)
    {
        Normalize(cfg);
        var json = JsonSerializer.Serialize(cfg, JsonOptions);
        SafeFile.Write(ConfigPath, json);
    }

    private static void Normalize(AppConfig cfg)
    {
        cfg.SelectedProfile = string.IsNullOrWhiteSpace(cfg.SelectedProfile) ? "default" : cfg.SelectedProfile.Trim();
        cfg.SelectedTheme = string.IsNullOrWhiteSpace(cfg.SelectedTheme) ? "Dark" : cfg.SelectedTheme.Trim();

        // Rows by columns, with the total kept between four pads and the ceiling. The ceiling
        // is the app's own, not the setting's: a config that says thirty-two pads keeps them
        // even if the extended switch has since been turned off, since dropping half somebody's
        // pads on the way in is not a thing a settings file should be able to do quietly. What
        // the switch governs is what the SETTINGS page will let you ask for next.
        cfg.Rows = Math.Clamp(cfg.Rows, 1, PadMatrix.Most);
        cfg.Columns = Math.Clamp(cfg.Columns, 1, PadMatrix.Most);

        if (cfg.Rows * cfg.Columns < PadMatrix.Least)
        {
            cfg.Rows = 2;
            cfg.Columns = 2;
        }

        if (cfg.Rows * cfg.Columns > PadMatrix.Most)
        {
            cfg.Rows = 4;
            cfg.Columns = 4;
        }

        int padCount = cfg.Rows * cfg.Columns;

        cfg.Profiles ??= new List<ConfigProfile>();
        cfg.Pads ??= new List<PadConfig>();

        // MIDI defaults / normalization (global, not per profile)
        cfg.Midi ??= new MidiConfig();
        cfg.Midi.Pads ??= new List<MidiMapping>();

        // Cleans the device roles and brings a single-device config file across.
        MidiDeviceBindings.Normalize(cfg.Midi);

        // Ensure exactly padCount mappings
        while (cfg.Midi.Pads.Count < padCount)
        {
            var i = cfg.Midi.Pads.Count;
            cfg.Midi.Pads.Add(new MidiMapping
            {
                PadIndex = i,
                Type = MidiMessageType.Note,
                Channel = 1,
                Value = 36 + i
            });
        }

        while (cfg.Midi.Pads.Count > padCount)
            cfg.Midi.Pads.RemoveAt(cfg.Midi.Pads.Count - 1);

        // Ensure indices consistent
        for (int i = 0; i < cfg.Midi.Pads.Count; i++)
            cfg.Midi.Pads[i].PadIndex = i;

        // MIGRATION: if Profiles empty but legacy Pads exists, migrate to default profile
        if (cfg.Profiles.Count == 0 && cfg.Pads.Count > 0)
        {
            cfg.Profiles.Add(new ConfigProfile
            {
                Name = "default",
                Pads = cfg.Pads.Select(ClonePad).ToList()
            });
        }

        // Ensure default profile exists
        if (!cfg.Profiles.Any(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase)))
        {
            cfg.Profiles.Add(new ConfigProfile
            {
                Name = "default",
                Pads = new List<PadConfig>()
            });
        }

        // Ensure selected profile exists; otherwise default
        if (!cfg.Profiles.Any(p => string.Equals(p.Name, cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            cfg.SelectedProfile = "default";

        // Normalize all profiles pad counts + fields
        foreach (var profile in cfg.Profiles)
        {
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "unnamed" : profile.Name.Trim();
            profile.Pads ??= new List<PadConfig>();

            while (profile.Pads.Count < padCount)
                profile.Pads.Add(new PadConfig { Name = $"Pad {profile.Pads.Count + 1}" });

            while (profile.Pads.Count > padCount)
                profile.Pads.RemoveAt(profile.Pads.Count - 1);

            for (int i = 0; i < profile.Pads.Count; i++)
            {
                var pad = profile.Pads[i] ??= new PadConfig();
                pad.Name = string.IsNullOrWhiteSpace(pad.Name) ? $"Pad {i + 1}" : pad.Name;
                pad.Volume = Math.Clamp(pad.Volume, 0.0, 1.0);
                pad.Source ??= "";
            }
        }

        // Keep legacy Pads in sync with selected profile (so old code can still work if anything uses it)
        var sel = GetSelectedProfile(cfg);
        if (sel != null)
            cfg.Pads = sel.Pads.Select(ClonePad).ToList();
        else
            cfg.Pads = CreateDefaultPads(padCount);
    }

    private static ConfigProfile? GetSelectedProfile(AppConfig cfg)
        => cfg.Profiles.FirstOrDefault(p => string.Equals(p.Name, cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase));

    private static List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
            pads.Add(new PadConfig { Name = $"Pad {i + 1}" });
        return pads;
    }

    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume
    };
}
