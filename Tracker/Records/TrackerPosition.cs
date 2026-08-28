using System;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>Where the player is: which entry in the order list, and which step inside it.</summary>
/// <remarks>
/// The order index rather than the pattern, because the same pattern can be in a song twice and
/// "which pattern" would then be an ambiguous answer to "where are we". What follows a slot is a
/// different question each time it is asked, and only the order knows it.
/// </remarks>
/// <param name="OrderIndex">Which entry of <see cref="Song.Order"/>, counting from zero.</param>
/// <param name="Line">Which step inside that entry's pattern, counting from zero.</param>
public readonly record struct TrackerPosition(int OrderIndex, int Line)
{
    /// <summary>The top of the song, which is where a stopped transport goes back to.</summary>
    public static readonly TrackerPosition Start = new(0, 0);

    /// <summary>Order and line, both two digits, as the status line shows it.</summary>
    public override string ToString() => $"{OrderIndex:00}:{Line:00}";
}
