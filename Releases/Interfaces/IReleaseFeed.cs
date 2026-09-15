using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Releases.Records;

namespace JingleBox2.Releases.Interfaces;

/// <summary>
/// Where the latest release of the application is found.
/// </summary>
/// <remarks>
/// A seam of its own because it is the half that goes out onto the network, and everything
/// decided about the answer can then be asked without one. Nothing here throws: no network, a
/// site that is down, an answer that is not what was expected and a wait that ran out are all
/// the same answer, which is that nothing is known, since a machine offline at startup is an
/// ordinary machine and not a fault worth a word.
/// </remarks>
public interface IReleaseFeed
{
    /// <summary>The newest published release, or null where that could not be found out.</summary>
    /// <param name="cancel">Stops the asking, answering null.</param>
    Task<Release?> Latest(CancellationToken cancel = default);
}
