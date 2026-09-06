using System.Collections.Generic;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>Which of the patchbay's cables are carrying audio right now.</summary>
/// <remarks>
/// A rule rather than a flag on the cable, because the answer changes many times a second and
/// the cables do not: what the graph reads is the wiring, and this is the traffic on it. The two
/// are kept apart for the reason the pattern's playing line is kept off the grid, which is that
/// a thing which changes constantly must not be a property of a thing that is dear to rebuild.
/// </remarks>
public interface IPatchFlow
{
    /// <summary>
    /// The cables that are carrying something, out of the ones that are drawn.
    /// </summary>
    /// <remarks>
    /// Answered by where each cable runs rather than by anything written on it, so a cable
    /// somebody patches this afternoon is live on the same terms as the ones that ship: what
    /// decides is which of this application's own blocks it touches.
    /// </remarks>
    /// <param name="links">Every cable on the picture.</param>
    /// <param name="signals">What is carrying audio at this moment.</param>
    IReadOnlyList<PatchLink> Live(IReadOnlyList<PatchLink> links, PatchSignals signals);
}
