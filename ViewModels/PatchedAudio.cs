using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// Here rather than in the audio, because this is the one place both halves are in view: the
/// routing is drawn in <c>UI</c> and the busses live in <c>Audio</c>, and a seam that reached
/// from one into the other would be the layering said backwards.
///
/// Handed the two busses and a way of asking for each source's stream, rather than the engine and
/// the tracker: what this does is take a channel off one bus and put it on another, and it has no
/// business knowing what a song is.
/// </remarks>
public sealed class PatchedAudio : IPatchedAudio
{
    /// <summary>Every bus one of our sources may be sent to, by the block its cable lands on.</summary>
    private readonly IReadOnlyDictionary<string, IOutputBus> _busses;

    /// <summary>Each source's stream, asked for rather than kept.</summary>
    /// <remarks>
    /// **Asked each time**, since a stream is made when the thing behind it starts and closed
    /// when it stops: a handle kept from an earlier run is a number naming something that has
    /// gone, and putting that on a bus is the one way this could reach for freed memory.
    /// </remarks>
    private readonly IReadOnlyDictionary<string, Func<int>> _streams;

    /// <summary>Held while a source is moved, so two readings cannot move one at once.</summary>
    private readonly object _lock = new();

    /// <summary>Takes where a source may land and where its stream comes from.</summary>
    /// <param name="busses">Each block a cable may land on, and the bus behind it.</param>
    /// <param name="streams">Each source's stream by its block id, asked for when it is needed.</param>
    public PatchedAudio(
        IReadOnlyDictionary<string, IOutputBus> busses,
        IReadOnlyDictionary<string, Func<int>> streams)
    {
        _busses = busses;
        _streams = streams;
    }

    /// <inheritdoc/>
    public void Follow(PatchScene scene)
    {
        if (scene?.Links == null) return;

        lock (_lock)
        {
            foreach (var link in scene.Links)
            {
                if (link.From.Side != PatchSide.Out) continue;
                if (!_streams.TryGetValue(link.From.Node, out var asked)) continue;
                if (!_busses.TryGetValue(link.To.Node, out var lands)) continue;

                Land(link.From.Node, asked(), lands, link.To.Node);
            }
        }
    }

    /// <summary>
    /// Puts one stream on the bus its cable lands on.
    /// </summary>
    /// <remarks>
    /// **Taking it off wherever it was is the bus's own business**, since a channel belongs to one
    /// bus and that is the mixer's rule rather than a policy anybody here invents. This coordinated
    /// two busses for a while, removing from one and adding to the other in an order that had to
    /// be right, and it was not: a mixer refuses a channel that is still on another, so adding
    /// first could never work and the song stayed on the desk with its cable drawn to the recorder.
    /// The rule now lives with the thing that enforces it and there is nothing left here to get
    /// the order of.
    /// </remarks>
    /// <param name="node">Which source, for the log.</param>
    /// <param name="stream">Its stream, or nought while it is not running.</param>
    /// <param name="lands">The bus its cable lands on.</param>
    /// <param name="landed">That bus's block id, for the log.</param>
    private static void Land(string node, int stream, IOutputBus lands, string landed)
    {
        if (stream == 0) return;
        if (lands.Holds(stream)) return;

        bool took = lands.Add(stream);

        Log.Write(LogArea.Audio, () =>
            took
                ? "routing: " + node + " now goes to " + landed
                : "routing: " + node + " could not be put on " + landed);
    }
}
