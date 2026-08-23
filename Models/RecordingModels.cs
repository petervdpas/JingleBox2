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

    /// <summary>
    /// The instruments that play this recording, as a phrase, or empty when nothing does. A
    /// sample instrument points at the file itself, so one that is spoken for cannot be
    /// deleted without silencing every song that uses it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInUse))]
    private string usedBy = "";

    public bool IsInUse => !string.IsNullOrEmpty(UsedBy);

    /// <summary>True while this recording is the one being auditioned from the list.</summary>
    [ObservableProperty] private bool isPlaying;

    /// <summary>
    /// What a picker shows when it is given one of these and told nothing else.
    /// </summary>
    /// <remarks>
    /// Every other thing in this app that lands in a list says its own name this way, and this
    /// was the one that did not, so a take picker offered four rows all reading
    /// "JingleBox2.Models.Recording".
    /// </remarks>
    public override string ToString() => Name;
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
