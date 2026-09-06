using System.Collections.Generic;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Turns what the machine is offering into the blocks and cables the patchbay draws.
/// </summary>
/// <remarks>
/// **From this application's point of view and no other.** Every block is either something that
/// can feed us or us, and every cable has us at one end, which is what makes this a patchbay
/// somebody can use rather than a picture of the whole machine: there is no gesture on it that
/// can unplug the speakers, because no edge that does not touch us is drawn.
///
/// **This is the routing table rather than a picture of one.** What the blocks and the fixed
/// cables between them say is how audio moves through this application: the pads and the tracker
/// reach the desk, the desk reaches the machine, and the recorder listens to whatever is patched
/// into it. Today those inner cables describe what the engine already does and cannot be pulled
/// out; the point of writing them down here is that there is then one place saying it, which is
/// the place the engine can be made to read.
///
/// A rule of its own rather than something the page works out, so what is drawn can be put a
/// question to without a window, a sound server or a hand: the awkward cases here are a source
/// that is being captured but is not on the offered list, a machine offering nothing at all,
/// and a list that has the same thing in it twice.
/// </remarks>
public interface IPatchGraph
{
    /// <summary>What this application's own block is called underneath.</summary>
    /// <remarks>
    /// An id rather than the words on the block, since a port names its block by this and the
    /// words are somebody's to change.
    /// </remarks>
    string OwnNode { get; }

    /// <summary>The connection point everything being captured arrives at.</summary>
    PatchPort OwnInput { get; }

    /// <summary>
    /// Reads one picture: a block for every source, our own block, and a cable for what is
    /// feeding us.
    /// </summary>
    /// <remarks>
    /// The source being captured is drawn even when it is not among the offered ones, because
    /// that really happens: the sound server can wire something into the capture that the picker
    /// would not list, and a picture that left it out would say the input is unconnected while it
    /// is recording.
    /// </remarks>
    /// <param name="routes">Everything with audio to give, as the routing offers it.</param>
    /// <param name="chosen">What is feeding the input now, or nothing while none is.</param>
    /// <param name="output">What the mix is leaving through, or nothing where that is not known yet.</param>
    PatchScene Read(IReadOnlyList<AudioRoute> routes, AudioRoute? chosen, string? output = null);
}
