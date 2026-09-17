using System;
using System.Diagnostics;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class ChordWindow : IChordWindow
{
    /// <summary>
    /// Forty milliseconds, which is a hand's chord and nothing a hand plays on purpose.
    /// </summary>
    /// <remarks>
    /// A chord played on a keyboard is not one instant: the notes of it arrive over something
    /// like twenty to fifty milliseconds, and a player who leans on the melody note spreads it
    /// further on purpose. What has to stay on the other side of the number is deliberate
    /// separation, and the fastest of that anybody plays into a tracker is sixteenths at two
    /// hundred to the minute, which is 75 ms apart. Forty sits between the two.
    /// </remarks>
    public const double Seconds = 0.04;

    /// <inheritdoc/>
    /// <remarks>
    /// Held to half a line as well, since the window is only there to keep one chord together
    /// and a song whose lines are shorter than the window would have it gathering notes that
    /// really are a line apart. Measured either way round, because the two moments are stamped
    /// on whichever thread the key arrived on and the one that reaches the pattern first is not
    /// certainly the earlier.
    /// </remarks>
    public bool Together(long first, long second, long line)
    {
        long apart = Math.Abs(second - first);
        long window = (long)(Seconds * Stopwatch.Frequency);

        if (line > 0) window = Math.Min(window, line / 2);

        return apart <= window;
    }
}
