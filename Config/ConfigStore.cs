// ===============================
// Config/ConfigStore.cs
// ===============================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JingleBox2.Config;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigPath { get; }

    public ConfigStore(string appName = "JingleBox2")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, appName);
        Directory.CreateDirectory(dir);

        ConfigPath = Path.Combine(dir, "config.json");
    }

    public AppConfig LoadOrCreateDefault(int padCount = 8)
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                Normalize(cfg, padCount);
                return cfg;
            }
        }
        catch
        {
            // ignore and fall back to default
        }

        var fresh = new AppConfig();
        Normalize(fresh, padCount);
        Save(fresh);
        return fresh;
    }

    public void Save(AppConfig cfg)
    {
        Normalize(cfg, padCount: GuessPadCount(cfg));
        var json = JsonSerializer.Serialize(cfg, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static int GuessPadCount(AppConfig cfg)
    {
        if (cfg.Profiles != null && cfg.Profiles.Count > 0)
        {
            var p = GetSelectedProfile(cfg) ?? cfg.Profiles[0];
            if (p.Pads != null && p.Pads.Count > 0) return p.Pads.Count;
        }

        if (cfg.Pads != null && cfg.Pads.Count > 0) return cfg.Pads.Count;
        return 8;
    }

    private static void Normalize(AppConfig cfg, int padCount)
    {
        cfg.SelectedProfile = string.IsNullOrWhiteSpace(cfg.SelectedProfile) ? "default" : cfg.SelectedProfile.Trim();
        cfg.Profiles ??= new List<ConfigProfile>();
        cfg.Pads ??= new List<PadConfig>();

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
