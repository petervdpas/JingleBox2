namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Whether a button held down has become a drag, or is still a click that has not let go yet.
/// </summary>
/// <remarks>
/// Every press moves a little. A hand pressing a button pushes the mouse a pixel or two doing
/// it, and a trackpad moves further than that, so anything that turns a press plus a movement
/// into a drag has to say how much movement it takes or it will read every click as one.
///
/// The pattern grid had no such rule and used the cell instead: a drag began the moment the
/// pointer was over a different cell from the one it was pressed on. A row is under twenty
/// pixels tall, so a click landing near the top or bottom of one needed a single pixel of hand
/// movement to select two lines, and clicking to move the cursor selected a block about as
/// often as it did not.
///
/// The cell test is still worth keeping and is not this: they answer different questions, and a
/// drag has to pass both. Far enough to be a drag at all, and onto a different cell so a drag
/// inside one cell selects nothing. Only the start is asked about, since a drag that has begun
/// stays begun however far back towards the press the hand wanders.
/// </remarks>
public interface IPointerDrag
{
    /// <summary>How far the pointer has to move before a press is read as a drag.</summary>
    double Threshold { get; }

    /// <summary>Whether the pointer has moved far enough from where it was pressed.</summary>
    /// <remarks>
    /// The two axes are asked about separately rather than as a distance, deliberately. A row is
    /// much shorter than a cell is wide, so a diagonal reading would let a mostly sideways
    /// movement start a drag down the pattern that nobody made.
    /// </remarks>
    /// <param name="fromX">Where the button went down, across.</param>
    /// <param name="fromY">Where the button went down, down the page.</param>
    /// <param name="toX">Where the pointer is now, across.</param>
    /// <param name="toY">Where the pointer is now, down the page.</param>
    bool Begun(double fromX, double fromY, double toX, double toY);
}
