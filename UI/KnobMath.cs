using System;

namespace JingleBox2.UI;

/// <summary>
/// The maths behind a rotary knob: where the pointer sits, and what a drag does to the value.
/// No Avalonia types, so the behaviour can be checked without a window.
/// </summary>
public static class KnobMath
{
    /// <summary>A pot turns three quarters of a circle, from seven o'clock to five o'clock.</summary>
    public const double SweepDegrees = 270;

    public const double StartDegrees = -135;

    /// <summary>Pixels of vertical drag that cover the whole range.</summary>
    public const double DragPixelsForFullRange = 150;

    /// <summary>Holding shift makes the same drag cover a quarter as much.</summary>
    public const double FineDragFactor = 0.25;

    /// <summary>Where the value sits in its range, 0 to 1. A dead range reads as the bottom.</summary>
    public static double Fraction(double value, double minimum, double maximum)
    {
        double range = maximum - minimum;
        if (range <= 0 || double.IsNaN(value)) return 0;

        return Math.Clamp((value - minimum) / range, 0, 1);
    }

    /// <summary>Pointer angle in degrees, clockwise from twelve o'clock.</summary>
    public static double AngleFor(double value, double minimum, double maximum) =>
        StartDegrees + Fraction(value, minimum, maximum) * SweepDegrees;

    /// <summary>
    /// A point on the dial at that angle. Screen coordinates, so y grows downwards and twelve
    /// o'clock is straight up.
    /// </summary>
    public static (double X, double Y) PointAt(double centerX, double centerY, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;

        return (centerX + radius * Math.Sin(radians), centerY - radius * Math.Cos(radians));
    }

    /// <summary>
    /// The value a drag lands on. Dragging up raises it, which is why the delta is measured
    /// from the start of the drag rather than the last move: a drag that goes down and back up
    /// then ends where it began.
    /// </summary>
    public static double ValueFromDrag(
        double startValue,
        double pixelsDraggedUp,
        double minimum,
        double maximum,
        double step,
        bool fine = false)
    {
        double range = maximum - minimum;
        if (range <= 0) return minimum;

        double pixels = DragPixelsForFullRange / (fine ? FineDragFactor : 1.0);
        return Quantize(startValue + pixelsDraggedUp / pixels * range, minimum, maximum, step);
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
}
