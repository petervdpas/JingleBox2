using System.Collections.Generic;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Every recording the instruments play, decoded once and held.
/// </summary>
/// <remarks>
/// Handing the same decoded data to any number of voices is what gives a sample instrument its
/// polyphony: a voice owns a position in the file, never the file itself. It is also what makes
/// a chop cost nothing, since a chopped instrument's pieces all name the one recording and it is
/// decoded once for all of them.
///
/// Reading is bounded on purpose. An instrument is a jingle or a hit, not an album side, and a
/// voice reads from memory on the audio thread, so a file long enough to matter is refused and
/// reported rather than quietly turning the application into a disc cache.
///
/// Asked from the clock thread and from the drawing thread both, so everything here is safe to
/// call from either. What is refused is remembered as well as what worked: a broken instrument
/// is reported once rather than reopening the same missing file on every note.
/// </remarks>
public interface ISampleStore
{
    /// <summary>
    /// The paths that could not be used, so a broken instrument can be named after a take rather
    /// than being noticed as a track that went quiet.
    /// </summary>
    IReadOnlyCollection<string> FailedPaths { get; }

    /// <summary>
    /// Reads every instrument's file up front, so the first note is not late.
    /// </summary>
    /// <remarks>
    /// A kit is sixteen recordings rather than one, and a map up to thirty-two, so the first hit
    /// of each is what would stutter if they were left to be read as they were played. Whichever
    /// machine an instrument is on, everything it names is read.
    /// </remarks>
    void Preload(IEnumerable<TrackerInstrument> instruments);

    /// <summary>The decoded recording, or null when there is nothing usable at that path.</summary>
    /// <remarks>
    /// Not a WAV, half written, longer than the ceiling, or gone since the instrument was made:
    /// all the same answer, which the caller reports as an instrument that will not sound.
    /// </remarks>
    SampleData? Load(string filePath);

    /// <summary>Forgets a file so an edited or re-recorded one is picked up next time.</summary>
    /// <remarks>
    /// Forgets the failure as well as the audio. A recording that was missing when it was first
    /// asked for is exactly the one somebody has just put back.
    /// </remarks>
    void Invalidate(string filePath);

    /// <summary>Forgets everything, for a song being closed or the player being put down.</summary>
    void Clear();
}
