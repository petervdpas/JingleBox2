using System.Collections.Generic;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchWiring : IPatchWiring
{
    /// <inheritdoc/>
    /// <remarks>
    /// The two ends are sorted out first, since a hand drags a cable in whichever direction it
    /// likes and everything after that is about which end gives and which takes.
    /// </remarks>
    public bool Allowed(PatchPort from, PatchPort to)
    {
        if (from.Side == to.Side) return false;
        if (string.IsNullOrEmpty(from.Node) || string.IsNullOrEmpty(to.Node)) return false;
        if (string.Equals(from.Node, to.Node, System.StringComparison.Ordinal)) return false;

        var gives = from.Side == PatchSide.Out ? from : to;
        var takes = from.Side == PatchSide.Out ? to : from;

        if (gives.Fixed || takes.Fixed) return Absorbed(gives, takes);

        return true;
    }

    /// <summary>
    /// Whether this is a source of ours being patched into the recorder's input.
    /// </summary>
    /// <remarks>
    /// **The one cable between our own blocks that somebody may draw.** What the song sums to and
    /// what the pads sum to are things this application is playing, and whether they are also
    /// being recorded is a decision rather than a fact about the engine, which is exactly what a
    /// patchbay is for: a jingle over the song, the song bounced down, the pads taken as one take.
    ///
    /// The desk's own master is deliberately not on the list. It is everything summed, the input
    /// among it, so patching it back into the input is the input arriving into itself: it is the
    /// one connection here that can howl rather than mix, and it wants an answer of its own
    /// before it is offered.
    ///
    /// Written out one node per line rather than worked out from what a block is, since which of
    /// our blocks may feed the input is a decision somebody made and not a property anything has.
    /// </remarks>
    /// <param name="gives">The end audio leaves by.</param>
    /// <param name="takes">The end it arrives at.</param>
    private static bool Absorbed(PatchPort gives, PatchPort takes)
    {
        if (!Feeds(gives)) return false;

        if (string.Equals(takes.Node, PatchNodes.Record, System.StringComparison.Ordinal))
            return string.Equals(takes.Name, PatchPorts.Capture, System.StringComparison.Ordinal);

        if (!string.Equals(takes.Node, PatchNodes.Mixer, System.StringComparison.Ordinal)) return false;

        return string.Equals(takes.Name, gives.Name, System.StringComparison.Ordinal);
    }

    /// <summary>Whether that point is one of ours that may go to the recorder.</summary>
    /// <remarks>
    /// The list itself, asked by both questions this class answers: whether a cable may run
    /// between two points, and whether a hand may take hold of one at all. Two spellings of it
    /// would eventually disagree, and the way that fails is a point you can pick a cable up from
    /// and never put down.
    /// </remarks>
    /// <param name="gives">The point audio would leave by.</param>
    private static bool Feeds(PatchPort gives) =>
        gives.Side == PatchSide.Out
        && (string.Equals(gives.Node, PatchNodes.Song, System.StringComparison.Ordinal)
            || string.Equals(gives.Node, PatchNodes.Fire, System.StringComparison.Ordinal));

    /// <inheritdoc/>
    public bool Wirable(PatchPort port)
    {
        if (string.IsNullOrEmpty(port.Node)) return false;
        if (!port.Fixed) return true;

        return Feeds(port) || Lands(port);
    }

    /// <summary>Whether that point is a place one of ours may be dropped.</summary>
    /// <remarks>
    /// The desk's own song and pads points, which is where those two go unless somebody moves
    /// them. They have to answer the hand as well as the source does, or the cable can be picked
    /// up off the desk and never put back on it.
    /// </remarks>
    /// <param name="takes">The point audio would arrive at.</param>
    private static bool Lands(PatchPort takes) =>
        takes.Side == PatchSide.In
        && string.Equals(takes.Node, PatchNodes.Mixer, System.StringComparison.Ordinal)
        && (string.Equals(takes.Name, PatchPorts.Song, System.StringComparison.Ordinal)
            || string.Equals(takes.Name, PatchPorts.Pads, System.StringComparison.Ordinal));

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
