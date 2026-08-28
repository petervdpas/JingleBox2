namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Writes the recordings a song plays as names that survive the folder moving, and reads them
/// back as the paths this machine actually has.
/// </summary>
/// <remarks>
/// A song stores a full path for every recording an instrument plays, and almost all of them
/// are inside the application's own folder: a machine's presets, or a take off the shelf. That
/// folder is somewhere different on every machine and under a different name on every platform,
/// so a song saved here and opened anywhere else finds nothing, and so does a song opened after
/// the account was renamed. Nothing reports it. The instruments are simply silent.
///
/// So anything under the application folder is written as <c>{app}/</c> and what follows, with
/// forward slashes, and put back together on the way in. A path outside that folder is left
/// exactly as it was: it is somewhere the user chose, and guessing at it would be worse than
/// keeping it.
///
/// There is no ambiguity to guard against. A path that really begins <c>{app}/</c> is a
/// relative one, and every path a song holds is absolute.
///
/// Whether a path is under the application folder is a comparison of two names, which is a
/// question about the disc and not about the strings, so the rule is handed in rather than
/// read here: on Windows a folder spelled two ways is one folder, and a song whose paths were
/// left alone because the case did not match is a song that opens silent.
/// </remarks>
public interface ISongPaths
{
    /// <summary>A path as a song should hold it.</summary>
    /// <param name="path">The path this machine has. Nothing and the empty name both read as empty.</param>
    string Pack(string path);

    /// <summary>The path that name means on this machine.</summary>
    /// <param name="path">What the song holds, which may or may not begin with the token.</param>
    string Unpack(string path);

    /// <summary>Everything one instrument plays, written the portable way.</summary>
    /// <param name="instrument">The instrument to go over, in place.</param>
    void PackInto(TrackerInstrument instrument);

    /// <summary>Everything one instrument plays, read back as real paths.</summary>
    /// <param name="instrument">The instrument to go over, in place.</param>
    void UnpackInto(TrackerInstrument instrument);
}
