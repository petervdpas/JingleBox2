namespace JingleBox2.Tracker.Enums;

/// <summary>How a lane gets from one point to the next.</summary>
/// <remarks>
/// Renoise's set, minus the one that is not built. Its third is <c>CURVES</c>, a cubic through
/// the points, and its <c>LINES</c> carries a per-point scaling that bends each segment. Both
/// are additions to the same points and neither changes what is stored, so they go on the end
/// of this when somebody draws them. Deliberately not declared before then: a song saying
/// curves and playing straight lines is the kind of silence that looks like it is working.
/// </remarks>
public enum AutomationPlay
{
    /// <summary>Holds the last value until the next point is reached. A stepped change.</summary>
    Points,

    /// <summary>Straight between the surrounding points. A sweep.</summary>
    Lines
}
