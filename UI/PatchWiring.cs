using System.Collections.Generic;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchWiring : IPatchWiring
{
    /// <inheritdoc/>
    public bool Allowed(PatchPort from, PatchPort to)
    {
        if (from.Side == to.Side) return false;
        if (string.IsNullOrEmpty(from.Node) || string.IsNullOrEmpty(to.Node)) return false;

        return !string.Equals(from.Node, to.Node, System.StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Worked out from the two counts rather than from a table of the four cases, so a shape
    /// added later is arithmetic rather than three more entries: every channel of the wider side
    /// is fed, and the narrower side is read round.
    /// </remarks>
    public IReadOnlyList<(int From, int To)> Pairs(PatchChannels from, PatchChannels to)
    {
        int outs = (int)from;
        int ins = (int)to;
        int wires = outs > ins ? outs : ins;

        var pairs = new List<(int From, int To)>(wires);

        for (int wire = 0; wire < wires; wire++) pairs.Add((wire % outs, wire % ins));

        return pairs;
    }
}
