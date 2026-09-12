using System;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// How evenly the pattern really moved under the playhead, measured where it was drawn.
/// </summary>
/// <remarks>
/// **The clock being right says nothing about the picture being even.** The transport waits out
/// each step against a stopwatch and spins onto it, so a line is sent when it is due; what this
/// measures is the other end, which is when the drawing thread got round to it. Everything
/// between the two is the toolkit's: a posted message waiting behind whatever else is in the
/// queue, a frame that has to wait for the screen, and on some systems a timer that is only
/// delivered when nothing else is pending.
///
/// So what it answers is the one question a bumpy pattern asks, which is whether the steps are
/// arriving evenly. Even steps at the wrong rate is a tempo; uneven steps at the right mean rate
/// is the thing being looked for, and the spread is what says so. It cannot be reasoned out from
/// here at all, because it is a fact about the machine it is running on.
///
/// The moment is handed in rather than read off a clock, which is what lets a five second window
/// be asked about without waiting five seconds.
/// </remarks>
public interface IPlayheadFlow
{
    /// <summary>
    /// A line reached the drawing thread, and a sentence back means a window closed on it.
    /// </summary>
    /// <remarks>
    /// The gap before the first step of a run is not a gap, so nothing is counted until there are
    /// two: a window is about how evenly the steps came, and a run has to have started.
    /// </remarks>
    /// <param name="at">How long this page has been up, which only has to agree with itself.</param>
    /// <returns>What to write down, or nothing while the window is still open.</returns>
    string? Stepped(TimeSpan at);

    /// <summary>
    /// The transport stopped, so what has been gathered is said and the gap across the stop is
    /// dropped.
    /// </summary>
    /// <remarks>
    /// Said rather than thrown away, since a run shorter than a window would otherwise report
    /// nothing at all, and a short run is what somebody trying one tempo does. The gap goes
    /// because the silence between two runs is not a step that was late.
    /// </remarks>
    /// <returns>What to write down, or nothing where there was nothing to say.</returns>
    string? Stopped();
}
