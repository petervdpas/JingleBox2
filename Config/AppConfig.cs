using System.Collections.Generic;

namespace JingleBox2.Config;

public enum PadSourceKind
{
    None = 0,
    File = 1,
    StreamUrl = 2
}

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

public sealed class AppConfig
{
    public int SelectedOutputDeviceId { get; set; } = -1;

    // fixed 8 pads for now
    public List<PadConfig> Pads { get; set; } = new();
}
