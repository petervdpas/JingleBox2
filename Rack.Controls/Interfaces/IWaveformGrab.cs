namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// Which of the four lines on a picture of a recording a press takes hold of.
/// </summary>
/// <remarks>
/// The window's two ends are solid and the loop's two are dashed, and they very often lie on the
/// same pixel or within a few of it: a fresh loop spans the whole window. Deciding by which line is
/// nearest makes the hand's choice a matter of a pixel it cannot see, so a drag takes the loop as
/// often as the window.
///
/// **The picture is split in two instead.** The top half takes hold of the window's ends, whose
/// grips are drawn at the head, and the bottom half takes hold of the loop's, whose grips are
/// drawn at the foot. Nearest decides only within the half that was pressed, and where nothing in
/// that half is in reach the other half's lines are asked, so a press a little low beside the
/// window's end still takes it. With no loop showing there is only one half.
///
/// A position is in pixels across the picture, and a line out of reach is never taken.
/// </remarks>
internal interface IWaveformGrab
{
    /// <summary>
    /// Which line the press takes hold of: nought the start, one the end, two the loop's start,
    /// three the loop's end, or minus one for none.
    /// </summary>
    /// <param name="x">Where the press landed across the picture.</param>
    /// <param name="y">And down it.</param>
    /// <param name="height">How tall the picture is.</param>
    /// <param name="start">Where the start is drawn.</param>
    /// <param name="end">Where the end is drawn.</param>
    /// <param name="loopStart">Where the loop's start is drawn.</param>
    /// <param name="loopEnd">Where the loop's end is drawn.</param>
    /// <param name="loop">Whether the loop's lines are showing at all.</param>
    /// <param name="reach">How close a press has to be to a line to take it.</param>
    int Grabbed(double x, double y, double height, double start, double end,
                double loopStart, double loopEnd, bool loop, double reach);
}
