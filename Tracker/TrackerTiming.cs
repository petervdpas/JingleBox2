using System;

namespace JingleBox2.Tracker;

/// <summary>
/// Converts tempo into time. Lines per beat is the tracker's resolution knob: at 4 LPB one
/// beat is four steps, so a 120 BPM pattern advances a step every 125 milliseconds.
/// </summary>
public readonly record struct TrackerTiming(double Bpm, int LinesPerBeat)
{
    public const double MinBpm = 20;
    public const double MaxBpm = 400;
    public const double DefaultBpm = 120;

    public const int MinLinesPerBeat = 1;
    public const int MaxLinesPerBeat = 16;
    public const int DefaultLinesPerBeat = 4;

    public static readonly TrackerTiming Default = new(DefaultBpm, DefaultLinesPerBeat);

    public double ClampedBpm => Math.Clamp(Bpm, MinBpm, MaxBpm);
    public int ClampedLinesPerBeat => Math.Clamp(LinesPerBeat, MinLinesPerBeat, MaxLinesPerBeat);

    public double SecondsPerLine => 60.0 / (ClampedBpm * ClampedLinesPerBeat);

    public TimeSpan LineDuration => TimeSpan.FromSeconds(SecondsPerLine);

    /// <summary>Samples per step at a given rate. The player schedules on this, not on wall clock.</summary>
    public double SamplesPerLine(int sampleRate) => sampleRate * SecondsPerLine;

    public TimeSpan DurationOf(int lines) => TimeSpan.FromSeconds(SecondsPerLine * Math.Max(0, lines));

    /// <summary>Which step a point in time falls on, counting from zero.</summary>
    public int LineAt(TimeSpan elapsed) => (int)(elapsed.TotalSeconds / SecondsPerLine);
}
