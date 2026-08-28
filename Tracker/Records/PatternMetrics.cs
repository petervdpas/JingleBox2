using System;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// Where everything sits in a pattern grid, in pixels, given one character's width and one
/// row's height. Pure maths with no drawing in it, so the grid and its header derive their
/// positions from the same place instead of each keeping a copy that can drift.
/// </summary>
/// <param name="CharWidth">One character of the monospaced font every column is measured in.</param>
/// <param name="RowHeight">One row, which is the same for every line of every track.</param>
/// <param name="TrackCount">How many tracks are drawn, which is what sets the content's width.</param>
/// <param name="TopPad">
/// Space above line 00, which is half a viewport and is what keeps the cursor on the middle of
/// the screen at the top of a pattern as well as in the thick of one.
///
/// Always half a screen, with no exceptions: line 00 of a song's first pattern sits on the middle
/// exactly as any other row does, and what is above it there is blank. Whether the tail of the
/// pattern before this one is drawn into that room is a separate question, answered by whoever
/// draws, and it changes none of these numbers. Nought only for a header, which has no rows to
/// place.
/// </param>
/// <param name="BottomPad">The same underneath, for the pattern that comes next.</param>
public readonly record struct PatternMetrics(
    double CharWidth, double RowHeight, int TrackCount, double TopPad = 0, double BottomPad = 0)
{
    /// <summary>Digits in the line number gutter.</summary>
    public const int LineNumberChars = 3;

    /// <summary>Blank characters between a track's divider and its columns, each side.</summary>
    public const double TrackPadChars = 1;

    /// <summary>One blank character after each column.</summary>
    public const int ColumnGapChars = 1;

    /// <summary>Characters per column: note, instrument, volume, effect.</summary>
    public static readonly int[] ColumnWidths = { 3, 2, 2, 3 };

    /// <summary>The line number gutter, plus the blank character after it.</summary>
    public double GutterWidth => (LineNumberChars + 1) * CharWidth;

    /// <summary>One track, divider padding and column gaps included. Every track is this wide.</summary>
    public double TrackWidth
    {
        get
        {
            double chars = TrackPadChars * 2;
            foreach (int width in ColumnWidths) chars += width + ColumnGapChars;
            return chars * CharWidth;
        }
    }

    /// <summary>The gutter and every track, which is how wide the grid needs to be.</summary>
    public double ContentWidth => GutterWidth + Math.Max(0, TrackCount) * TrackWidth;

    /// <summary>The rows, and whatever neighbouring pattern is shown either side of them.</summary>
    /// <remarks>
    /// The room either side is counted whether or not there is anything to draw in it, which is
    /// what makes <see cref="RowY"/>, <see cref="LineAt"/> and this move together: a click still
    /// lands on the row it looks like it landed on.
    /// </remarks>
    public double ContentHeight(int lines) =>
        TopPad + Math.Max(0, lines) * RowHeight + BottomPad;

    /// <summary>Left edge of a track's divider.</summary>
    public double TrackDividerX(int track) => GutterWidth + track * TrackWidth;

    /// <summary>Left edge of a track's first column, past its divider and padding.</summary>
    public double TrackX(int track) => TrackDividerX(track) + TrackPadChars * CharWidth;

    /// <summary>Left edge of one column of one track.</summary>
    public double ColumnX(int track, CellColumn column)
    {
        double x = TrackX(track);
        for (int i = 0; i < (int)column; i++)
            x += (ColumnWidths[i] + ColumnGapChars) * CharWidth;

        return x;
    }

    /// <summary>How wide that column's characters are, without the gap after them.</summary>
    public double ColumnWidth(CellColumn column) => ColumnWidths[(int)column] * CharWidth;

    /// <summary>Top edge of a row, counted from the top of the content rather than of the rows.</summary>
    public double RowY(int line) => TopPad + line * RowHeight;

    /// <summary>Which line a point falls on, held inside the pattern.</summary>
    /// <remarks>
    /// Clamped rather than refused, since a point in the room above or below the pattern is a
    /// click on a neighbouring pattern's ghost and the nearest real row is what was meant.
    /// </remarks>
    public int LineAt(double y, int lines) =>
        lines <= 0 ? 0 : Math.Clamp((int)Math.Floor((y - TopPad) / RowHeight), 0, lines - 1);

    /// <summary>Which track a point falls on, held inside the song.</summary>
    public int TrackAt(double x)
    {
        if (TrackCount <= 0 || TrackWidth <= 0) return 0;
        return Math.Clamp((int)((x - GutterWidth) / TrackWidth), 0, TrackCount - 1);
    }

    /// <summary>Which column of <paramref name="track"/> the point falls on.</summary>
    /// <remarks>
    /// The gap after a column belongs to that column rather than to the next one, so a click in
    /// the space between two columns lands on the one to its left. Past the last column the
    /// answer is the last column, which is what a click in a track's right hand padding means.
    /// </remarks>
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
