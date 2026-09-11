using System;
using JingleBox2.Waveform.Interfaces;

namespace JingleBox2.Waveform;

/// <inheritdoc/>
public sealed class RegionTimes : IRegionTimes
{
    /// <inheritdoc/>
    /// <remarks>
    /// The fraction is held between the two ends of the take before it is multiplied out, since
    /// a handle is dragged against the edge of the picture and lands a hair outside it.
    /// </remarks>
    public TimeSpan At(double where, long frames, int rate)
    {
        if (frames <= 0 || rate <= 0 || double.IsNaN(where)) return TimeSpan.Zero;

        return TimeSpan.FromSeconds(Math.Clamp(where, 0, 1) * frames / rate);
    }

    /// <inheritdoc/>
    public TimeSpan Between(double from, double to, long frames, int rate) =>
        At(Math.Max(from, to), frames, rate) - At(Math.Min(from, to), frames, rate);
}
