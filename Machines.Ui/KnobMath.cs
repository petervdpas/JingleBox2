using System;

namespace JingleBox2.UI;

/// <summary>
/// Where a knob's pointer sits. The value maths it shares with the other range controls lives
/// in <see cref="RangeValue"/>.
/// </summary>
public static class KnobMath
{
    /// <summary>A pot turns three quarters of a circle, from seven o'clock to five o'clock.</summary>
    public const double SweepDegrees = 270;

    public const double StartDegrees = -135;

    /// <summary>Pixels of vertical drag that cover the whole range.</summary>
    public const double DragPixelsForFullRange = 150;

    /// <summary>Pointer angle in degrees, clockwise from twelve o'clock.</summary>
    public static double AngleFor(double value, double minimum, double maximum) =>
        StartDegrees + RangeValue.Fraction(value, minimum, maximum) * SweepDegrees;

    /// <summary>
    /// A point on the dial at that angle. Screen coordinates, so y grows downwards and twelve
    /// o'clock is straight up.
    /// </summary>
    public static (double X, double Y) PointAt(double centerX, double centerY, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;

        return (centerX + radius * Math.Sin(radians), centerY - radius * Math.Cos(radians));
    }
}
