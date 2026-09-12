using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
/// <remarks>
/// A scaling of nought or less is a screen nobody has asked yet, and anything that is not a number
/// cannot be rounded onto one: both answer what they were given, untouched.
/// </remarks>
public sealed class DevicePixels : IDevicePixels
{
    /// <inheritdoc/>
    public double Whole(double length, double scaling) =>
        Unanswerable(length, scaling) ? length : Math.Max(1, Pixels(length, scaling)) / scaling;

    /// <inheritdoc/>
    public double Lands(double position, double scaling) =>
        Unanswerable(position, scaling) ? position : Pixels(position, scaling) / scaling;

    /// <summary>Whether there is anything to work out, which needs a real screen and a real number.</summary>
    private static bool Unanswerable(double length, double scaling) =>
        scaling <= 0 || double.IsNaN(scaling) || double.IsNaN(length) || double.IsInfinity(length);

    /// <summary>
    /// The length in the screen's own pixels, to the nearest whole one.
    /// </summary>
    /// <remarks>
    /// Away from nought at the halfway mark rather than to the even pixel, so a length that falls
    /// exactly between two of them always answers the same way: to even, 22.5 and 23.5 round to 22
    /// and 24, which is a rule that grows and shrinks a row by a pixel depending on where it
    /// happened to fall.
    /// </remarks>
    private static double Pixels(double length, double scaling) =>
        Math.Round(length * scaling, MidpointRounding.AwayFromZero);
}
