using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class LaneGrid : ILaneGrid
{
    /// <summary>About how many lines the range is divided into.</summary>
    private const double Wanted = 6;

    /// <summary>How many fine steps a marked step holds.</summary>
    private const double Fine = 50;

    /// <inheritdoc/>
    public IReadOnlyList<double> Lines(double min, double max)
    {
        var lines = new List<double>();
        double step = Step(min, max);

        if (step <= 0) return lines;

        for (double at = Math.Ceiling(min / step) * step; at <= max + step * 1e-9; at += step)
            lines.Add(Math.Round(at / step) * step);

        return lines;
    }

    /// <inheritdoc/>
    public double Snap(double value, double min, double max, double reach)
    {
        if (double.IsNaN(value)) return min;

        double step = Step(min, max);
        double held = Math.Clamp(value, Math.Min(min, max), Math.Max(min, max));

        if (step <= 0) return held;

        double line = Math.Round(held / step) * step;

        if (Math.Abs(line - held) <= Math.Max(0, reach) && line >= min && line <= max) return line;

        double fine = step / Fine;

        return Math.Clamp(Math.Round(held / fine) * fine, min, max);
    }

    /// <summary>The round step that puts about six lines across the range, nought for a range of nothing.</summary>
    /// <remarks>
    /// The nearest of one, two and five times a power of ten as a ratio rather than as a
    /// difference, so a pan's third of a unit is marked every half rather than every fifth.
    /// </remarks>
    private static double Step(double min, double max)
    {
        double span = max - min;

        if (!(span > 0) || double.IsInfinity(span)) return 0;

        double raw = span / Wanted;
        double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / power;

        double nice = norm < Math.Sqrt(2) ? 1 : norm < Math.Sqrt(10) ? 2 : norm < Math.Sqrt(50) ? 5 : 10;

        return nice * power;
    }
}
