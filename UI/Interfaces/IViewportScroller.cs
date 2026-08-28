using JingleBox2.Tracker.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Works out the scroll offset that keeps one item inside a viewport. One axis at a time,
/// so the same function follows the cursor down the rows and across the tracks.
/// </summary>
public interface IViewportScroller
{
    /// <summary>How much of the neighbouring content to keep in view, in item lengths.</summary>
    double DefaultMarginItems { get; }

    /// <summary>
    /// The offset to scroll to so the item is visible, or the current offset when it already
    /// is. Scrolls the shortest distance: an item above the viewport comes to the top edge,
    /// one below comes to the bottom edge, rather than being centred every time.
    /// </summary>
    /// <remarks>
    /// Nought when there is nothing to scroll, which is a viewport with no length yet or content
    /// that already fits inside it. Both happen: the first is every control before its first
    /// layout, and the second is an ordinary short pattern.
    /// </remarks>
    /// <param name="offset">Where the viewport is scrolled to now.</param>
    /// <param name="viewportLength">How much is seen at once, along this axis.</param>
    /// <param name="itemStart">Where the item begins, in the content's own measure.</param>
    /// <param name="itemLength">How long the item is.</param>
    /// <param name="contentLength">How long everything there is to scroll through.</param>
    /// <param name="margin">How much room to keep beyond the item at either end.</param>
    /// <returns>The offset to scroll to, never past either end of the content.</returns>
    double KeepVisible(
        double offset,
        double viewportLength,
        double itemStart,
        double itemLength,
        double contentLength,
        double margin = 0);

    /// <summary>Keeps a pattern row in view, with a couple of rows of context around it.</summary>
    /// <param name="offset">Where the pattern is scrolled to now.</param>
    /// <param name="viewportHeight">How much of the pattern is seen at once.</param>
    /// <param name="metrics">The pattern's measurements.</param>
    /// <param name="row">The line to keep in view.</param>
    /// <param name="lines">How many lines the pattern has.</param>
    /// <returns>The offset to scroll to.</returns>
    double KeepRowVisible(
        double offset, double viewportHeight, PatternMetrics metrics, int row, int lines);

    /// <summary>
    /// The offset that puts a row on the middle of the screen and leaves it there.
    /// </summary>
    /// <remarks>
    /// What every tracker does, and it is the pattern that moves rather than the cursor: the
    /// line being worked on stays in the same place, so the eye has one thing to watch instead
    /// of following a highlight down the screen and being snapped back when it reaches the
    /// bottom.
    ///
    /// Always, with no exceptions, including line 00 of a song's first pattern and the last line
    /// of its last. That is not this function's doing: it works because the metrics leave half a
    /// screen of room above and below the rows, whether or not a neighbouring pattern is drawn
    /// into it, so there is an offset that puts even the first row on the middle. Getting that
    /// wrong is what once made the two ends of a pattern an exception, and the exception is what
    /// somebody notices, since the cursor jumps as a pattern is entered and left.
    ///
    /// Renoise leaves that room and leaves it empty, which is visible in its own screenshots by
    /// the cursor sitting at the same height on every one of them.
    /// </remarks>
    /// <param name="viewportHeight">How much of the pattern is seen at once.</param>
    /// <param name="metrics">The pattern's measurements.</param>
    /// <param name="row">The line to put on the middle.</param>
    /// <param name="lines">How many lines the pattern has.</param>
    /// <returns>The offset to scroll to.</returns>
    double CentreRow(double viewportHeight, PatternMetrics metrics, int row, int lines);

    /// <summary>
    /// Keeps a track in view, including its divider and padding. The line numbers scroll with
    /// the pattern rather than being pinned, so the first track counts the gutter as part of
    /// itself: scrolling back to track 0 should put the row numbers back on screen too.
    /// </summary>
    /// <param name="offset">Where the pattern is scrolled to now, across.</param>
    /// <param name="viewportWidth">How many tracks' worth is seen at once.</param>
    /// <param name="metrics">The pattern's measurements.</param>
    /// <param name="track">The track to keep in view.</param>
    /// <returns>The offset to scroll to.</returns>
    double KeepTrackVisible(double offset, double viewportWidth, PatternMetrics metrics, int track);
}
