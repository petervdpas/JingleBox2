using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchGeometry : IPatchGeometry
{
    /// <inheritdoc/>
    public double HeaderHeight => 22;

    /// <inheritdoc/>
    public double RowHeight => 18;

    /// <inheritdoc/>
    public double DotRadius => 4;

    /// <inheritdoc/>
    public double GrabRadius => 10;

    /// <inheritdoc/>
    public double EdgeInset => 8;

    /// <summary>How far apart the two dots of a stereo port sit, either side of the middle.</summary>
    /// <remarks>
    /// Enough that two dots of four pixels do not touch, and little enough that the pair still
    /// reads as one port rather than as two.
    /// </remarks>
    private const double StereoSpread = 5;

    /// <summary>The air under the last row, so a dot is not against the bottom edge.</summary>
    private const double Foot = 6;

    /// <summary>How far a cable is allowed to bow out, whatever the gap.</summary>
    private const double MostBend = 90;

    /// <inheritdoc/>
    public double BlockHeight(int rows) => HeaderHeight + Math.Max(1, rows) * RowHeight + Foot;

    /// <inheritdoc/>
    public double RowCentre(int row) => HeaderHeight + (row + 0.5) * RowHeight;

    /// <inheritdoc/>
    public int RowAt(double y, int rows)
    {
        if (rows <= 0) return -1;

        int row = (int)Math.Floor((y - HeaderHeight) / RowHeight);

        return row < 0 || row >= rows ? -1 : row;
    }

    /// <inheritdoc/>
    public IReadOnlyList<double> ChannelCentres(double centre, int channels)
    {
        if (channels <= 1) return new[] { centre };

        var centres = new double[channels];
        double top = centre - (channels - 1) * StereoSpread / 2;

        for (int channel = 0; channel < channels; channel++)
            centres[channel] = top + channel * StereoSpread;

        return centres;
    }

    /// <inheritdoc/>
    public (double X1, double Y1, double X2, double Y2) Curve(
        double fromX, double fromY, double toX, double toY)
    {
        double bend = Math.Min(MostBend, Math.Max(30, Math.Abs(toX - fromX) / 2));

        return (fromX + bend, fromY, toX - bend, toY);
    }
}
