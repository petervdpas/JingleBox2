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
    /// <summary>How far in from the left each column of blocks stands.</summary>
    private const double SourceX = 20;

    /// <inheritdoc cref="SourceX"/>
    private const double OwnX = 290;

    /// <inheritdoc cref="SourceX"/>
    private const double MixX = 570;

    /// <inheritdoc cref="SourceX"/>
    private const double OutX = 850;

    /// <summary>How far down the first block starts, and how far apart a column stacks them.</summary>
    private const double TopY = 20;

    /// <inheritdoc cref="TopY"/>
    private const double Apart = 96;

    /// <summary>Where the blocks that are not the recorder start, under it.</summary>
    private const double PlayY = 150;

    /// <inheritdoc/>
    public string OwnNode => RecordNode;

    /// <summary>What each block is called underneath, written out one per line.</summary>
    /// <remarks>
    /// An id is what a port names its block by and what a place is remembered by, so each one is
    /// a literal a reader and a search can both find rather than a word built out of pieces.
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

    /// <summary>What a source's own point is called.</summary>
    private const string OutName = "out";

    /// <summary>What the point the recorder listens at is called.</summary>
    private const string CaptureName = "capture";

    /// <inheritdoc/>
    public PatchPort OwnInput => new(RecordNode, CaptureName, PatchSide.In, PatchChannels.Stereo);

    /// <summary>The tracker's mix, on its way to the desk.</summary>
    private static readonly PatchPort TrackerOut =
        new(TrackerNode, "mix", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>The pads, on their way to the desk.</summary>
    private static readonly PatchPort FireOut =
        new(FireNode, "pads", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where the tracker arrives on the desk.</summary>
    private static readonly PatchPort MixerTracker =
        new(MixerNode, "tracker", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where the pads arrive on it.</summary>
    private static readonly PatchPort MixerPads =
        new(MixerNode, "pads", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>What the whole desk sums to.</summary>
    private static readonly PatchPort MixerOut =
        new(MixerNode, "master", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where that lands on the machine.</summary>
    private static readonly PatchPort OutputIn =
        new(OutputNode, "playback", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <inheritdoc/>
    public PatchScene Read(IReadOnlyList<AudioRoute> routes, AudioRoute? chosen, string? output = null)
    {
        var nodes = new List<PatchNode>();
        var links = new List<PatchLink>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int stacked = 0;

        foreach (var route in Offered(routes, chosen))
        {
            if (route == null || string.IsNullOrEmpty(route.Node)) continue;
            if (Ours(route.Node)) continue;
            if (!seen.Add(route.Node)) continue;

            var port = new PatchPort(route.Node, OutName, PatchSide.Out, Shape(route));

            nodes.Add(new PatchNode(
                route.Node,
                route.Display,
                Array.Empty<PatchPort>(),
                new[] { port },
                IsOurs: false,
                SourceX,
                TopY + (stacked++ * Apart)));

            if (chosen != null && string.Equals(route.Node, chosen.Node, StringComparison.Ordinal))
                links.Add(new PatchLink(port, OwnInput));
        }

        nodes.Add(new PatchNode(
            RecordNode, "RECORD", new[] { OwnInput }, Array.Empty<PatchPort>(), true, OwnX, TopY));

        nodes.Add(new PatchNode(
            TrackerNode, "TRACKER", Array.Empty<PatchPort>(), new[] { TrackerOut }, true, OwnX, PlayY));

        nodes.Add(new PatchNode(
            FireNode, "FIRE", Array.Empty<PatchPort>(), new[] { FireOut }, true, OwnX, PlayY + Apart));

        nodes.Add(new PatchNode(
            MixerNode, "MIXER", new[] { MixerTracker, MixerPads }, new[] { MixerOut }, true, MixX, PlayY));

        nodes.Add(new PatchNode(
            OutputNode,
            string.IsNullOrWhiteSpace(output) ? "Output" : output,
            new[] { OutputIn },
            Array.Empty<PatchPort>(),
            IsOurs: false,
            OutX,
            PlayY));

        links.Add(new PatchLink(TrackerOut, MixerTracker));
        links.Add(new PatchLink(FireOut, MixerPads));
        links.Add(new PatchLink(MixerOut, OutputIn));

        return new PatchScene(nodes, links);
    }

    /// <summary>Whether an address is one of this application's own blocks.</summary>
    /// <remarks>
    /// Asked of everything the machine offers, since a program on this computer could be called
    /// anything at all and two blocks with one id would be one block with two meanings.
    /// </remarks>
    private static bool Ours(string node) =>
        string.Equals(node, RecordNode, StringComparison.Ordinal) ||
        string.Equals(node, TrackerNode, StringComparison.Ordinal) ||
        string.Equals(node, FireNode, StringComparison.Ordinal) ||
        string.Equals(node, MixerNode, StringComparison.Ordinal) ||
        string.Equals(node, OutputNode, StringComparison.Ordinal);

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
