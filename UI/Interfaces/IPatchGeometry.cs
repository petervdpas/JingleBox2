using System.Collections.Generic;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Where everything on a patchbay block sits, and the shape of the cable between two of them.
/// </summary>
/// <remarks>
/// **Kept out of the control because three things need the same answer.** A block draws its own
/// dots, the same block decides which dot a press landed on, and the surface above it draws
/// cables between dots on two different blocks. Written out where each of those happens, a cable
/// would meet its dot on one machine and miss it by two pixels on the next theme.
///
/// Everything here is in a block's own coordinates, with nought at its top left corner, and
/// knows nothing about the toolkit: what it deals in is numbers.
/// </remarks>
public interface IPatchGeometry
{
    /// <summary>How tall the block's title bar is, which is also the grip it is dragged by.</summary>
    double HeaderHeight { get; }

    /// <summary>How tall one port's row is.</summary>
    double RowHeight { get; }

    /// <summary>How big a connection point is drawn.</summary>
    double DotRadius { get; }

    /// <summary>How near a press has to land to count as being on a dot.</summary>
    /// <remarks>
    /// Wider than the dot, because a dot is a few pixels across and a hand is not. The same
    /// reasoning the automation lane's own grab distance is written up with.
    /// </remarks>
    double GrabRadius { get; }

    /// <summary>How far in from the block's edge a dot's centre sits.</summary>
    double EdgeInset { get; }

    /// <summary>How tall a block with this many rows of ports stands.</summary>
    /// <param name="rows">The most rows either side has, since the two sides share the height.</param>
    double BlockHeight(int rows);

    /// <summary>Where the middle of one port's row is, down from the top of the block.</summary>
    /// <param name="row">Which row, counting from nought under the title bar.</param>
    double RowCentre(int row);

    /// <summary>Which row a place on the block falls in, or -1 where it is on none of them.</summary>
    /// <param name="y">How far down the block the pointer is.</param>
    /// <param name="rows">How many rows that side has.</param>
    int RowAt(double y, int rows);

    /// <summary>
    /// Where each channel's dot sits within a row.
    /// </summary>
    /// <remarks>
    /// One dot in the middle for a mono port and a pair either side of the middle for a stereo
    /// one, which is the whole of what "either stereo or mono depending on the type" comes to on
    /// the screen. Asked for by count rather than by the enum so the drawing, the hit test and
    /// the cable layer cannot each decide it differently.
    /// </remarks>
    /// <param name="centre">The middle of the row, from <see cref="RowCentre"/>.</param>
    /// <param name="channels">How many channels the port carries.</param>
    IReadOnlyList<double> ChannelCentres(double centre, int channels);

    /// <summary>
    /// The two control points that bend a cable between its ends.
    /// </summary>
    /// <remarks>
    /// Horizontal at both ends, so a cable leaves an output going right and arrives at an input
    /// coming from the left however the two blocks are placed: that is what makes a picture of
    /// wires readable when they cross. The bend grows with the gap and is held to a ceiling, or
    /// two blocks side by side get a loop wider than either of them.
    /// </remarks>
    /// <param name="fromX">Where the cable leaves, across.</param>
    /// <param name="fromY">Where the cable leaves, down.</param>
    /// <param name="toX">Where it arrives, across.</param>
    /// <param name="toY">Where it arrives, down.</param>
    (double X1, double Y1, double X2, double Y2) Curve(
        double fromX, double fromY, double toX, double toY);
}
