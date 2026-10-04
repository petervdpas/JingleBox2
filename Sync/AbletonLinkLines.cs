using System;
using JingleBox2.Sync.Interfaces;

namespace JingleBox2.Sync;

/// <inheritdoc/>
public sealed class AbletonLinkLines : IAbletonLinkLines
{
    /// <summary>A bar of four, which is what a quantum that means nothing becomes.</summary>
    public const double UsualQuantum = 4.0;

    /// <summary>The largest quantum offered, four bars of four.</summary>
    public const double MostQuantum = 16.0;

    /// <inheritdoc/>
    public double Quantum(double asked) =>
        double.IsFinite(asked) && asked >= 1 ? Math.Min(asked, MostQuantum) : UsualQuantum;

    /// <inheritdoc/>
    /// <remarks>
    /// A beat a hair past a boundary from rounding is still on it, or a pass asked for exactly on
    /// the bar would wait a whole bar more: a millionth of a beat is a microsecond at sixty to the
    /// minute.
    /// </remarks>
    public double StartBeat(double beatNow, double quantum, int peers)
    {
        if (!double.IsFinite(beatNow)) return double.NaN;
        if (peers <= 0) return beatNow;

        double q = Quantum(quantum);
        double bars = Math.Ceiling(beatNow / q - 1e-6);

        return bars * q;
    }
}
