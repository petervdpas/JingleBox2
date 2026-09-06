using System;
using System.Collections.Generic;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchGraph : IPatchGraph
{
    /// <summary>How far in from the left the sources stand, and our own block from them.</summary>
    private const double SourceX = 24;

    /// <inheritdoc cref="SourceX"/>
    private const double OwnX = 300;

    /// <summary>How far down the first block starts, and how far apart they stack.</summary>
    private const double TopY = 20;

    /// <inheritdoc cref="TopY"/>
    private const double Apart = 78;

    /// <inheritdoc/>
    public string OwnNode => "jinglebox2";

    /// <summary>What the block for this application says on it.</summary>
    private const string OwnTitle = "JingleBox2";

    /// <summary>What the point everything arrives at is called.</summary>
    private const string InputName = "input";

    /// <summary>What a source's own point is called.</summary>
    private const string OutputName = "out";

    /// <inheritdoc/>
    public PatchPort OwnInput => new(OwnNode, InputName, PatchSide.In, PatchChannels.Stereo);

    /// <inheritdoc/>
    public PatchScene Read(IReadOnlyList<AudioRoute> routes, AudioRoute? chosen)
    {
        var nodes = new List<PatchNode>();
        var links = new List<PatchLink>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var route in Offered(routes, chosen))
        {
            if (route == null || string.IsNullOrEmpty(route.Node)) continue;
            if (string.Equals(route.Node, OwnNode, StringComparison.Ordinal)) continue;
            if (!seen.Add(route.Node)) continue;

            var port = new PatchPort(route.Node, OutputName, PatchSide.Out, Shape(route));

            nodes.Add(new PatchNode(
                route.Node,
                route.Display,
                Array.Empty<PatchPort>(),
                new[] { port },
                IsOurs: false,
                SourceX,
                TopY + (nodes.Count * Apart)));

            if (chosen != null && string.Equals(route.Node, chosen.Node, StringComparison.Ordinal))
                links.Add(new PatchLink(port, OwnInput));
        }

        nodes.Add(new PatchNode(
            OwnNode,
            OwnTitle,
            new[] { OwnInput },
            Array.Empty<PatchPort>(),
            IsOurs: true,
            OwnX,
            TopY));

        return new PatchScene(nodes, links);
    }

    /// <summary>
    /// Everything worth a block: what is offered, and what is feeding us whether or not it was
    /// offered.
    /// </summary>
    /// <remarks>
    /// The one being captured goes first, so it is the block at the top rather than somewhere
    /// down a list of twenty: it is the one thing on the page that is actually doing something.
    /// </remarks>
    private static IEnumerable<AudioRoute?> Offered(IReadOnlyList<AudioRoute>? routes, AudioRoute? chosen)
    {
        if (chosen != null) yield return chosen;

        if (routes == null) yield break;

        foreach (var route in routes) yield return route;
    }

    /// <summary>
    /// How many channels a source carries.
    /// </summary>
    /// <remarks>
    /// **Stereo, for everything the routing can offer today**, and this one line is the whole of
    /// why. A source is only offered at all where the graph reader recognised a stereo pair of
    /// ports on it, so a mono source is not in the list to be drawn: a Bluetooth headset in its
    /// telephone profile is exactly that case, and it is why it cannot be picked. When the
    /// routing learns to report a channel count, this is the line that reads it, and nothing
    /// above here changes: the picture, the hit test and the pairing already deal in
    /// <see cref="PatchChannels"/>.
    /// </remarks>
    private static PatchChannels Shape(AudioRoute route) => PatchChannels.Stereo;
}
