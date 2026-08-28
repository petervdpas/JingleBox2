using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// Somewhere instruments are kept, asked what a recording is the sound of.
/// </summary>
/// <remarks>
/// A sample instrument owns no copy of its file: it is a path and a base note, so removing the
/// recording leaves the instrument silent in every song that plays it and nothing is said. This
/// is the question RECORD puts before it deletes or renames a take, and it is an interface so
/// that page can put it without knowing where instruments live.
///
/// There are two places to ask, and they are separate on purpose. The rack is the instruments
/// you own; the songs folder is the instruments your songs own, since a song keeps its own
/// copies of what it plays. The recording underneath is one file, so both have to be asked, and
/// <see cref="SampleUsers"/> is that pair asked as one.
///
/// Neither answer is cheap and neither is asked on a hot path: a name is being typed or a
/// confirmation is being put up.
/// </remarks>
public interface ISampleUsage
{
    /// <summary>The names of the instruments that play this file. Empty when it is free.</summary>
    IReadOnlyList<string> InstrumentsUsing(string filePath);

    /// <summary>
    /// Points every instrument playing <paramref name="from"/> at <paramref name="to"/> instead,
    /// and says how many moved. For a recording that has been renamed, which is a recording that
    /// has been moved.
    /// </summary>
    /// <remarks>
    /// A recording's name is its file name, so renaming one moves it, and every instrument
    /// holding the old path goes silent. The rename and the repointing are meant to be one
    /// action, so that an instrument never sees the moment in between.
    /// </remarks>
    int Repoint(string from, string to);
}
