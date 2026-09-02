
namespace JingleBox2.Rack.Ui.Interfaces;

/// <summary>
/// Where a knob's pointer sits.
/// </summary>
/// <remarks>
/// The value maths it shares with the other range controls is <see cref="IRangeValue"/>; what
/// is here is only the part that is about being round. A pot turns three quarters of a circle
/// because that is what a pot does, and the sweep is written down rather than drawn so that
/// the tick ring, the pointer and the arc all read the same fact.
/// </remarks>
internal interface IKnobMath
{
    /// <summary>A pot turns three quarters of a circle, from seven o'clock to five o'clock.</summary>
    double SweepDegrees { get; }

    /// <summary>Where the sweep begins, which is seven o'clock, measured from twelve.</summary>
    double StartDegrees { get; }

    /// <summary>Pixels of vertical drag that cover the whole range.</summary>
    double DragPixelsForFullRange { get; }

    /// <summary>Pointer angle in degrees, clockwise from twelve o'clock.</summary>
    double AngleFor(double value, double minimum, double maximum);

    /// <summary>
    /// A point on the dial at that angle. Screen coordinates, so y grows downwards and twelve
    /// o'clock is straight up.
    /// </summary>
    (double X, double Y) PointAt(double centerX, double centerY, double radius, double angleDegrees);
}
