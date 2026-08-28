using System;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker;

/// <summary>
/// Which part of a recording an instrument plays, and how. Everything is a fraction of the
/// file rather than a frame number, so trimming or re-recording the file leaves the settings
/// pointing at the same places in the sound rather than at stale offsets.
/// </summary>
public sealed class SampleShape
{
    /// <summary>Where the sound begins, as a fraction of the whole file.</summary>
    /// <remarks>
    /// A chopped instrument is a set of pieces that differ only in this and in
    /// <see cref="End"/>, all pointing at the one recording. That is what makes chopping cost
    /// no storage: the cuts are these windows and there is no second copy of them anywhere.
    /// </remarks>
    public double Start { get; set; }

    /// <summary>And where it ends. One is the end of the file.</summary>
    public double End { get; set; } = 1;

    /// <summary>Whether it repeats, and which way round.</summary>
    public SampleLoopMode LoopMode { get; set; } = SampleLoopMode.None;

    /// <summary>Where the repeat goes back to, inside the window rather than the file.</summary>
    public double LoopStart { get; set; }

    /// <summary>And where it turns round or jumps from.</summary>
    public double LoopEnd { get; set; } = 1;

    /// <summary>Plays from the end of the window towards the start.</summary>
    public bool Reverse { get; set; }

    /// <summary>True when it repeats at all, whichever of the two ways it does it.</summary>
    public bool IsLooping => LoopMode != SampleLoopMode.None;

    /// <summary>
    /// A copy that can be edited without the original hearing about it, for a preset landing on
    /// a piece somebody is already holding.
    /// </summary>
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

    /// <summary>
    /// One position, put back inside the file. A NaN falls to what that position would be on a
    /// shape nobody had touched, since a NaN dragged through the arithmetic afterwards produces
    /// a window with nothing in it and no way to say which reading was the bad one.
    /// </summary>
    private static double Fraction(double value, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Clamp(value, 0, 1);
}
