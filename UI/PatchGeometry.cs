using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PatchGeometry : IPatchGeometry
{
    /// <inheritdoc/>
    public double HeaderHeight => 38;

    /// <inheritdoc/>
    public double RowHeight => 24;

    /// <inheritdoc/>
    public double DotRadius => 5.5;

    /// <inheritdoc/>
    public double GrabRadius => 12;

    /// <inheritdoc/>
    public double EdgeInset => 10;

    /// <summary>The air under the last row, so a dot is not against the bottom edge.</summary>
    private const double Foot = 8;

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
    public IReadOnlyList<PatchRow> Rows(IReadOnlyList<PatchPort>? ports)
    {
        var rows = new List<PatchRow>();

        if (ports == null) return rows;

        for (int port = 0; port < ports.Count; port++)
        {
            int channels = Math.Max(1, (int)ports[port].Channels);

            for (int channel = 0; channel < channels; channel++) rows.Add(new PatchRow(port, channel));
        }

        return rows;
    }

    /// <inheritdoc/>
    public (double X1, double Y1, double X2, double Y2) Curve(
        double fromX, double fromY, double toX, double toY)
    {
        double bend = Math.Min(MostBend, Math.Max(30, Math.Abs(toX - fromX) / 2));

        return (fromX + bend, fromY, toX - bend, toY);
    }
}
