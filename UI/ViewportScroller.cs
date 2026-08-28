using System;
using JingleBox2.Tracker.Records;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class ViewportScroller : IViewportScroller
{
    /// <inheritdoc cref="IViewportScroller.DefaultMarginItems"/>
    public const double DefaultMarginItems = 2;

    /// <inheritdoc/>
    double IViewportScroller.DefaultMarginItems => DefaultMarginItems;

    /// <inheritdoc/>
    public double KeepVisible(
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

    /// <inheritdoc/>
    public double KeepRowVisible(
        double offset, double viewportHeight, PatternMetrics metrics, int row, int lines) =>
        KeepVisible(offset, viewportHeight, metrics.RowY(row), metrics.RowHeight,
            metrics.ContentHeight(lines), metrics.RowHeight * DefaultMarginItems);

    /// <inheritdoc/>
    public double CentreRow(double viewportHeight, PatternMetrics metrics, int row, int lines)
    {
        double content = metrics.ContentHeight(lines);
        double maxOffset = Math.Max(0, content - viewportHeight);

        if (viewportHeight <= 0 || maxOffset <= 0) return 0;

        double middle = metrics.RowY(row) + metrics.RowHeight / 2;

        return Math.Clamp(middle - viewportHeight / 2, 0, maxOffset);
    }

    /// <inheritdoc/>
    public double KeepTrackVisible(
        double offset, double viewportWidth, PatternMetrics metrics, int track)
    {
        double start = track == 0 ? 0 : metrics.TrackDividerX(track);
        double length = metrics.TrackDividerX(track + 1) - start;

        return KeepVisible(offset, viewportWidth, start, length, metrics.ContentWidth);
    }
}
