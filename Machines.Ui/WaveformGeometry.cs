using Avalonia;
using Avalonia.Media;
using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// Turns peak data into the mirrored filled outline both the record tab and the editor draw.
/// </summary>
public static class WaveformGeometry
{
    /// <summary>The slice of peak data a viewport puts on screen.</summary>
    /// <param name="Start">First peak on screen.</param>
    /// <param name="End">One past the last, so the pair reads as a half-open range.</param>
    /// <param name="PixelWidth">How wide one peak is drawn, which is what spaces the columns.</param>
    public readonly record struct VisibleRange(int Start, int End, double PixelWidth)
    {
        /// <summary>How many peaks are in it.</summary>
        public int Count => End - Start;
    }

    /// <summary>
    /// Which peaks a viewport is showing, and how wide each of them lands.
    /// </summary>
    /// <remarks>
    /// The start is clamped so that scrolling to the far end still fills the width rather than
    /// running off it into blank space, which is what a scroll position taken at face value
    /// does once the zoom changes underneath it.
    /// </remarks>
    public static VisibleRange GetVisibleRange(int peakCount, WaveformViewport viewport, double width)
    {
        int visibleCount = Math.Max(1, (int)Math.Ceiling(peakCount / viewport.Zoom));

        int start = (int)Math.Round(viewport.Scroll * peakCount);
        start = Math.Clamp(start, 0, Math.Max(0, peakCount - visibleCount));

        int end = Math.Min(start + visibleCount, peakCount);

        return new VisibleRange(start, end, width / visibleCount);
    }

    /// <summary>
    /// Builds the outline: across the top following the peaks, then back along the bottom
    /// mirrored, closed into one filled shape centred on the vertical midpoint.
    /// </summary>
    public static StreamGeometry Build(float[] peaks, WaveformViewport viewport, double width, double height)
    {
        var geometry = new StreamGeometry();
        if (peaks.Length == 0 || width <= 0 || height <= 0) return geometry;

        var range = GetVisibleRange(peaks.Length, viewport, width);
        double centreY = height / 2;

        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(0, centreY), true);

        for (int i = range.Start; i < range.End; i++)
        {
            double x = PixelFor(i, range);
            if (x > width) break;
            ctx.LineTo(new Point(x, centreY - peaks[i] * centreY));
        }

        for (int i = range.End - 1; i >= range.Start; i--)
        {
            double x = PixelFor(i, range);
            if (x > width) continue;
            ctx.LineTo(new Point(x, centreY + peaks[i] * centreY));
        }

        ctx.EndFigure(true);
        return geometry;
    }

    /// <summary>
    /// Where one peak is drawn, measured to the middle of its own column rather than to its
    /// left edge, so the outline runs through the middle of each rather than leaning left.
    /// </summary>
    private static double PixelFor(int index, VisibleRange range)
        => (index - range.Start) * range.PixelWidth + range.PixelWidth / 2;
}
