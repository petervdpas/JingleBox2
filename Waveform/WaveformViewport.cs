using System;

namespace JingleBox2.Waveform;

/// <summary>
/// The window of a recording currently on screen: how far in it starts and how much of the
/// whole file fits. Positions are fractions of the file (0..1), so they stay pinned to the
/// audio no matter how the view is zoomed or panned.
///
/// Deliberately free of any UI type, so the mapping can be tested on its own.
/// </summary>
public sealed class WaveformViewport
{
    public const double MinZoom = 1;
    public const double MaxZoom = 10;

    public double Zoom { get; private set; } = MinZoom;

    /// <summary>Fraction of the file sitting at the left edge.</summary>
    public double Scroll { get; private set; }

    /// <summary>How much of the whole file fits on screen at the current zoom.</summary>
    public double VisibleFraction => 1.0 / Zoom;

    /// <summary>Furthest left edge that still fills the view.</summary>
    public double MaxScroll => Math.Max(0, 1.0 - VisibleFraction);

    /// <summary>Fraction of the file at the middle of the view.</summary>
    public double Centre => Scroll + VisibleFraction / 2;

    public bool CanPan => Zoom > MinZoom;

    /// <summary>Fraction of the recording to an x offset. Outside [0, width] means off screen.</summary>
    public double FractionToX(double fraction, double width) => (fraction - Scroll) * Zoom * width;

    /// <summary>An x offset back to a fraction of the recording.</summary>
    public double XToFraction(double x, double width) => width > 0 ? Scroll + x / (Zoom * width) : 0;

    public bool IsOnScreen(double x, double width) => x >= 0 && x <= width;

    /// <summary>Scroll distance equivalent to dragging the content by a pixel delta.</summary>
    public double PanDistance(double deltaX, double width) => width > 0 ? deltaX / (Zoom * width) : 0;

    public void ScrollTo(double scroll) => Scroll = Math.Clamp(scroll, 0, MaxScroll);

    /// <summary>Zooms while holding the middle of the view still.</summary>
    public void ZoomTo(double zoom)
    {
        double centre = Centre;
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ScrollTo(centre - VisibleFraction / 2);
    }

    /// <summary>
    /// Zooms while holding whatever sits at <paramref name="anchorX"/> still, which is what
    /// makes wheel zoom feel right. Returns false when already at the limit.
    /// </summary>
    public bool ZoomAt(double zoom, double anchorX, double width)
    {
        double target = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(target - Zoom) < 1e-9) return false;

        double anchor = XToFraction(anchorX, width);
        Zoom = target;

        // Solve XToFraction(anchorX) == anchor for the new scroll offset.
        ScrollTo(anchor - anchorX / (Zoom * width));
        return true;
    }
}
