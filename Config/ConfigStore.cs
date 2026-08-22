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
        File.WriteAllText(ConfigPath, json);
    }

    private static void Normalize(AppConfig cfg)
    {
        cfg.SelectedProfile = string.IsNullOrWhiteSpace(cfg.SelectedProfile) ? "default" : cfg.SelectedProfile.Trim();
        cfg.SelectedTheme = string.IsNullOrWhiteSpace(cfg.SelectedTheme) ? "Dark" : cfg.SelectedTheme.Trim();

        // Validate and clamp matrix dimensions
        // Min per dimension: 1, Min total: 4, Max total: 16
        cfg.Rows = Math.Clamp(cfg.Rows, 1, 16);
        cfg.Columns = Math.Clamp(cfg.Columns, 1, 16);

        // Ensure minimum 4 pads total
        if (cfg.Rows * cfg.Columns < 4)
        {
            cfg.Rows = 2;
            cfg.Columns = 2;
        }

        // Ensure maximum 16 pads total
        if (cfg.Rows * cfg.Columns > 16)
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
