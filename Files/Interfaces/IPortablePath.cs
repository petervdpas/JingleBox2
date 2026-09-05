namespace JingleBox2.Files.Interfaces;

/// <summary>
/// Writes a path under the application folder as a name that survives the folder moving, and
/// reads it back as the path this machine actually has.
/// </summary>
/// <remarks>
/// Almost everything this application stores a path to is inside its own folder: a take off the
/// shelf, a recording a device's preset names, the file behind a pad. That folder is somewhere
/// different on every machine and under a different name on every platform, so a full path
/// written here means nothing anywhere else, and nothing means nothing after the account is
/// renamed. Nothing reports it either: the pad is simply silent and the device plays nothing.
///
/// So anything under the application folder is written as <c>{app}/</c> and what follows, with
/// forward slashes, and put back together on the way in. A path outside that folder is left
/// exactly as it was: it is somewhere the user chose, or somebody else's plugin, and guessing at
/// it would be worse than keeping it.
///
/// There is no ambiguity to guard against. A path that really begins <c>{app}/</c> is a relative
/// one, and every path stored here is absolute.
///
/// Whether a path is under the application folder is a comparison of two names, which is a
/// question about the disc and not about the strings, so the rule is handed in rather than read
/// here: on Windows a folder spelled two ways is one folder, and a file left alone because the
/// case did not match is a file nothing can find.
///
/// It is in <c>Files/</c> and not beside whatever first needed it, because it is one of the
/// questions about a file that are about this machine rather than about this program, and
/// because everything that stores a path asks it: the songs, the rack and the settings. It knows
/// nothing about songs, devices or pads, and must not, or the thing that keeps them all honest
/// would depend on all three.
/// </remarks>
public interface IPortablePath
{
    /// <summary>A path as it should be stored.</summary>
    /// <param name="path">The path this machine has. Nothing and the empty name both read as empty.</param>
    string Pack(string path);

    /// <summary>The path that name means on this machine.</summary>
    /// <param name="path">What was stored, which may or may not begin with the token.</param>
    string Unpack(string path);
}
