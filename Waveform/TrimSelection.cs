using System;

namespace JingleBox2.Waveform;

public enum TrimHandle
{
    None,
    Start,
    End
}

/// <summary>
/// The region of a recording marked to keep, as fractions of the whole file. Holds the rules
/// about how far the handles may travel, so no caller has to repeat them.
/// </summary>
public sealed class TrimSelection
{
    /// <summary>Smallest gap between the handles, as a share of what is currently on screen.</summary>
    public const double MinGapOfVisible = 0.005;

    public double Start { get; private set; }
    public double End { get; private set; } = 1;

    public bool IsWholeFile => Start <= 0 && End >= 1;

    /// <summary>Back to covering everything, for when the underlying file has been replaced.</summary>
    public void Reset()
    {
        Start = 0;
        End = 1;
    }

    /// <summary>The gap scales with zoom, so a fine cut stays possible when zoomed in.</summary>
    public static double MinGapFor(WaveformViewport viewport) => MinGapOfVisible * viewport.VisibleFraction;

    public void MoveStart(double fraction, double minGap)
        => Start = Math.Clamp(fraction, 0, Math.Max(0, End - minGap));

    public void MoveEnd(double fraction, double minGap)
        => End = Math.Clamp(fraction, Math.Min(1, Start + minGap), 1);

    public void Move(TrimHandle handle, double fraction, double minGap)
    {
        if (handle == TrimHandle.Start) MoveStart(fraction, minGap);
        else if (handle == TrimHandle.End) MoveEnd(fraction, minGap);
    }

    /// <summary>Pulls a position inside the region.</summary>
    public double Clamp(double fraction) => Math.Clamp(fraction, Start, End);

    /// <summary>
    /// Which handle a click lands on. Handles are drawn thin, so this works on proximity
    /// rather than the painted rectangle, and the nearer one wins when they are close together.
    /// </summary>
    public TrimHandle HitTest(double x, WaveformViewport viewport, double width, double tolerance)
    {
        double distanceToStart = Math.Abs(x - viewport.FractionToX(Start, width));
        double distanceToEnd = Math.Abs(x - viewport.FractionToX(End, width));

        if (distanceToStart > tolerance && distanceToEnd > tolerance)
            return TrimHandle.None;

        return distanceToStart <= distanceToEnd ? TrimHandle.Start : TrimHandle.End;
    }
}
