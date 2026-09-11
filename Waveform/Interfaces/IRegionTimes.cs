using System;

namespace JingleBox2.Waveform.Interfaces;

/// <summary>
/// Where a place on a picture of a recording falls in the recording itself.
/// </summary>
/// <remarks>
/// A waveform is drawn in fractions of itself, since the picture knows nothing about rates:
/// nought is the head of the take and one is its tail, and every handle, playhead and marker on
/// it is a number between those two. What somebody working on a take wants to read is a time,
/// and the two facts that turn one into the other belong to the take rather than to the
/// picture: how many sample frames it holds, and how many of them go past in a second.
///
/// Every way this goes wrong is quiet. A rate of nought is a division by nought, a take still
/// being read holds no frames at all, and a region dragged out from right to left has its ends
/// the other way round, which reads as a selection of no length over audio somebody can plainly
/// see. So it answers rather than refuses, and nothing it hands back is negative or past the
/// end of the take.
/// </remarks>
public interface IRegionTimes
{
    /// <summary>How far into the take a place on the picture is.</summary>
    /// <param name="where">The place, as a fraction of the whole recording.</param>
    /// <param name="frames">How many sample frames the recording holds.</param>
    /// <param name="rate">How many of them go past in a second.</param>
    /// <returns>The time, which is nought where the take says nothing about itself.</returns>
    TimeSpan At(double where, long frames, int rate);

    /// <summary>How long the stretch between two places on the picture lasts.</summary>
    /// <remarks>The two are a pair rather than an order, so which end was dragged decides nothing.</remarks>
    /// <param name="from">One end, as a fraction of the whole recording.</param>
    /// <param name="to">The other.</param>
    /// <param name="frames">How many sample frames the recording holds.</param>
    /// <param name="rate">How many of them go past in a second.</param>
    /// <returns>The length, which is never negative.</returns>
    TimeSpan Between(double from, double to, long frames, int rate);
}
