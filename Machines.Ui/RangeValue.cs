using System;

namespace JingleBox2.UI;

/// <summary>
/// The value maths every range control shares: where a value sits in its range, what a drag
/// does to it, and the step grid it lands on. No Avalonia types, so it can be checked without
/// a window.
/// </summary>
public static class RangeValue
{
    /// <summary>Holding shift makes the same drag cover a quarter as much.</summary>
    public const double FineFactor = 0.25;

    /// <summary>Where the value sits in its range, 0 to 1. A dead range reads as the bottom.</summary>
    public static double Fraction(double value, double minimum, double maximum)
    {
        double range = maximum - minimum;
        if (range <= 0 || double.IsNaN(value)) return 0;

        return Math.Clamp((value - minimum) / range, 0, 1);
    }

    /// <summary>Clamps into range and onto the step grid, measured from the minimum.</summary>
    public static double Quantize(double value, double minimum, double maximum, double step)
    {
        if (double.IsNaN(value)) return minimum;
        if (maximum < minimum) return minimum;

        double clamped = Math.Clamp(value, minimum, maximum);
        if (step <= 0) return clamped;

        // Stepping from the minimum, not from zero: a range like -24..24 with a step of 5
        // should still be able to reach its own ends.
        double stepped = minimum + Math.Round((clamped - minimum) / step, MidpointRounding.AwayFromZero) * step;

        return Math.Clamp(stepped, minimum, maximum);
    }

    /// <summary>
    /// The value a drag lands on. Dragging up raises it, and the distance is measured from
    /// where the drag started rather than from the last move: a drag that goes down and back
    /// up then ends where it began.
    /// </summary>
    public static double FromDrag(
        double startValue,
        double pixelsDraggedUp,
        double minimum,
        double maximum,
        double step,
        double pixelsForFullRange,
        bool fine = false)
    {
        double range = maximum - minimum;
        if (range <= 0 || pixelsForFullRange <= 0) return minimum;

        double pixels = pixelsForFullRange / (fine ? FineFactor : 1.0);
        return Quantize(startValue + pixelsDraggedUp / pixels * range, minimum, maximum, step);
    }
}
