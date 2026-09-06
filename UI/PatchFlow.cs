using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchFlow : IPatchFlow
{
    /// <summary>The blocks a cable can touch, written out one per line.</summary>
    /// <remarks>
    /// The same literals <see cref="PatchGraph"/> builds the picture from. Written again here
    /// rather than shared as one list because they are two different questions asked of the same
    /// names, and a name is what both are about: a search for the word finds every place that
    /// cares about it.
    /// </remarks>
    private const string RecordNode = "record";

    /// <inheritdoc cref="RecordNode"/>
    private const string TrackerNode = "tracker";

    /// <inheritdoc cref="RecordNode"/>
    private const string FireNode = "fire";

    /// <inheritdoc cref="RecordNode"/>
    private const string MixerNode = "mixer";

    /// <inheritdoc cref="RecordNode"/>
    private const string OutputNode = "output";

    /// <inheritdoc/>
    public IReadOnlyList<PatchLink> Live(IReadOnlyList<PatchLink>? links, PatchSignals signals)
    {
        var live = new List<PatchLink>();

        if (links == null) return live;

        foreach (var link in links)
            if (Carrying(link, signals)) live.Add(link);

        return live;
    }

    /// <summary>Whether one cable is carrying audio.</summary>
    /// <remarks>
    /// Read by where the cable lands. Anything arriving at the recorder is the input; anything
    /// leaving the desk is the output; and a cable into the desk carries whatever the block at
    /// its other end is doing. The tracker is asked per track, since it gives out one pair a
    /// track and only some of them are ever sounding. A cable between two things that are nothing to do with us carries
    /// nothing we can know about, and says so by staying dashed.
    /// </remarks>
    private static bool Carrying(PatchLink link, PatchSignals signals)
    {
        if (Is(link.To.Node, RecordNode)) return signals.Input;
        if (Is(link.To.Node, OutputNode)) return signals.Output;

        if (!Is(link.To.Node, MixerNode)) return false;

        if (Is(link.From.Node, RecordNode)) return signals.Takes;
        if (Is(link.From.Node, TrackerNode)) return signals.Sounding(link.From.Name);
        if (Is(link.From.Node, FireNode)) return signals.Pads;

        return false;
    }

    /// <summary>Whether an address is that block, compared as it is written.</summary>
    private static bool Is(string node, string one) => string.Equals(node, one, StringComparison.Ordinal);
}
