using System;
using JingleBox2.Rack.Ui.Interfaces;

namespace JingleBox2.Rack.Ui;

/// <inheritdoc/>
public sealed class RangeValue : IRangeValue
{
    /// <inheritdoc cref="IRangeValue.FineFactor"/>
    public const double FineFactor = 0.25;

    /// <inheritdoc/>
    double IRangeValue.FineFactor => FineFactor;

    /// <inheritdoc/>
    public double Fraction(double value, double minimum, double maximum)
    {
        double range = maximum - minimum;
        if (range <= 0 || double.IsNaN(value)) return 0;

        return Math.Clamp((value - minimum) / range, 0, 1);
    }

    /// <inheritdoc/>
    public double Quantize(double value, double minimum, double maximum, double step)
    {
        if (double.IsNaN(value)) return minimum;
        if (maximum < minimum) return minimum;

        double clamped = Math.Clamp(value, minimum, maximum);
        if (step <= 0) return clamped;

        double stepped = minimum + Math.Round((clamped - minimum) / step, MidpointRounding.AwayFromZero) * step;

        return Math.Clamp(stepped, minimum, maximum);
    }

    /// <inheritdoc/>
    public double FromDrag(
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
