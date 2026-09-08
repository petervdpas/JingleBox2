using JingleBox2.UI.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>What this application is carrying audio through at this moment.</summary>
/// <remarks>
/// Asked of the one thing that holds every half of this program at once, since no page can see
/// the pads, the song, the takes and the input together. Read off the meters that are already
/// running rather than from a second set of measurements: where the threshold sits is the
/// business of whoever is measuring, and everything above only wants to know yes or no.
/// </remarks>
public interface IAudioFlowing
{
    /// <summary>What is sounding right now, one answer per path.</summary>
    PatchSignals Signals { get; }

    /// <summary>The song's tracks, by the names their strips wear.</summary>
    /// <remarks>
    /// Here rather than on a second seam, because it is the same half of the application
    /// answering the same question one step further back: what paths there are, and which of
    /// them are carrying something.
    /// </remarks>
    System.Collections.Generic.IReadOnlyList<string> Tracks { get; }

    /// <summary>
    /// What one point of the routing is carrying.
    /// </summary>
    /// <remarks>
    /// **This is the routing table's own question, asked in the routing table's own words**, and
    /// that is what makes the picture and the meters one model rather than two that agree by
    /// luck. A patchbay block is a node, a connection point on it is a port, and a
    /// <see cref="UI.Records.SignalPoint"/> is exactly that pair. It took a node before, so the
    /// two halves shared a vocabulary and said so nowhere, and the day a port was renamed on one
    /// side the other went quietly to nought.
    ///
    /// A block as a whole is a point with no port, which is what the sidebar's own meter asks
    /// for. **The tracker answers that with its tracks joined**, one meter for the lot rather
    /// than one per track: what somebody wants to know from a block is whether audio is coming
    /// out of it, and thirty two meters stacked in a sidebar is a page nobody can read. The
    /// tracks are told apart on the picture, by which cables are drawn solid.
    ///
    /// A point this application measures nothing about answers nothing rather than nought, which
    /// is every block on the machine: a bar sitting at nought reads as silence rather than as a
    /// question nobody can answer.
    /// </remarks>
    /// <param name="point">Which point, as a block and one of its connection points.</param>
    UI.Records.PatchLevel Level(UI.Records.SignalPoint point);

    /// <summary>
    /// The strip one of a block's outputs is, so its mute and its solo can be reached.
    /// </summary>
    /// <remarks>
    /// The strip itself rather than a copy of what it says, so pressing M in the sidebar and
    /// pressing M on the desk are the same press on the same thing. Nothing where nothing
    /// answers, which is every block on the machine and the recorder's own capture: somebody
    /// else's program has no mute of ours, and a mute on the input would mean quietly recording
    /// nothing.
    /// </remarks>
    /// <param name="node">Which block, by its id.</param>
    /// <param name="port">Which of its outputs, by the name on the face of the block.</param>
    IStripSwitches? Switches(string node, string port);
}
