using System;

namespace JingleBox2.Tracker;

/// <summary>Which of a cell's four columns the cursor is on.</summary>
/// <remarks>
/// The numbers are the order the columns are drawn in and are used as indexes into
/// <see cref="PatternMetrics.ColumnWidths"/>, so they are written out rather than left implicit.
/// </remarks>
public enum CellColumn
{
    /// <summary>What to play.</summary>
    Note = 0,

    /// <summary>Which instrument to play it on.</summary>
    Instrument = 1,

    /// <summary>How loud.</summary>
    Volume = 2,

    /// <summary>One effect command.</summary>
    Effect = 3
}

/// <summary>
/// Where the edit cursor is and how it moves. Pure position maths, so the grid control can
/// stay about drawing and the moves can be checked without a window.
/// </summary>
/// <param name="Line">Which step, counting from zero.</param>
/// <param name="Track">Which track, counting from zero.</param>
/// <param name="Column">Which of that cell's four columns.</param>
public readonly record struct PatternCursor(int Line, int Track, CellColumn Column)
{
    /// <summary>How many columns a cell has, which is how the flat column index is worked out.</summary>
    public const int ColumnCount = 4;

    /// <summary>The top left of a pattern, on the note column, which is where a song opens.</summary>
    public static readonly PatternCursor Start = new(0, 0, CellColumn.Note);

    /// <summary>
    /// Up or down by lines, wrapping round the pattern by default.
    /// </summary>
    /// <remarks>
    /// Wrapping is what a tracker does with the arrow keys: a pattern is a loop and running off
    /// the bottom onto the top is the same movement the playhead makes. Clamping is for the
    /// gestures where it is not, such as dragging a selection, where wrapping would take the far
    /// corner to the other end of the pattern.
    /// </remarks>
    public PatternCursor MoveLine(int delta, int lines, bool wrap = true)
    {
        if (lines <= 0) return this;

        int line = Line + delta;
        line = wrap ? ((line % lines) + lines) % lines : Math.Clamp(line, 0, lines - 1);
        return this with { Line = line };
    }

    /// <summary>Left or right by whole tracks, stopping at the ends rather than wrapping.</summary>
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

    /// <summary>And back, for Shift+Tab.</summary>
    public PatternCursor PreviousTrack(int trackCount) => MoveTrack(-1, trackCount);

    /// <summary>To line 00, keeping the track and the column.</summary>
    public PatternCursor ToLineStart() => this with { Line = 0 };

    /// <summary>And to the last line.</summary>
    public PatternCursor ToLineEnd(int lines) => this with { Line = Math.Max(0, lines - 1) };

    /// <summary>Pulls the cursor back inside a pattern that shrank under it.</summary>
    public PatternCursor Clamp(int lines, int trackCount) => new(
        Math.Clamp(Line, 0, Math.Max(0, lines - 1)),
        Math.Clamp(Track, 0, Math.Max(0, trackCount - 1)),
        Column);

    /// <summary>Line, track and column, as the status line shows it.</summary>
    public override string ToString() => $"{Line:00}:{Track}:{Column}";
}
