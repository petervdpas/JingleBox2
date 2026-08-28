using System;

namespace JingleBox2.Tracker;

/// <summary>
/// Works out the scroll offset that keeps one item inside a viewport. One axis at a time,
/// so the same function follows the cursor down the rows and across the tracks.
/// </summary>
public static class ViewportScroller
{
    /// <summary>How much of the neighbouring content to keep in view, in item lengths.</summary>
    public const double DefaultMarginItems = 2;

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
    public static double KeepVisible(
        double offset,
        double viewportLength,
        double itemStart,
        double itemLength,
        double contentLength,
        double margin = 0)
    {
        double maxOffset = Math.Max(0, contentLength - viewportLength);

        if (viewportLength <= 0 || maxOffset <= 0) return 0;

        double wantedStart = itemStart - margin;
        double wantedEnd = itemStart + itemLength + margin;

        double result = offset;

        if (wantedStart < offset)
            result = wantedStart;
        else if (wantedEnd > offset + viewportLength)
            result = wantedEnd - viewportLength;

        return Math.Clamp(result, 0, maxOffset);
    }

    /// <summary>Keeps a pattern row in view, with a couple of rows of context around it.</summary>
    public static double KeepRowVisible(
        double offset, double viewportHeight, PatternMetrics metrics, int row, int lines) =>
        KeepVisible(offset, viewportHeight, metrics.RowY(row), metrics.RowHeight,
            metrics.ContentHeight(lines), metrics.RowHeight * DefaultMarginItems);

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
    public static double CentreRow(
        double viewportHeight, PatternMetrics metrics, int row, int lines)
    {
        double content = metrics.ContentHeight(lines);
        double maxOffset = Math.Max(0, content - viewportHeight);

        if (viewportHeight <= 0 || maxOffset <= 0) return 0;

        double middle = metrics.RowY(row) + metrics.RowHeight / 2;

        return Math.Clamp(middle - viewportHeight / 2, 0, maxOffset);
    }

    /// <summary>
    /// Keeps a track in view, including its divider and padding. The line numbers scroll with
    /// the pattern rather than being pinned, so the first track counts the gutter as part of
    /// itself: scrolling back to track 0 should put the row numbers back on screen too.
    /// </summary>
    public static double KeepTrackVisible(
        double offset, double viewportWidth, PatternMetrics metrics, int track)
    {
        double start = track == 0 ? 0 : metrics.TrackDividerX(track);
        double length = metrics.TrackDividerX(track + 1) - start;

        return KeepVisible(offset, viewportWidth, start, length, metrics.ContentWidth);
    }
}
