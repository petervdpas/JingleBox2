using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchFlow : IPatchFlow
{
    /// <summary>The blocks a cable can touch, from the one place that says.</summary>
    /// <remarks>
    /// **The same words <see cref="PatchGraph"/> builds the picture from, and they are the same
    /// words.** They were written out again here on the reasoning that this asks a different
    /// question of them, which is true and is not a reason: two questions about one name still
    /// want one name. What that bought was two lists to keep in step, and the way it fails is a
    /// cable that never lights because the word behind it was typed differently in the half that
    /// decides.
    /// </remarks>
    private const string RecordNode = PatchNodes.Record;

    /// <inheritdoc cref="RecordNode"/>
    private const string TrackerNode = PatchNodes.Tracker;

    /// <inheritdoc cref="RecordNode"/>
    private const string FireNode = PatchNodes.Fire;

    /// <inheritdoc cref="RecordNode"/>
    private const string SongNode = PatchNodes.Song;

    /// <inheritdoc cref="RecordNode"/>
    private const string MixerNode = PatchNodes.Mixer;

    /// <inheritdoc cref="RecordNode"/>
    private const string OutputNode = PatchNodes.Output;

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
    /// leaving the desk is the output; a cable into the song is that track; and a cable into the
    /// desk carries whatever the block at its other end is doing. The tracker is asked per track,
    /// since it gives out one pair a track and only some of them are ever sounding, and the song
    /// is asked once, since what leaves it is those tracks already summed. A cable between two
    /// things that are nothing to do with us carries nothing we can know about, and says so by
    /// staying dashed.
    /// </remarks>
    private static bool Carrying(PatchLink link, PatchSignals signals)
    {
        if (Is(link.To.Node, RecordNode)) return Arriving(link, signals);
        if (Is(link.To.Node, OutputNode)) return signals.Output;

        if (Is(link.To.Node, SongNode)) return signals.Sounding(link.From.Name);

        if (!Is(link.To.Node, MixerNode)) return false;

        if (Is(link.From.Node, RecordNode)) return signals.Takes;
        if (Is(link.From.Node, SongNode)) return signals.Singing;
        if (Is(link.From.Node, TrackerNode)) return signals.Sounding(link.From.Name);
        if (Is(link.From.Node, FireNode)) return signals.Pads;

        return false;
    }

    /// <summary>Whether a cable arriving at the recorder is carrying anything.</summary>
    /// <remarks>
    /// **What arrives at the recorder is not always what is coming in at its input.** A source on
    /// the machine reaches it through the capture, so the input's own level is the answer for
    /// that one. One of ours patched across does not go near the capture: it is a bus put onto the
    /// recorder's, and whether it is carrying is whether that source is sounding.
    ///
    /// Asked because the picture said otherwise and was believed: the song's cable was drawn
    /// dashed while the song was audibly going through, which reads as the sound travelling by a
    /// path that is not on the picture at all. It was: the move had failed and the song was still
    /// on the desk. The cable was right that nothing was flowing along it and wrong about why, and
    /// the two together are a picture nobody can reason from.
    /// </remarks>
    /// <param name="link">The cable landing on the recorder.</param>
    /// <param name="signals">What is sounding.</param>
    private static bool Arriving(PatchLink link, PatchSignals signals)
    {
        if (Is(link.From.Node, SongNode)) return signals.Singing;
        if (Is(link.From.Node, FireNode)) return signals.Pads;

        return signals.Input;
    }

    /// <summary>Whether an address is that block, compared as it is written.</summary>
    private static bool Is(string node, string one) => string.Equals(node, one, StringComparison.Ordinal);
}
