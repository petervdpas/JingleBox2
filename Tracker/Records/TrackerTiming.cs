using System;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// Converts tempo into time. Lines per beat is the tracker's resolution knob: at 4 LPB one
/// beat is four steps, so a 120 BPM pattern advances a step every 125 milliseconds.
/// </summary>
/// <param name="Bpm">Beats a minute, as the song holds it, which may be outside the range.</param>
/// <param name="LinesPerBeat">
/// Steps to a beat. Raising it does not change the tempo, it changes how finely the beat can be
/// written down, which is what a tracker means by resolution.
/// </param>
public readonly record struct TrackerTiming(double Bpm, int LinesPerBeat)
{
    /// <summary>Slower than this is not a tempo anybody plays to.</summary>
    public const double MinBpm = 20;

    /// <summary>And faster than this is a resolution knob rather than a tempo.</summary>
    public const double MaxBpm = 400;

    /// <summary>What a new song runs at.</summary>
    public const double DefaultBpm = 120;

    /// <summary>One step to a beat, which is as coarse as a pattern can be written.</summary>
    public const int MinLinesPerBeat = 1;

    /// <summary>Sixteen, which is finer than anything typed by hand needs.</summary>
    public const int MaxLinesPerBeat = 16;

    /// <summary>Four, which is what every tracker opens on and what a bar of sixteen lines means.</summary>
    public const int DefaultLinesPerBeat = 4;

    /// <summary>What a new song is given.</summary>
    public static readonly TrackerTiming Default = new(DefaultBpm, DefaultLinesPerBeat);

    /// <summary>
    /// The tempo actually used, held inside the range.
    /// </summary>
    /// <remarks>
    /// Clamped here rather than trusted, because the numbers come out of a file anybody can edit
    /// and a nought or a negative would put every derived length at infinity or below zero.
    /// </remarks>
    public double ClampedBpm => Math.Clamp(Bpm, MinBpm, MaxBpm);

    /// <summary>The resolution actually used, held inside the range, for the same reason.</summary>
    public int ClampedLinesPerBeat => Math.Clamp(LinesPerBeat, MinLinesPerBeat, MaxLinesPerBeat);

    /// <summary>How long one step lasts. Everything else here is worked out from this.</summary>
    public double SecondsPerLine => 60.0 / (ClampedBpm * ClampedLinesPerBeat);

    /// <summary>The same, for anything that wants it as a span rather than a number.</summary>
    public TimeSpan LineDuration => TimeSpan.FromSeconds(SecondsPerLine);

    /// <summary>Samples per step at a given rate. The player schedules on this, not on wall clock.</summary>
    public double SamplesPerLine(int sampleRate) => sampleRate * SecondsPerLine;

    /// <summary>How long that many steps last, for a pattern's or a song's length.</summary>
    public TimeSpan DurationOf(int lines) => TimeSpan.FromSeconds(SecondsPerLine * Math.Max(0, lines));

    /// <summary>Which step a point in time falls on, counting from zero.</summary>
    public int LineAt(TimeSpan elapsed) => (int)(elapsed.TotalSeconds / SecondsPerLine);
}
