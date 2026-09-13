using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// One drum cut out of a recording and written to a file of its own.
/// </summary>
/// <param name="FilePath">Where the file is.</param>
/// <param name="Name">What its pad is called: the drum it is, numbered where the kit has two of it.</param>
/// <param name="Sound">What it was heard as.</param>
public sealed record ChoppedDrum(string FilePath, string Name, DrumSound Sound);
