using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Releases.Records;

namespace JingleBox2.Releases.Interfaces;

/// <summary>
/// Whether there is a newer release than the one running, and what to say about it.
/// </summary>
/// <remarks>
/// Asked once, as the application starts, and said as a toast because it is news somebody
/// would otherwise only find by going to look.
///
/// Three answers. A newer release is said, with the version running beside it. The same or an
/// older one says nothing, since being up to date is the ordinary case and a toast on every
/// start saying so is a toast nobody reads the day it matters. And a build with no release
/// version at all, which is what a build from a checkout is, says which release is the latest:
/// it cannot be behind or ahead of anything, and saying nothing there would make the check
/// impossible to see working on the one machine where it is written.
///
/// Versions are compared as numbers and not as words, so 2.10 is after 2.9. A leading v, a
/// pre-release label after a hyphen and build metadata after a plus are all read past.
/// </remarks>
public interface IReleaseCheck
{
    /// <summary>What to say, or null where there is nothing worth saying.</summary>
    /// <param name="running">The version this build carries, as its assembly says it.</param>
    /// <param name="cancel">Stops the asking, answering null.</param>
    Task<ReleaseNews?> Check(string running, CancellationToken cancel = default);

    /// <summary>What to say about one release against the version running, with no network in it.</summary>
    /// <param name="running">The version this build carries.</param>
    /// <param name="latest">The newest release there is.</param>
    ReleaseNews? Compare(string running, Release latest);
}
