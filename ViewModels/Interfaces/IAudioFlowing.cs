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
    /// What one block is putting out, for the meter beside its details.
    /// </summary>
    /// <remarks>
    /// **The tracker answers with its tracks joined**, one meter for the lot rather than one per
    /// track: what somebody wants to know from a block is whether audio is coming out of it, and
    /// thirty two meters stacked in a sidebar is a page nobody can read. The tracks are told
    /// apart on the picture, by which cables are drawn solid.
    ///
    /// Read off the strips the mixer is already polling, so nothing is measured twice.
    /// </remarks>
    /// <param name="node">Which block, by its id.</param>
    UI.Records.PatchLevel Level(string node);

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
