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

    /// <summary>
    /// A take being auditioned, on its way to the desk.
    /// </summary>
    /// <remarks>
    /// **The take goes through the mixer and the capture does not**, which is why the recorder
    /// has one point of each kind. Auditioning a take is played through the take bus and comes
    /// out of the master like anything else, so it has a strip on the desk; what is arriving at
    /// the input has a strip too and that strip reaches nothing, since its fader sets what a take
    /// will hold rather than what anybody hears.
    /// </remarks>
    private static readonly PatchPort RecordOut =
        new(RecordNode, "takes", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where a take arrives on the desk.</summary>
    private static readonly PatchPort MixerTakes =
        new(MixerNode, "takes", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>What the tracker gives out where the song has no tracks at all.</summary>
    /// <remarks>
    /// A block with nothing on it would be a block nobody can read: a song is always going to
    /// have tracks, and what this covers is the moment before one has been opened.
    /// </remarks>
    private const string WholeMix = "mix";

    /// <summary>The pads, on their way to the desk.</summary>
    private static readonly PatchPort FireOut =
        new(FireNode, "pads", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where the pads arrive on it.</summary>
    private static readonly PatchPort MixerPads =
        new(MixerNode, "pads", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>What the whole desk sums to.</summary>
    private static readonly PatchPort MixerOut =
        new(MixerNode, "master", PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where that lands on the machine.</summary>
    private static readonly PatchPort OutputIn =
        new(OutputNode, "playback", PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>
    /// One track's pair, on the tracker and again on the desk.
    /// </summary>
    /// <remarks>
    /// **Stereo, and that is not a guess.** A track is summed into a bus of its own, and that bus
    /// is interleaved two channels because a track has a pan and an insert chain: a plugin on it
    /// places what it hears in the stereo field, so what leaves a track has two sides whatever
    /// the instrument on it was. See <c>TrackMixer.VoicesThenInsert</c>.
    /// </remarks>
    /// <param name="node">Which block the point is on.</param>
    /// <param name="track">The track's name, as its strip wears it.</param>
    /// <param name="side">Whether audio leaves here or arrives here.</param>
    private static PatchPort Track(string node, string track, PatchSide side) =>
        new(node, track, side, PatchChannels.Stereo, Fixed: true);

    /// <inheritdoc/>
    public PatchScene Read(
        IReadOnlyList<AudioRoute> routes,
        AudioRoute? chosen,
        string? output = null,
        IReadOnlyList<string>? tracks = null)
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
            RecordNode, "RECORD", new[] { OwnInput }, new[] { RecordOut }, true, OwnX, TopY));

        var named = tracks is { Count: > 0 } ? tracks : new[] { WholeMix };

        var plays = new List<PatchPort>(named.Count);
        var takes = new List<PatchPort>(named.Count);

        foreach (string track in named)
        {
            plays.Add(Track(TrackerNode, track, PatchSide.Out));
            takes.Add(Track(MixerNode, track, PatchSide.In));
        }

        nodes.Add(new PatchNode(
            TrackerNode, "TRACKER", Array.Empty<PatchPort>(), plays, true, OwnX, PlayY));

        nodes.Add(new PatchNode(
            FireNode, "FIRE", Array.Empty<PatchPort>(), new[] { FireOut }, true, OwnX, PlayY + Apart));

        nodes.Add(new PatchNode(
            MixerNode,
            "MIXER",
            Desk(takes),
            new[] { MixerOut },
            true,
            MixX,
            PlayY));

        nodes.Add(new PatchNode(
            OutputNode,
            string.IsNullOrWhiteSpace(output) ? "Output" : output,
            new[] { OutputIn },
            Array.Empty<PatchPort>(),
            IsOurs: false,
            OutX,
            PlayY));

        links.Add(new PatchLink(RecordOut, MixerTakes));

        for (int track = 0; track < plays.Count; track++)
            links.Add(new PatchLink(plays[track], takes[track]));
        links.Add(new PatchLink(FireOut, MixerPads));
        links.Add(new PatchLink(MixerOut, OutputIn));

        return new PatchScene(nodes, links);
    }

    /// <summary>
    /// Everything the desk takes in: the song's tracks, then the pads, then a take.
    /// </summary>
    /// <remarks>
    /// The tracks first and in the song's own order, which is the order the strips stand in on
    /// the page beside this one. The two that are not tracks go under them rather than among
    /// them, so adding a track to a song does not move the point a cable was drawn to.
    /// </remarks>
    /// <param name="tracks">One point per track, already made.</param>
    private static IReadOnlyList<PatchPort> Desk(IReadOnlyList<PatchPort> tracks)
    {
        var takes = new List<PatchPort>(tracks.Count + 2);

        takes.AddRange(tracks);
        takes.Add(MixerPads);
        takes.Add(MixerTakes);

        return takes;
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
