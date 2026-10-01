using System.Collections.Generic;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// The value grid of an automation lane: which round values its picture marks with a line and a
/// number, and where a point being drawn lands.
/// </summary>
/// <remarks>
/// Worked out in the parameter's own units, so a tempo is marked at 50, 100, 150 beats a minute
/// and a pan at minus one, minus a half, nought, rather than at quarters of a slider nobody sees.
/// The step is the round number, one, two or five times a power of ten, that puts about six lines
/// across the range.
///
/// A point lands on a marked line when it is let go close enough to one, which is the snap, and
/// anywhere else on the nearest fiftieth of a step, so it reads as a number somebody would type
/// rather than as wherever the pointer happened to be: a tempo lands on whole beats a minute.
/// </remarks>
public interface ILaneGrid
{
    /// <summary>The round values inside the range that get a line and a number, lowest first.</summary>
    /// <param name="min">The bottom of the parameter's range.</param>
    /// <param name="max">The top of it.</param>
    IReadOnlyList<double> Lines(double min, double max);

    /// <summary>Where a value lands: on a marked line within reach of one, otherwise on the nearest fine step, inside the range.</summary>
    /// <param name="value">Where the pointer put it, in the parameter's own units.</param>
    /// <param name="min">The bottom of the parameter's range.</param>
    /// <param name="max">The top of it.</param>
    /// <param name="reach">How close to a line, in the same units, counts as on it.</param>
    double Snap(double value, double min, double max, double reach);
}
