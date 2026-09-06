using System.Collections.Generic;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Where a device let go of over a chain lands.
/// </summary>
/// <remarks>
/// Two questions, and both are the kind that are wrong by one for a year. Which gap in the row
/// a point means, and what that gap comes to once the device being moved is no longer in the
/// row it was counted against.
///
/// A rule of its own rather than lines inside the strip, because neither of them is about
/// Avalonia: one takes the edges of the blocks and a distance across, the other takes two
/// numbers. Inside the view they could only be checked by dragging things with a hand, which is
/// how an order that is right except when you drag leftwards survives.
/// </remarks>
public interface IChainDrop
{
    /// <summary>
    /// Which gap in the row a point across it means.
    /// </summary>
    /// <remarks>
    /// The half of a block the pointer is on decides which side of it the device goes, which is
    /// what every list that takes a drop does and is the only way to say "in front of the first
    /// one". Before the first block is the start and past the last is the end, since somebody
    /// dragging to the end of a row means the end of it rather than nowhere.
    /// </remarks>
    /// <param name="blocks">Where each block starts and ends across the row, in order.</param>
    /// <param name="at">Where the hand is, across the row.</param>
    /// <returns>A gap from nought to as many blocks as there are.</returns>
    int Place(IReadOnlyList<(double Left, double Right)> blocks, double at);

    /// <summary>
    /// What that gap comes to as a place in the chain, once the device has left where it was.
    /// </summary>
    /// <remarks>
    /// **A gap is counted with the device still in the row and a chain counts without it**, so a
    /// device moved to the right lands one short of the gap it was dropped in. Dropping the
    /// first of three past the second is gap 2 with it still there and place 1 without, which is
    /// between the second and the third: the same place said in the two ways the two ends of
    /// this count.
    ///
    /// A device dropped on either side of itself is left where it is, since both of those gaps
    /// are the place it already occupies.
    /// </remarks>
    /// <param name="moving">Where the device sits now.</param>
    /// <param name="place">The gap it was let go in.</param>
    /// <returns>Where it should end up, counting from nought.</returns>
    int Landing(int moving, int place);
}
