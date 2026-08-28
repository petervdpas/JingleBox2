using System.Collections.Generic;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Where CLAP plugins live on this machine, and which of them are there.
/// </summary>
/// <remarks>
/// The same shape as <see cref="IVst3Scanner"/> and deliberately not folded into it. The two
/// standards put their plugins in different places under different rules, and a single scanner
/// taking a list of folders would have to be told those rules from outside, which is where they
/// would go stale.
/// </remarks>
public interface IClapScanner
{
    /// <summary>What a CLAP bundle is called: a shared library on Linux and Windows, a bundle
    /// directory on macOS, and the same four letters either way.</summary>
    string Extension { get; }

    /// <summary>
    /// Every directory this platform keeps plugins in, plus any the user has added, whether
    /// or not they exist.
    /// </summary>
    /// <remarks>
    /// The list is offered whole rather than filtered, since a folder that does not exist today
    /// is a folder a plugin can be installed into tomorrow. <c>CLAP_PATH</c> is read as well: the
    /// format says the environment may name more places to look, and some distributions rely on
    /// nothing else.
    /// </remarks>
    /// <param name="extra">
    /// Folders somebody has added in SETTINGS. They come first, because a person who names a
    /// folder means it, and a plugin found in two places should be the one they pointed at.
    /// </param>
    IReadOnlyList<string> SearchPaths(IEnumerable<string>? extra = null);

    /// <summary>Every .clap found on the search paths, sorted by name. Unreadable ones are skipped.</summary>
    /// <remarks>
    /// Followed all the way down, because vendors habitually keep their plugins in a folder of
    /// their own. A directory that cannot be read is one place with no plugins in it rather than
    /// a reason for the application to have no plugins at all, so it is stepped over in silence.
    /// </remarks>
    IReadOnlyList<string> Bundles(IEnumerable<string>? extra = null);
}
