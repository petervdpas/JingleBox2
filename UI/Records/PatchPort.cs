using JingleBox2.UI.Enums;

namespace JingleBox2.UI.Records;

/// <summary>
/// One connection point on a block: what it is called, which side it is on, and how many
/// channels it carries.
/// </summary>
/// <remarks>
/// The node is named rather than referenced so a port can be compared, written down and handed
/// about without carrying a block with it: two ports are the same port when they say the same
/// three things.
/// </remarks>
/// <param name="Node">The block this belongs to, by its id.</param>
/// <param name="Name">What the point is called on the face of the block.</param>
/// <param name="Side">Whether audio arrives here or leaves here.</param>
/// <param name="Channels">One or two, which decides how many dots are drawn and how a cable pairs up.</param>
/// <param name="Fixed">
/// Whether this point is wired by the program and cannot be repatched by hand. The way the
/// pads, the tracker and the mixer are joined to each other is a fact about how this
/// application is built rather than a choice anybody makes, so those points are drawn, are
/// plainly connected, and refuse the hand.
/// </param>
public readonly record struct PatchPort(
    string Node,
    string Name,
    PatchSide Side,
    PatchChannels Channels,
    bool Fixed = false)
{
    /// <summary>What the port carries, in the word somebody reading a sidebar wants.</summary>
    /// <remarks>
    /// Here rather than at the two places that show it, so the picture and the sidebar cannot
    /// end up calling the same port two things.
    /// </remarks>
    public string Shape => Channels == PatchChannels.Stereo ? "stereo" : "mono";

    /// <summary>What one channel of this port is called on the face of the block.</summary>
    /// <remarks>
    /// The sides are named the way the sound server names them, `_FL` and `_FR`, rather than
    /// left and right in words: somebody looking at this picture is usually looking at the
    /// machine's own graph beside it, and two names for one thing is a reader having to
    /// translate. A mono port says its own name and nothing else, since there is nothing to
    /// tell apart.
    /// </remarks>
    /// <param name="channel">Which channel, counting from nought.</param>
    public string Label(int channel) =>
        Channels == PatchChannels.Stereo ? Name + (channel == 0 ? "_FL" : "_FR") : Name;
}
