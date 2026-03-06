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
    public List<PadConfig> Pads { get; set; } = new();
}
