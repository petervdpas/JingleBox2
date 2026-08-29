using System;
using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// Where the edit cursor is and how it moves. Pure position maths, so the grid control can
/// stay about drawing and the moves can be checked without a window.
/// </summary>
/// <param name="Line">Which step, counting from zero.</param>
/// <param name="Track">Which track, counting from zero.</param>
/// <param name="Column">Which of that cell's four columns.</param>
/// <param name="NoteColumn">
/// Which of the track's note columns, counting from zero.
/// </param>
/// <remarks>
/// The note column is last and defaults to nought, so everything written before tracks could
/// play chords still says what it always said: the first note column is the track itself.
///
/// Two words that both end in column, and they are two different things. A note column is a
/// voice: a whole cell again, with its own note, instrument, volume and effect. A
/// <see cref="CellColumn"/> is one of those four fields inside it.
/// </remarks>
public readonly record struct PatternCursor(
    int Line, int Track, CellColumn Column, int NoteColumn = 0)
{
    /// <summary>How many fields a cell has, which is how the flat column index is worked out.</summary>
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

    /// <summary>
    /// Left or right by whole tracks, stopping at the ends rather than wrapping.
    /// </summary>
    /// <remarks>
    /// The note column is dropped back to the first, because the track it is moving to need not
    /// have as many, and a cursor left on the third column of a track with one would be sitting
    /// on a cell that is not there.
    /// </remarks>
    public PatternCursor MoveTrack(int delta, int trackCount)
    {
        if (trackCount <= 0) return this;

        return this with { Track = Math.Clamp(Track + delta, 0, trackCount - 1), NoteColumn = 0 };
    }

    /// <summary>
    /// Tab-style movement: steps through the columns of a track, then on to the next track.
    /// Stops at the edges rather than wrapping, so holding the key does not loop the row.
    /// </summary>
    public PatternCursor MoveColumn(int delta, int trackCount, NoteColumns columns = default)
    {
        if (trackCount <= 0) return this;

        int flat = (columns.Before(Track) + NoteColumn) * ColumnCount + (int)Column + delta;
        int max = columns.Total(trackCount) * ColumnCount - 1;
        flat = Math.Clamp(flat, 0, max);

        int column = flat / ColumnCount;
        int track = columns.TrackOf(column, trackCount);

        return this with
        {
            Track = track,
            NoteColumn = column - columns.Before(track),
            Column = (CellColumn)(flat % ColumnCount)
        };
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
    /// <remarks>
    /// The note column is held to what that track has, which is what a track narrowed under the
    /// cursor needs: the columns it lost are gone and the cursor cannot stay in one of them.
    /// </remarks>
    public PatternCursor Clamp(int lines, int trackCount, NoteColumns columns = default)
    {
        int track = Math.Clamp(Track, 0, Math.Max(0, trackCount - 1));

        return new PatternCursor(
            Math.Clamp(Line, 0, Math.Max(0, lines - 1)),
            track,
            Column,
            Math.Clamp(NoteColumn, 0, Math.Max(0, columns.On(track) - 1)));
    }

    /// <summary>
    /// Line, track and column, as the status line shows it.
    /// </summary>
    /// <remarks>
    /// The note column is only said when it is not the first, so a song whose tracks play one
    /// note apiece reads exactly as it always did.
    /// </remarks>
    public override string ToString() =>
        NoteColumn == 0 ? $"{Line:00}:{Track}:{Column}" : $"{Line:00}:{Track}.{NoteColumn}:{Column}";
}
