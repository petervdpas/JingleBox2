// ===============================
// Config/AppConfig.cs
// ===============================
using System.Collections.Generic;

namespace JingleBox2.Config;

public sealed class PadConfig
{
    public string Name { get; set; } = "";
    public PadSourceKind Kind { get; set; } = PadSourceKind.None;

    // For File: absolute path
    // For StreamUrl: https://... or http://...
    public string Source { get; set; } = "";

    // 0.0 .. 1.0
    public double Volume { get; set; } = 1.0;
}

public sealed class ConfigProfile
{
    public string Name { get; set; } = "default";
    public List<PadConfig> Pads { get; set; } = new();
}

public sealed class AppConfig
{
    public int SelectedOutputDeviceId { get; set; } = -1;

    // NEW: profile support (single config.json holds multiple profiles)
    public string SelectedProfile { get; set; } = "default";
    public List<ConfigProfile> Profiles { get; set; } = new();

    // LEGACY: keep for backward compatibility (older config.json)
    // If present, will be migrated into Profiles["default"].
    public List<PadConfig> Pads { get; set; } = new();
}
