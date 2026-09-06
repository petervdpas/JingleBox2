using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class StripRoom : IStripRoom
{
    /// <summary>What has to be left of whatever the strips are folded under.</summary>
    /// <remarks>
    /// Enough lines of a pattern to be worth looking at, which is what makes it a floor rather
    /// than a token: a pattern of two rows is a pattern nobody can work in, so a grip that could
    /// reach it would be a grip that breaks the page.
    /// </remarks>
    public const double DefaultLeast = 160;

    /// <inheritdoc/>
    public double Tallest(double room, double others, double least, double holding)
    {
        if (double.IsNaN(room) || double.IsInfinity(room) || room <= 0) return double.PositiveInfinity;

        double free = room - Math.Max(0, others) - Math.Max(0, least);

        return Math.Max(free, Math.Max(0, holding));
    }
}
