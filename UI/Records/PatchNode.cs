using System.Collections.Generic;

namespace JingleBox2.UI.Records;

/// <summary>
/// One block on the patchbay: what it is called, what it takes in, what it gives out, and where
/// it starts on the surface.
/// </summary>
/// <remarks>
/// The place is where a block appears the first time it is seen and nothing more: once somebody
/// has moved it, where it sits belongs to the surface, which remembers it by id. Otherwise a
/// list read again two seconds later would throw every block back to where the list says, which
/// is a picture that undoes what a hand just did.
/// </remarks>
/// <param name="Id">What this block is, which is what a port names and what a place is remembered by.</param>
/// <param name="Title">What is written on it.</param>
/// <param name="Ins">What it takes in, down the left, in the order given.</param>
/// <param name="Outs">What it gives out, down the right.</param>
/// <param name="IsOurs">Whether this block is the application itself rather than something outside it.</param>
/// <param name="X">Where it starts, across.</param>
/// <param name="Y">Where it starts, down.</param>
public sealed record PatchNode(
    string Id,
    string Title,
    IReadOnlyList<PatchPort> Ins,
    IReadOnlyList<PatchPort> Outs,
    bool IsOurs,
    double X,
    double Y)
{
    /// <summary>Whether this block takes anything in, so a sidebar knows to say so.</summary>
    /// <remarks>
    /// Here rather than as a count compared in a binding, since a heading over an empty list is
    /// a section that says a block has connections it has not got.
    /// </remarks>
    public bool HasIns => Ins.Count > 0;

    /// <inheritdoc cref="HasIns"/>
    public bool HasOuts => Outs.Count > 0;
}
