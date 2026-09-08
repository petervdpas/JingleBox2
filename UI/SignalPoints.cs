using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
/// <remarks>
/// **One list and no switch**, which is the point: what the points are and what each one reads
/// are the same table read two ways, so they cannot fall out of step. Written as a switch it was
/// two things, the cases and whatever else knew the cases, and there was nothing to walk.
///
/// A block as a whole is a point with no port, and it is in the list beside its own ports rather
/// than worked out from them: what a block is chiefly putting out is a decision. The recorder's
/// is what is coming in, since that is what somebody asks a recorder; the desk's and the
/// output's are what leaves, since that is what everything sums to.
/// </remarks>
public sealed class SignalPoints : ISignalPoints
{
    /// <summary>Each point and what reads it.</summary>
    private readonly Dictionary<SignalPoint, Func<PatchLevel>> _points;

    /// <summary>The points, in the order they were written.</summary>
    private readonly List<SignalPoint> _ours;

    /// <summary>
    /// Takes the readings the routing is made of.
    /// </summary>
    /// <remarks>
    /// A reading apiece rather than the engine, so what a point carries can be decided here and
    /// measured wherever the audio actually is. The two joined ones are handed in whole because
    /// only the tracker can work them out: a track's level comes from the voices sounding on it.
    /// </remarks>
    /// <param name="capture">What is arriving at the recorder's input.</param>
    /// <param name="takes">What the recorder is sending to the desk.</param>
    /// <param name="pads">What the pads sum to.</param>
    /// <param name="song">What the song sums to, after its own chain and level.</param>
    /// <param name="tracks">The song's tracks at once, which is what a whole song block shows.</param>
    /// <param name="leaving">What the desk sums to, which is what the machine plays.</param>
    public SignalPoints(
        Func<PatchLevel> capture,
        Func<PatchLevel> takes,
        Func<PatchLevel> pads,
        Func<PatchLevel> song,
        Func<PatchLevel> tracks,
        Func<PatchLevel> leaving)
    {
        _points = new Dictionary<SignalPoint, Func<PatchLevel>>
        {
            [new SignalPoint(PatchNodes.Record, PatchPorts.Capture)] = capture,
            [new SignalPoint(PatchNodes.Record, PatchPorts.Takes)] = takes,
            [new SignalPoint(PatchNodes.Record, "")] = capture,

            [new SignalPoint(PatchNodes.Fire, PatchPorts.Pads)] = pads,
            [new SignalPoint(PatchNodes.Fire, "")] = pads,

            [new SignalPoint(PatchNodes.Song, PatchPorts.Song)] = song,
            [new SignalPoint(PatchNodes.Song, "")] = song,

            [new SignalPoint(PatchNodes.Tracker, "")] = tracks,

            [new SignalPoint(PatchNodes.Mixer, PatchPorts.Master)] = leaving,
            [new SignalPoint(PatchNodes.Mixer, "")] = leaving,

            [new SignalPoint(PatchNodes.Output, PatchPorts.Playback)] = leaving,
            [new SignalPoint(PatchNodes.Output, "")] = leaving
        };

        _ours = new List<SignalPoint>(_points.Keys);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SignalPoint> Ours => _ours;

    /// <inheritdoc/>
    public PatchLevel At(SignalPoint point) =>
        _points.TryGetValue(point, out var reads) ? reads() : default;
}
