using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Synth.Records;

/// <summary>
/// Where a voice reads from, in frames: the part of the file it plays and the part it repeats.
/// Worked out once when the note starts, since none of it moves while the note lasts.
/// </summary>
/// <param name="Start">The first frame of the part that sounds.</param>
/// <param name="End">The last frame of it.</param>
/// <param name="LoopStart">Where a repeat goes back to, held inside the window.</param>
/// <param name="LoopEnd">Where a repeat turns round or jumps, held inside the window.</param>
/// <param name="Loop">Whether it repeats at all, and which way round.</param>
/// <param name="Reverse">Whether the whole window is read backwards.</param>
public readonly record struct SampleWindow(
    double Start,
    double End,
    double LoopStart,
    double LoopEnd,
    SampleLoopMode Loop,
    bool Reverse)
{
    /// <summary>How much of the file one repeat covers, in frames.</summary>
    public double LoopLength => LoopEnd - LoopStart;

    /// <summary>A loop of no length is not a loop; it would be a division by zero and a stuck note.</summary>
    public bool IsLooping => Loop != SampleLoopMode.None && LoopLength > 0;

    /// <summary>Where a note starts reading: the far end when it plays backwards.</summary>
    public double Entry => Reverse ? End : Start;

    /// <summary>Which way the read head moves to begin with: forwards, or back from the end.</summary>
    public int Direction => Reverse ? -1 : 1;
}
