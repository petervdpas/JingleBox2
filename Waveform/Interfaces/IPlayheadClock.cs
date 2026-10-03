using System;

namespace JingleBox2.Waveform.Interfaces;

/// <summary>
/// What tells a take player to read where its take has got to, on the thread that draws.
/// </summary>
/// <remarks>
/// A seam so the reading can be driven by hand: the cursor moving is a sequence of readings, and
/// a test that can take them one at a time can say what the cursor does after each, which a
/// real timer only ever answers by waiting. One clock per player, started when a take starts
/// and stopped when it stops; starting it again replaces what it was doing.
/// </remarks>
public interface IPlayheadClock
{
    /// <summary>Calls the reading every so often until stopped, replacing any reading already running.</summary>
    /// <param name="every">How often.</param>
    /// <param name="tick">The reading.</param>
    void Start(TimeSpan every, Action tick);

    /// <summary>Stops calling the reading. Does nothing when nothing is running.</summary>
    void Stop();
}
