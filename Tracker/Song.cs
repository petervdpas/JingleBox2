using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// Patterns, the order they play in, and the instruments they reference.
/// The order list holds pattern indexes, so the same pattern can appear more than once.
/// </summary>
public sealed class Song
{
    public const int MinTrackCount = 1;
    public const int MaxTrackCount = 16;
    public const int DefaultTrackCount = 4;

    public string Name { get; set; } = "untitled";

    public double Bpm { get; set; } = TrackerTiming.DefaultBpm;
    public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;
    public int TrackCount { get; set; } = DefaultTrackCount;

    public List<Pattern> Patterns { get; set; } = new();

    /// <summary>Indexes into <see cref="Patterns"/>, in playing order.</summary>
    public List<int> Order { get; set; } = new();

    public List<TrackerInstrument> Instruments { get; set; } = new();

    [JsonIgnore]
    public TrackerTiming Timing => new(Bpm, LinesPerBeat);

    public static Song CreateDefault()
    {
        var song = new Song();
        song.Patterns.Add(new Pattern(Pattern.DefaultLines, song.TrackCount) { Name = "01" });
        song.Order.Add(0);
        return song;
    }

    public Pattern? PatternAt(int orderIndex)
    {
        if (orderIndex < 0 || orderIndex >= Order.Count) return null;

        int patternIndex = Order[orderIndex];
        return patternIndex >= 0 && patternIndex < Patterns.Count ? Patterns[patternIndex] : null;
    }

    public TrackerInstrument? InstrumentAt(int index) =>
        index >= 0 && index < Instruments.Count ? Instruments[index] : null;

    /// <summary>Adds a pattern sized to match the song and returns its index.</summary>
    public int AddPattern(int lines = Pattern.DefaultLines)
    {
        var pattern = new Pattern(lines, TrackCount)
        {
            Name = (Patterns.Count + 1).ToString("00")
        };
        Patterns.Add(pattern);
        return Patterns.Count - 1;
    }

    /// <summary>Applies a new track count to the song and every pattern in it.</summary>
    public void SetTrackCount(int trackCount)
    {
        TrackCount = Math.Clamp(trackCount, MinTrackCount, MaxTrackCount);
        foreach (var pattern in Patterns)
            pattern.SetTrackCount(TrackCount);
    }

    public TimeSpan Duration =>
        TimeSpan.FromSeconds(Timing.SecondsPerLine *
            Enumerable.Range(0, Order.Count).Sum(i => PatternAt(i)?.Lines ?? 0));

    /// <summary>
    /// Brings a loaded song back to a state the player can trust: sane tempo, patterns all
    /// the same width, and no order entry pointing at a pattern that is not there.
    /// </summary>
    public void Normalize()
    {
        Bpm = Math.Clamp(Bpm, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
        LinesPerBeat = Math.Clamp(LinesPerBeat, TrackerTiming.MinLinesPerBeat, TrackerTiming.MaxLinesPerBeat);
        TrackCount = Math.Clamp(TrackCount, MinTrackCount, MaxTrackCount);

        if (Patterns.Count == 0)
            Patterns.Add(new Pattern(Pattern.DefaultLines, TrackCount) { Name = "01" });

        foreach (var pattern in Patterns)
            pattern.SetTrackCount(TrackCount);

        Order.RemoveAll(index => index < 0 || index >= Patterns.Count);
        if (Order.Count == 0)
            Order.Add(0);
    }
}
