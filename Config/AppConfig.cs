// ===============================
// Config/AppConfig.cs
// ===============================
using System.Collections.Generic;
using JingleBox2.Midi;

namespace JingleBox2.Config;

public sealed class PadConfig
{
    public string Name { get; set; } = "";
    public PadSourceKind Kind { get; set; } = PadSourceKind.None;
    public string Source { get; set; } = "";
    public double Volume { get; set; } = 1.0;

    public bool Loop { get; set; } = false;
    public double FadeIn { get; set; } = 0;
    public double FadeOut { get; set; } = 0;
    public string Color { get; set; } = "";

    // The effects on this pad. Null for a pad with none, so a profile is not full of empty
    // chains.
    public Audio.Plugins.PluginChainConfig? Plugins { get; set; }
}

public sealed class ConfigProfile
{
    public string Name { get; set; } = "default";
    public List<PadConfig> Pads { get; set; } = new();
}

public sealed class AppConfig
{
    public int SelectedOutputDeviceId { get; set; } = -1;
    public string SelectedProfile { get; set; } = "default";
    public List<ConfigProfile> Profiles { get; set; } = new();
    public string SelectedTheme { get; set; } = "Dark";
    public int Rows { get; set; } = 4;
    public int Columns { get; set; } = 2;
    public MidiConfig Midi { get; set; } = new();
    public double RecordGainDb { get; set; } = 0;

    // A velocity sensitive keyboard writes a different level for every hit. Some parts want
    // that; a kick almost never does.
    public bool IgnoreKeyVelocity { get; set; }

    // Extra places to look for plugins, on top of the ones the format specifies. Someone who
    // keeps their plugins somewhere of their own says so here.
    public List<string> PluginFolders { get; set; } = new();

    // What the tracker and synth run at. Zero means whatever the output device is running at,
    // which is what stops everything being resampled on the way out.
    public int EngineSampleRate { get; set; }

    // Stored by name, not index: device indexes shift when hardware is plugged in or out.
    public string RecordInputDevice { get; set; } = "";

    // 0 means never resized, so fall back to sizing from the pad matrix.
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public List<PadConfig> Pads { get; set; } = new();
}
