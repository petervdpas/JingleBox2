using System;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// The window of a recording currently on screen: how far in it starts and how much of the
/// whole file fits. Positions are fractions of the file (0..1), so they stay pinned to the
/// audio no matter how the view is zoomed or panned.
///
/// Deliberately free of any UI type, so the mapping can be tested on its own.
/// </summary>
public sealed class WaveformViewport
{
    /// <summary>The whole file on screen, which is as far out as there is anything to see.</summary>
    public const double MinZoom = 1;

    /// <summary>
    /// A four hundredth of the recording across the width, which is as far in as this goes.
    /// </summary>
    /// <remarks>
    /// Not limited by what it costs to draw: only the peaks on screen are walked, so zooming in
    /// is cheaper than zooming out. What limits it is how many peaks the recording was read
    /// into, and while that was five thousand this could not usefully pass about ten: a peak was
    /// already two pixels wide there and more zoom only drew the same peaks bigger.
    ///
    /// At two hundred thousand peaks, four hundred times leaves five hundred of them across an
    /// ordinary window, which is a peak every pixel or two and still a waveform rather than the
    /// data behind one. On a sixteen second take that is forty milliseconds across the window,
    /// which is close enough to see a click and take it out.
    /// </remarks>
    public const double MaxZoom = 400;

    /// <summary>How many times over the file would fit in the width, so 1 is the whole of it.</summary>
    public double Zoom { get; private set; } = MinZoom;

    /// <summary>Fraction of the file sitting at the left edge.</summary>
    public double Scroll { get; private set; }

    /// <summary>How much of the whole file fits on screen at the current zoom.</summary>
    public double VisibleFraction => 1.0 / Zoom;

    /// <summary>Furthest left edge that still fills the view.</summary>
    public double MaxScroll => Math.Max(0, 1.0 - VisibleFraction);

    /// <summary>Fraction of the file at the middle of the view.</summary>
    public double Centre => Scroll + VisibleFraction / 2;

    /// <summary>Whether there is anything off screen to pan to, which there is not at full zoom out.</summary>
    public bool CanPan => Zoom > MinZoom;

    /// <summary>Fraction of the recording to an x offset. Outside [0, width] means off screen.</summary>
    public double FractionToX(double fraction, double width) => (fraction - Scroll) * Zoom * width;

    /// <summary>An x offset back to a fraction of the recording.</summary>
    public double XToFraction(double x, double width) => width > 0 ? Scroll + x / (Zoom * width) : 0;

    /// <summary>Whether an x offset landed inside the view, for the markers drawn over the wave.</summary>
    public bool IsOnScreen(double x, double width) => x >= 0 && x <= width;

    /// <summary>Scroll distance equivalent to dragging the content by a pixel delta.</summary>
    public double PanDistance(double deltaX, double width) => width > 0 ? deltaX / (Zoom * width) : 0;

    /// <summary>Moves the left edge, held inside the file so the view never runs off the end.</summary>
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
    /// <remarks>
    /// The new scroll offset is <see cref="XToFraction"/> solved backwards: whatever fraction of
    /// the file was under <paramref name="anchorX"/> before the zoom has to be under it after.
    /// </remarks>
    public bool ZoomAt(double zoom, double anchorX, double width)
    {
        double target = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(target - Zoom) < 1e-9) return false;

        double anchor = XToFraction(anchorX, width);
        Zoom = target;

        ScrollTo(anchor - anchorX / (Zoom * width));
        return true;
    }
}
