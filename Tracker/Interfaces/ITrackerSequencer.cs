using System.Collections.Generic;
using JingleBox2.Tracker;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Reads a song step by step and says what should happen, without touching audio.
/// </summary>
/// <remarks>
/// The split is deliberate: what a line means is arithmetic over the pattern and can be put a
/// question to without a device, a window or a thread, and the tests do exactly that. What it
/// sounds like is <see cref="TrackerPlayer"/>'s business.
///
/// It holds the per-track memory a tracker needs, which is why it is a seam rather than a
/// function. A note with a blank instrument column plays whatever that track played last, and a
/// volume column stays where it was set until something moves it, so what a line means depends
/// on the lines before it. That is what keeps a pattern readable: you write the instrument once
/// at the top and the column stays empty down the page.
/// </remarks>
public interface ITrackerSequencer
{
    /// <summary>How many tracks it is keeping memory for, which is the song's count.</summary>
    int TrackCount { get; }

    /// <summary>
    /// Forgets the per-track memory, so the next line starts from nothing rather than from
    /// wherever the last pass left off.
    /// </summary>
    /// <remarks>
    /// Called whenever playback restarts. Without it, starting a song from the middle would
    /// carry the instrument and the level of a pass that is over, and a pattern played twice
    /// would not sound the same the second time.
    /// </remarks>
    void Reset();

    /// <summary>
    /// What to do on this step.
    /// </summary>
    /// <remarks>
    /// Only tracks with something to say produce an event, so a mostly empty pattern costs
    /// almost nothing to play. An empty list is the ordinary answer and not a failure: it is
    /// what a line nobody has typed on looks like, and it is also what a position past the end
    /// of a pattern gives back.
    /// </remarks>
    IReadOnlyList<TrackerEvent> EventsFor(Song song, TrackerPosition position);
}
