using System;
using System.Collections.Generic;
using System.IO;
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
        Normalize(cfg, cfg.Pads.Count == 0 ? 8 : cfg.Pads.Count);
        var json = JsonSerializer.Serialize(cfg, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static void Normalize(AppConfig cfg, int padCount)
    {
        cfg.Pads ??= new List<PadConfig>();
        while (cfg.Pads.Count < padCount) cfg.Pads.Add(new PadConfig { Name = $"Pad {cfg.Pads.Count + 1}" });
        while (cfg.Pads.Count > padCount) cfg.Pads.RemoveAt(cfg.Pads.Count - 1);

        for (int i = 0; i < cfg.Pads.Count; i++)
        {
            cfg.Pads[i].Name = string.IsNullOrWhiteSpace(cfg.Pads[i].Name) ? $"Pad {i + 1}" : cfg.Pads[i].Name;
            cfg.Pads[i].Volume = Math.Clamp(cfg.Pads[i].Volume, 0.0, 1.0);
            cfg.Pads[i].Source ??= "";
        }
    }
}
