using System;
using JingleBox2.Rack.Ui.Interfaces;

namespace JingleBox2.Rack.Ui;

/// <inheritdoc/>
internal sealed class KnobMath : IKnobMath
{
    /// <summary>Where a value sits in its range, and what a drag does to it. Holds nothing, so one is enough.</summary>
    private readonly IRangeValue _range = new RangeValue();

    /// <inheritdoc cref="IKnobMath.SweepDegrees"/>
    public const double SweepDegrees = 270;

    /// <inheritdoc cref="IKnobMath.StartDegrees"/>
    public const double StartDegrees = -135;

    /// <inheritdoc cref="IKnobMath.DragPixelsForFullRange"/>
    public const double DragPixelsForFullRange = 150;

    /// <inheritdoc/>
    double IKnobMath.SweepDegrees => SweepDegrees;

    /// <inheritdoc/>
    double IKnobMath.StartDegrees => StartDegrees;

    /// <inheritdoc/>
    double IKnobMath.DragPixelsForFullRange => DragPixelsForFullRange;

    /// <inheritdoc/>
    public double AngleFor(double value, double minimum, double maximum) =>
        StartDegrees + _range.Fraction(value, minimum, maximum) * SweepDegrees;

    /// <inheritdoc/>
    public (double X, double Y) PointAt(double centerX, double centerY, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180.0;

        return (centerX + radius * Math.Sin(radians), centerY - radius * Math.Cos(radians));
    }
}
