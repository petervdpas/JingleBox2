using System;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
internal sealed class FaderMath : IFaderMath
{
    /// <summary>Where a value sits in its range, and what a drag does to it. Holds nothing, so one is enough.</summary>
    private readonly IRangeValue _range = new RangeValue();

    /// <inheritdoc/>
    public double ValueAt(
        double y,
        double trackTop,
        double trackLength,
        double minimum,
        double maximum,
        double step)
    {
        if (trackLength <= 0 || maximum <= minimum) return minimum;

        double fraction = Math.Clamp((trackTop + trackLength - y) / trackLength, 0, 1);

        return _range.Quantize(minimum + fraction * (maximum - minimum), minimum, maximum, step);
    }

    /// <inheritdoc/>
    public double CapCenterY(double value, double trackTop, double trackLength, double minimum, double maximum) =>
        trackTop + (1.0 - _range.Fraction(value, minimum, maximum)) * trackLength;
}
