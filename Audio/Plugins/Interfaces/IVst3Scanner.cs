using System.Collections.Generic;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Where VST3 plugins live on this machine, and which of them are there.
/// </summary>
/// <remarks>
/// The folders are the ones the standard names, per platform, and they are looked in rather
/// than asked about: there is no register to consult and no service to ask, so a plugin exists
/// because a bundle is sitting in one of these places.
///
/// Finding a bundle is not loading one. Nothing here opens a plugin or runs any of its code,
/// which is the whole point of the split: a scan that crashed on a bad plugin would take the
/// list of good ones with it.
/// </remarks>
public interface IVst3Scanner
{
    /// <summary>What a VST3 bundle is called. A directory rather than a file, on every platform.</summary>
    string Extension { get; }

    /// <summary>
    /// Every directory this platform keeps plugins in, plus any the user has added, whether
    /// or not they exist.
    /// </summary>
    /// <remarks>
    /// Offered whole rather than filtered, since a folder that does not exist today is a folder
    /// a plugin can be installed into tomorrow. <c>VST3_PATH</c> is read as well, the same way
    /// the CLAP scanner reads <c>CLAP_PATH</c>.
    /// </remarks>
    /// <param name="extra">
    /// Folders somebody has added in SETTINGS. They come first, because a person who names a
    /// folder means it.
    /// </param>
    IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null);

    /// <summary>Every .vst3 found on the search paths, sorted by name.</summary>
    /// <remarks>
    /// A folder that cannot be read is skipped rather than reported. A plugins directory
    /// somebody has no permission on is a thing to walk past, not a thing to stop the scan
    /// over: the other forty plugins are still there.
    /// </remarks>
    IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null);
}
