using Avalonia;
using Avalonia.Media;
using System;
using JingleBox2.Rack.Ui.Interfaces;
using JingleBox2.Rack.Ui.Records;

namespace JingleBox2.Rack.Ui;

/// <inheritdoc/>
public sealed class WaveformGeometry : IWaveformGeometry
{
    /// <inheritdoc/>
    public VisibleRange GetVisibleRange(int peakCount, WaveformViewport viewport, double width)
    {
        int visibleCount = Math.Max(1, (int)Math.Ceiling(peakCount / viewport.Zoom));

        int start = (int)Math.Round(viewport.Scroll * peakCount);
        start = Math.Clamp(start, 0, Math.Max(0, peakCount - visibleCount));

        int end = Math.Min(start + visibleCount, peakCount);

        return new VisibleRange(start, end, width / visibleCount);
    }

    /// <inheritdoc/>
    public StreamGeometry Build(float[] peaks, WaveformViewport viewport, double width, double height)
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
    private double PixelFor(int index, VisibleRange range)
        => (index - range.Start) * range.PixelWidth + range.PixelWidth / 2;
}
