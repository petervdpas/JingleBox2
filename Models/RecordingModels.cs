using System;

namespace JingleBox2.Models;

public class Recording
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string FilePath { get; set; }
    public long DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Description { get; set; } = "";
}

public class WaveformData
{
    public required float[] PeakData { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public long TotalSamples { get; set; }
}

public class TrimRegion
{
    public long StartSample { get; set; }
    public long EndSample { get; set; }
}
