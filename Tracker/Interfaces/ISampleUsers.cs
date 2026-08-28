using System.Collections.Generic;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Several places that might be playing a recording, asked as one, and the same two questions
/// put to instruments directly.
/// </summary>
/// <remarks>
/// There are two places: the rack, which is the instruments you own, and the songs folder,
/// which is the instruments your songs own. They are genuinely separate, on purpose, because a
/// song keeps its own copies of what it plays. The recording underneath is the one file,
/// though, so the question RECORD asks before deleting one has to be put to both.
///
/// The per-instrument half used to be a static class of its own beside this one, and they were
/// one thing all along: a place answers <see cref="ISampleUsage.InstrumentsUsing"/> by walking
/// its instruments and asking each of them the very question <see cref="Uses"/> answers, and it
/// answers <see cref="ISampleUsage.Repoint(string, string)"/> by walking them and calling
/// <see cref="Repoint(TrackerInstrument, string, string)"/> on each. Split in two, the rule for
/// what counts as playing a file lived on one side and every caller of the other had to know to
/// go and find it. Together, there is one answer to "does this instrument play this file", and
/// asking a place is asking that of everything the place holds.
///
/// Which is also why the comparison rule is handed in. A kit built on a recording spelled with
/// a different case is still built on that recording, and on Windows deciding otherwise is how
/// a file gets deleted out from under a song with nothing said.
///
/// This is the wider contract; <see cref="ISampleUsage"/> stays the narrow one, because a rack
/// and a songs folder are places to ask and have no business answering questions about an
/// instrument somebody is holding.
/// </remarks>
public interface ISampleUsers : ISampleUsage
{
    /// <summary>
    /// True when the instrument plays this file. Paths are compared as the file system would:
    /// the same file reached two ways is still the same file.
    /// </summary>
    /// <param name="instrument">The instrument to ask. Nothing plays nothing.</param>
    /// <param name="filePath">The recording in question.</param>
    bool Uses(TrackerInstrument? instrument, string? filePath);

    /// <summary>
    /// Every recording an instrument plays, whichever machine it is.
    /// </summary>
    /// <remarks>
    /// A kit plays sixteen and a map up to thirty-two, and asking only the one an instrument
    /// keeps at the top would say a recording is free when a drum kit is built on it. Which is
    /// how a file gets deleted out from under a song.
    /// </remarks>
    /// <param name="instrument">The instrument to go over. Nothing plays nothing.</param>
    IEnumerable<string> Files(TrackerInstrument? instrument);

    /// <summary>
    /// Points an instrument at a recording's new place, wherever it was playing the old one.
    /// True when something moved.
    /// </summary>
    /// <remarks>
    /// A recording's name is its file name, so renaming one moves it and every instrument
    /// holding the old path goes silent. This is what stops that: the rename and the repointing
    /// are one action, and an instrument never sees the moment in between.
    /// </remarks>
    /// <param name="instrument">The instrument to move, in place.</param>
    /// <param name="from">Where the recording was.</param>
    /// <param name="to">Where it is now.</param>
    bool Repoint(TrackerInstrument? instrument, string? from, string? to);

    /// <summary>The names of the instruments that play this file, in the order given.</summary>
    /// <param name="instruments">The instruments to walk. Nothing gives an empty list.</param>
    /// <param name="filePath">The recording in question.</param>
    IReadOnlyList<string> By(IEnumerable<TrackerInstrument>? instruments, string? filePath);

    /// <summary>
    /// The same list as a phrase to put in a sentence. A long list is cut short: the point is
    /// that the recording is spoken for, not to read out the whole library.
    /// </summary>
    /// <param name="names">What came back from <see cref="By"/>, or from a place.</param>
    string Describe(IReadOnlyList<string>? names);
}
