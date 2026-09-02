using JingleBox2.Rack.Ui;
using System;
using JingleBox2.Waveform.Enums;

namespace JingleBox2.Waveform;

/// <summary>
/// The region of a recording marked to keep, as fractions of the whole file. Holds the rules
/// about how far the handles may travel, so no caller has to repeat them.
/// </summary>
public sealed class TrimSelection
{
    /// <summary>Smallest gap between the handles, as a share of what is currently on screen.</summary>
    public const double MinGapOfVisible = 0.005;

    /// <summary>Where the region begins, as a share of the whole file.</summary>
    public double Start { get; private set; }

    /// <summary>Where it ends, as a share of the whole file.</summary>
    public double End { get; private set; } = 1;

    /// <summary>Whether the region covers everything, so a trim would take nothing off.</summary>
    public bool IsWholeFile => Start <= 0 && End >= 1;

    /// <summary>Back to covering everything, for when the underlying file has been replaced.</summary>
    public void Reset()
    {
        Start = 0;
        End = 1;
    }

    /// <summary>The gap scales with zoom, so a fine cut stays possible when zoomed in.</summary>
    /// <param name="viewport">What is on screen, which is what the gap is a share of.</param>
    public static double MinGapFor(WaveformViewport viewport) => MinGapOfVisible * viewport.VisibleFraction;

    /// <summary>Moves the start, and never past the end.</summary>
    /// <param name="fraction">Where it is being dragged to, 0 to 1.</param>
    /// <param name="minGap">How close to the other handle it may come.</param>
    public void MoveStart(double fraction, double minGap)
        => Start = Math.Clamp(fraction, 0, Math.Max(0, End - minGap));

    /// <summary>Moves the end, and never past the start.</summary>
    /// <param name="fraction">Where it is being dragged to, 0 to 1.</param>
    /// <param name="minGap">How close to the other handle it may come.</param>
    public void MoveEnd(double fraction, double minGap)
        => End = Math.Clamp(fraction, Math.Min(1, Start + minGap), 1);

    /// <summary>Moves whichever handle is in the hand, and neither when it is not on one.</summary>
    /// <param name="handle">The handle a gesture took hold of.</param>
    /// <param name="fraction">Where it is being dragged to, 0 to 1.</param>
    /// <param name="minGap">How close to the other handle it may come.</param>
    public void Move(TrimHandle handle, double fraction, double minGap)
    {
        if (handle == TrimHandle.Start) MoveStart(fraction, minGap);
        else if (handle == TrimHandle.End) MoveEnd(fraction, minGap);
    }

    /// <summary>
    /// Drags a region out from nothing: two fractions in either order become the two ends.
    /// </summary>
    /// <remarks>
    /// The gesture every audio editor has, and the one this was missing: the handles could be
    /// moved but a region could not be drawn, so selecting the middle of a take meant dragging
    /// one end all the way in and then the other, past everything you were trying to look at.
    ///
    /// Either order, because a hand dragging leftwards is drawing the same region as a hand
    /// dragging rightwards and only one of them starts at the lower number. Held apart by the
    /// same minimum gap the handles keep, so a drag that goes nowhere leaves something that can
    /// still be taken hold of.
    /// </remarks>
    /// <param name="from">Where the drag started, nought to one.</param>
    /// <param name="to">Where it is now.</param>
    /// <param name="minGap">The narrowest the region may be.</param>
    public void Select(double from, double to, double minGap)
    {
        double low = Math.Clamp(Math.Min(from, to), 0, 1);
        double high = Math.Clamp(Math.Max(from, to), 0, 1);

        if (high - low < minGap) high = Math.Min(1, low + minGap);
        if (high - low < minGap) low = Math.Max(0, high - minGap);

        Start = low;
        End = high;
    }

    /// <summary>Pulls a position inside the region.</summary>
    /// <param name="fraction">A place in the file, 0 to 1.</param>
    public double Clamp(double fraction) => Math.Clamp(fraction, Start, End);

    /// <summary>
    /// Which handle a click lands on. Handles are drawn thin, so this works on proximity
    /// rather than the painted rectangle, and the nearer one wins when they are close together.
    /// </summary>
    /// <param name="x">Where the click landed, in the picture's own coordinates.</param>
    /// <param name="viewport">What is on screen, for turning a fraction into a place.</param>
    /// <param name="width">How wide the picture is drawn.</param>
    /// <param name="tolerance">How near counts as on a handle.</param>
    public TrimHandle HitTest(double x, WaveformViewport viewport, double width, double tolerance)
    {
        double distanceToStart = Math.Abs(x - viewport.FractionToX(Start, width));
        double distanceToEnd = Math.Abs(x - viewport.FractionToX(End, width));

        if (distanceToStart > tolerance && distanceToEnd > tolerance)
            return TrimHandle.None;

        return distanceToStart <= distanceToEnd ? TrimHandle.Start : TrimHandle.End;
    }
}
