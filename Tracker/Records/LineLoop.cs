namespace JingleBox2.Tracker.Records;

/// <summary>A stretch of a pattern's lines that goes round a number of times on each pass.</summary>
/// <remarks>
/// Lines across every track: the pattern plays a whole row at a time, so a stretch is the same
/// stretch whichever track it was selected on, and two stretches on one slot may not share a line.
/// </remarks>
/// <param name="From">The first line.</param>
/// <param name="To">The last line, the one the song goes back from.</param>
/// <param name="Times">How many times the stretch plays on each pass.</param>
public sealed record LineLoop(int From, int To, int Times)
{
    /// <summary>Whether the two share any line.</summary>
    /// <param name="other">The other stretch.</param>
    public bool Overlaps(LineLoop other) => From <= other.To && other.From <= To;

    /// <summary>Whether this covers exactly the same lines as the other.</summary>
    /// <param name="other">The other stretch.</param>
    public bool SameLines(LineLoop other) => From == other.From && To == other.To;
}
