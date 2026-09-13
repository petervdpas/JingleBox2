namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>
/// How a sound on Lighttower goes through its waves from the beginning to the end.
/// </summary>
/// <remarks>
/// These numbers are in people's files, so they do not move.
/// </remarks>
public enum SegmentMotion
{
    /// <summary>From the beginning to the end once, and then the end for as long as the note holds.</summary>
    Once = 0,

    /// <summary>From the beginning to the end, then straight back to the beginning, over and over.</summary>
    Loop = 1,

    /// <summary>From the beginning to the end and back again, over and over.</summary>
    Bounce = 2
}
