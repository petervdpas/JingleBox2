using Avalonia;
using Avalonia.Media;
using System;

namespace JingleBox2.Waveform;

/// <summary>
/// Turns peak data into the mirrored filled outline both the record tab and the editor draw.
/// </summary>
public static class WaveformGeometry
{
    /// <summary>The slice of peak data a viewport puts on screen.</summary>
    public readonly record struct VisibleRange(int Start, int End, double PixelWidth)
    {
        public int Count => End - Start;
    }

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

    private static double PixelFor(int index, VisibleRange range)
        => (index - range.Start) * range.PixelWidth + range.PixelWidth / 2;
}
