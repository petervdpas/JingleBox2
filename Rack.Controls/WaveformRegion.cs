using System;
using JingleBox2.Rack.Controls.Interfaces;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class WaveformRegion : IWaveformRegion
{
    /// <inheritdoc/>
    public double Started(double at, Region region, double gap) =>
        Math.Clamp(at, 0, Math.Max(0, region.End - gap));

    /// <inheritdoc/>
    public double Ended(double at, Region region, double gap) =>
        Math.Clamp(at, Math.Min(1, region.Start + gap), 1);

    /// <inheritdoc/>
    /// <remarks>
    /// Widened from whichever end has room. A drag that goes nowhere at the very end of a
    /// recording has none to the right, so it takes its gap from the left instead, and the two
    /// passes are what stop the region collapsing onto a single place there.
    /// </remarks>
    public Region Drawn(double from, double to, double gap)
    {
        double low = Math.Clamp(Math.Min(from, to), 0, 1);
        double high = Math.Clamp(Math.Max(from, to), 0, 1);

        if (high - low < gap) high = Math.Min(1, low + gap);
        if (high - low < gap) low = Math.Max(0, high - gap);

        return new Region(low, high);
    }
}
