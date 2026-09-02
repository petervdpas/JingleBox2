
namespace JingleBox2.Rack.Ui.Interfaces;

/// <summary>
/// Where a fader's cap sits on its track, and what the pointer at a given height means.
/// </summary>
/// <remarks>
/// The track runs bottom to top: the minimum is at the bottom, where a fader's zero belongs.
/// Both directions are here because they are a pair, and a pair that disagrees is a cap that
/// jumps away from the pointer the moment it is grabbed.
/// </remarks>
internal interface IFaderMath
{
    /// <summary>The value at a point on the track, snapped to the step grid.</summary>
    double ValueAt(
    double y,
    double trackTop,
    double trackLength,
    double minimum,
    double maximum,
    double step);

    /// <summary>The middle of the cap for a value, in the same coordinates.</summary>
    double CapCenterY(double value, double trackTop, double trackLength, double minimum, double maximum);
}
