using System;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// How evenly the pattern really moved under the playhead, measured where it was drawn.
/// </summary>
/// <remarks>
/// **The clock being right says nothing about the picture being even.** The transport waits out
/// each step against a stopwatch and spins onto it, so a line is sent when it is due; what this
/// measures is the other end, which is when the drawing thread got round to it. What sits between
/// the two is a posted message waiting behind whatever else is in the queue, which is usually the
/// drawing thread being somewhere else entirely.
///
/// **It ends there and never reaches the screen**, which is the one thing to be exact about. The
/// moment is stamped when the job runs, and the picture is presented after that, so a step can
/// only move on a frame and this cannot see which one it landed on: at 120 to the minute on a
/// 60 Hz screen a line is seven and a half frames, the playhead steps after seven and then after
/// eight for ever, and this reads a fifth of a millisecond through the whole of it. That beat is
/// half a frame at any tempo and is the floor of what a stepping picture can do; what this is
/// for is everything above it.
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
