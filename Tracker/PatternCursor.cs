using System;

namespace JingleBox2.Tracker;

/// <summary>Which of a cell's four columns the cursor is on.</summary>
public enum CellColumn
{
    Note = 0,
    Instrument = 1,
    Volume = 2,
    Effect = 3
}

/// <summary>
/// Where the edit cursor is and how it moves. Pure position maths, so the grid control can
/// stay about drawing and the moves can be checked without a window.
/// </summary>
public readonly record struct PatternCursor(int Line, int Track, CellColumn Column)
{
    public const int ColumnCount = 4;

    public static readonly PatternCursor Start = new(0, 0, CellColumn.Note);

    public PatternCursor MoveLine(int delta, int lines, bool wrap = true)
    {
        if (lines <= 0) return this;

        int line = Line + delta;
        line = wrap ? ((line % lines) + lines) % lines : Math.Clamp(line, 0, lines - 1);
        return this with { Line = line };
    }

    public PatternCursor MoveTrack(int delta, int trackCount)
    {
        if (trackCount <= 0) return this;
        return this with { Track = Math.Clamp(Track + delta, 0, trackCount - 1) };
    }

    /// <summary>
    /// Tab-style movement: steps through the columns of a track, then on to the next track.
    /// Stops at the edges rather than wrapping, so holding the key does not loop the row.
    /// </summary>
    public PatternCursor MoveColumn(int delta, int trackCount)
    {
        if (trackCount <= 0) return this;

        int flat = Track * ColumnCount + (int)Column + delta;
        int max = trackCount * ColumnCount - 1;
        flat = Math.Clamp(flat, 0, max);

        return this with { Track = flat / ColumnCount, Column = (CellColumn)(flat % ColumnCount) };
    }

    /// <summary>Jumps whole tracks, keeping the column. What Tab does in most trackers.</summary>
    public PatternCursor NextTrack(int trackCount) => MoveTrack(1, trackCount);

    public PatternCursor PreviousTrack(int trackCount) => MoveTrack(-1, trackCount);

    public PatternCursor ToLineStart() => this with { Line = 0 };

    public PatternCursor ToLineEnd(int lines) => this with { Line = Math.Max(0, lines - 1) };

    /// <summary>Pulls the cursor back inside a pattern that shrank under it.</summary>
    public PatternCursor Clamp(int lines, int trackCount) => new(
        Math.Clamp(Line, 0, Math.Max(0, lines - 1)),
        Math.Clamp(Track, 0, Math.Max(0, trackCount - 1)),
        Column);

    public override string ToString() => $"{Line:00}:{Track}:{Column}";
}
