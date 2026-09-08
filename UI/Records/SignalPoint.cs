namespace JingleBox2.UI.Records;

/// <summary>One place on the routing table where a level can be read.</summary>
/// <remarks>
/// **A point is a port rather than a block**, since a block usually has more than one and they
/// carry different audio: what arrives at the recorder and what a take being auditioned puts out
/// are two readings of one block, and a meter showing the wrong one of them would be right about
/// something nobody asked.
///
/// The whole block is a point too, written with no port at all: that is what the picture's own
/// meter beside a picked block is asked for, and for a block with several outputs it is the lot
/// of them together. Written as a point rather than as a second kind of thing, so one table
/// answers both.
/// </remarks>
/// <param name="Node">Which block, by the ids in <see cref="PatchNodes"/>.</param>
/// <param name="Port">Which of its ports, or empty for the block as a whole.</param>
public readonly record struct SignalPoint(string Node, string Port);
