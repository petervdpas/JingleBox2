using System;

namespace JingleBox2.Tracker;

/// <summary>How a sample repeats, if it does.</summary>
public enum SampleLoopMode
{
    /// <summary>Plays once and stops at the end of the window.</summary>
    None = 0,

    /// <summary>Jumps back to the loop start every time it reaches the loop end.</summary>
    Forward = 1,

    /// <summary>Turns round at each end of the loop instead of jumping.</summary>
    PingPong = 2
}

/// <summary>
/// Which part of a recording an instrument plays, and how. Everything is a fraction of the
/// file rather than a frame number, so trimming or re-recording the file leaves the settings
/// pointing at the same places in the sound rather than at stale offsets.
/// </summary>
public sealed class SampleShape
{
    public double Start { get; set; }

    public double End { get; set; } = 1;

    public SampleLoopMode LoopMode { get; set; } = SampleLoopMode.None;

    public double LoopStart { get; set; }

    public double LoopEnd { get; set; } = 1;

    /// <summary>Plays from the end of the window towards the start.</summary>
    public bool Reverse { get; set; }

    public bool IsLooping => LoopMode != SampleLoopMode.None;

    public SampleShape Clone() => new()
    {
        Start = Start,
        End = End,
        LoopMode = LoopMode,
        LoopStart = LoopStart,
        LoopEnd = LoopEnd,
        Reverse = Reverse
    };

    /// <summary>
    /// Puts every position back inside the file and in the right order. A window with nothing
    /// in it would be a silent instrument with no way to tell why, so an inverted one is
    /// straightened rather than kept.
    /// </summary>
    public void Clamp()
    {
        Start = Fraction(Start, 0);
        End = Fraction(End, 1);

        if (End < Start) (Start, End) = (End, Start);

        LoopStart = Math.Clamp(Fraction(LoopStart, 0), Start, End);
        LoopEnd = Math.Clamp(Fraction(LoopEnd, 1), Start, End);

        if (LoopEnd < LoopStart) (LoopStart, LoopEnd) = (LoopEnd, LoopStart);
    }

    private static double Fraction(double value, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp(value, 0, 1);
}
