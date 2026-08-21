using System;

namespace JingleBox2.Tracker;

/// <summary>
/// Where everything sits in a pattern grid, in pixels, given one character's width and one
/// row's height. Pure maths with no drawing in it, so the grid and its header derive their
/// positions from the same place instead of each keeping a copy that can drift.
/// </summary>
public readonly record struct PatternMetrics(double CharWidth, double RowHeight, int TrackCount)
{
    /// <summary>Digits in the line number gutter.</summary>
    public const int LineNumberChars = 3;

    /// <summary>Blank characters between a track's divider and its columns, each side.</summary>
    public const double TrackPadChars = 1;

    /// <summary>One blank character after each column.</summary>
    public const int ColumnGapChars = 1;

    /// <summary>Characters per column: note, instrument, volume, effect.</summary>
    public static readonly int[] ColumnWidths = { 3, 2, 2, 3 };

    public double GutterWidth => (LineNumberChars + 1) * CharWidth;

    public double TrackWidth
    {
        get
        {
            double chars = TrackPadChars * 2;
            foreach (int width in ColumnWidths) chars += width + ColumnGapChars;
            return chars * CharWidth;
        }
    }

    public double ContentWidth => GutterWidth + Math.Max(0, TrackCount) * TrackWidth;

    public double ContentHeight(int lines) => Math.Max(0, lines) * RowHeight;

    /// <summary>Left edge of a track's divider.</summary>
    public double TrackDividerX(int track) => GutterWidth + track * TrackWidth;

    /// <summary>Left edge of a track's first column, past its divider and padding.</summary>
    public double TrackX(int track) => TrackDividerX(track) + TrackPadChars * CharWidth;

    public double ColumnX(int track, CellColumn column)
    {
        double x = TrackX(track);
        for (int i = 0; i < (int)column; i++)
            x += (ColumnWidths[i] + ColumnGapChars) * CharWidth;

        return x;
    }

    public double ColumnWidth(CellColumn column) => ColumnWidths[(int)column] * CharWidth;

    public double RowY(int line) => line * RowHeight;

    public int LineAt(double y, int lines) =>
        lines <= 0 ? 0 : Math.Clamp((int)(y / RowHeight), 0, lines - 1);

    public int TrackAt(double x)
    {
        if (TrackCount <= 0 || TrackWidth <= 0) return 0;
        return Math.Clamp((int)((x - GutterWidth) / TrackWidth), 0, TrackCount - 1);
    }

    /// <summary>Which column of <paramref name="track"/> the point falls on.</summary>
    public CellColumn ColumnAt(double x, int track)
    {
        double inside = x - TrackX(track);
        double edge = 0;

        for (int i = 0; i < ColumnWidths.Length; i++)
        {
            edge += (ColumnWidths[i] + ColumnGapChars) * CharWidth;
            if (inside < edge) return (CellColumn)i;
        }

        return (CellColumn)(ColumnWidths.Length - 1);
    }

    /// <summary>The cursor a click at this point lands on.</summary>
    public PatternCursor CursorAt(double x, double y, int lines)
    {
        int track = TrackAt(x);
        return new PatternCursor(LineAt(y, lines), track, ColumnAt(x, track));
    }
}
