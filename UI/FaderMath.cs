using System;

namespace JingleBox2.UI;

/// <summary>
/// Where a fader's cap sits on its track, and what the pointer at a given height means.
/// The track runs bottom to top: the minimum is at the bottom, where a fader's zero belongs.
/// </summary>
public static class FaderMath
{
    /// <summary>The value at a point on the track, snapped to the step grid.</summary>
    public static double ValueAt(
        double y,
        double trackTop,
        double trackLength,
        double minimum,
        double maximum,
        double step)
    {
        if (trackLength <= 0 || maximum <= minimum) return minimum;

        double fraction = Math.Clamp((trackTop + trackLength - y) / trackLength, 0, 1);

        return RangeValue.Quantize(minimum + fraction * (maximum - minimum), minimum, maximum, step);
    }

    /// <summary>The middle of the cap for a value, in the same coordinates.</summary>
    public static double CapCenterY(double value, double trackTop, double trackLength, double minimum, double maximum) =>
        trackTop + (1.0 - RangeValue.Fraction(value, minimum, maximum)) * trackLength;
}
