using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace JingleBox2.Models;

public sealed partial class Recording : ObservableObject
{
    public required string Id { get; set; }
    public required string FilePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Description { get; set; } = "";

    // Observable: trimming a recording changes its duration while the list is on screen.
    [ObservableProperty] private string name = "";
    [ObservableProperty] private long durationMs;
}

public class WaveformData
{
    public required float[] PeakData { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }

    /// <summary>Number of sample frames, so this is the length in samples per channel.</summary>
    public long TotalSamples { get; set; }
}

public class TrimRegion
{
    public long StartSample { get; set; }
    public long EndSample { get; set; }
}
