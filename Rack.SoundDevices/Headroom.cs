using System;
using JingleBox2.Rack.SoundDevices.Interfaces;

namespace JingleBox2.Rack.SoundDevices;

/// <inheritdoc/>
public sealed class Headroom : IHeadroom
{
    /// <inheritdoc cref="IHeadroom.Least"/>
    public const double LeastDecibels = 12.0;

    /// <summary>What silence reads as, so nothing here ever answers infinity.</summary>
    public const double Quietest = 120.0;

    /// <inheritdoc/>
    public double Least => LeastDecibels;

    /// <inheritdoc/>
    public double Room(double peak)
    {
        if (double.IsNaN(peak)) return 0;

        double magnitude = Math.Abs(peak);

        if (magnitude <= 0) return Quietest;

        double room = -20 * Math.Log10(magnitude);

        return double.IsFinite(room) ? Math.Min(room, Quietest) : Quietest;
    }

    /// <inheritdoc/>
    public bool Cramped(double peak) => Room(peak) < LeastDecibels;
}
