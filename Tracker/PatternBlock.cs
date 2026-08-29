using System;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <summary>
/// A rectangle of cells lifted out of a pattern: what copy takes and what paste puts back.
/// Holds its own cells, so the pattern it came from can be edited, replaced or closed without
/// the copy changing under it.
/// </summary>
public sealed class PatternBlock
{
    /// <summary>The cells themselves, by line, track and note column, copied out of the pattern.</summary>
    /// <remarks>
    /// Room for the widest track in the block on every track of it, which wastes a few cells on
    /// a block where one track plays chords and the others do not. A block is a handful of
    /// kilobytes and lives until the next copy, so the shape being simple is worth more here
    /// than the room being tight.
    /// </remarks>
    private readonly TrackerCell[,,] _cells;

    /// <summary>How many note columns each track of the block was copied with.</summary>
    private readonly int[] _columns;

    /// <summary>Private, so <see cref="Copy"/> is the only way one is made and it cannot be empty.</summary>
    private PatternBlock(TrackerCell[,,] cells, int[] columns, int lines, int tracks)
    {
        _cells = cells;
        _columns = columns;
        Lines = lines;
        Tracks = tracks;
    }

    /// <summary>How many note columns the block holds for one of its tracks.</summary>
    public int ColumnsOn(int track) =>
        track >= 0 && track < _columns.Length ? _columns[track] : 0;

    /// <summary>How many lines deep it is.</summary>
    public int Lines { get; }

    /// <summary>And how many tracks across.</summary>
    public int Tracks { get; }

    /// <summary>True when there is nothing in it, which paste treats as nothing to do.</summary>
    public bool IsEmpty => Lines <= 0 || Tracks <= 0;

    /// <summary>One cell of the block, or an empty one for anything outside it.</summary>
    /// <remarks>
    /// Held rather than thrown, because a block is read by a paste that is already clipping
    /// itself against the pattern and an index past the edge is an ordinary state there.
    /// </remarks>
    public TrackerCell At(int line, int track) => At(line, track, 0);

    /// <summary>One cell of one note column of the block, or an empty one for anything outside it.</summary>
    public TrackerCell At(int line, int track, int column) =>
        line >= 0 && line < Lines && track >= 0 && track < Tracks
        && column >= 0 && column < ColumnsOn(track)
            ? _cells[line, track, column]
            : TrackerCell.Empty;

    /// <summary>How a menu or a status line names it.</summary>
    public string Describe() =>
        Lines + (Lines == 1 ? " line" : " lines") + " on " + Tracks + (Tracks == 1 ? " track" : " tracks");

    /// <summary>Takes a copy of a block of the pattern, or null when there is nothing to take.</summary>
    public static PatternBlock? Copy(Pattern? pattern, PatternSelection selection)
    {
        if (pattern == null) return null;

        var block = selection.Clamp(pattern.Lines, pattern.TrackCount);
        if (block.IsEmpty) return null;

        var columns = new int[block.TrackCount];
        int widest = 1;

        for (int track = 0; track < block.TrackCount; track++)
        {
            columns[track] = pattern.ColumnsOn(block.FirstTrack + track);
            widest = Math.Max(widest, columns[track]);
        }

        var cells = new TrackerCell[block.LineCount, block.TrackCount, widest];

        for (int line = 0; line < block.LineCount; line++)
        {
            for (int track = 0; track < block.TrackCount; track++)
            {
                for (int column = 0; column < columns[track]; column++)
                    cells[line, track, column] =
                        pattern[block.FirstLine + line, block.FirstTrack + track, column];
            }
        }

        return new PatternBlock(cells, columns, block.LineCount, block.TrackCount);
    }

    /// <summary>
    /// Writes the block into the pattern with its top left corner at the cursor, and returns
    /// what it covers so the paste can be left selected.
    /// </summary>
    /// <remarks>
    /// A block that hangs off the bottom or the right is clipped rather than refused: pasting
    /// four tracks into the last two puts two of them in, which is what a tracker does and
    /// what anyone dragging a phrase to the end of a pattern expects. A chord pasted onto a
    /// track with fewer note columns is clipped the same way and for the same reason, and the
    /// notes that do not fit are the last of the chord rather than the first.
    ///
    /// Cells are replaced, not merged. A paste is a decision about that region.
    ///
    /// This is the one edit that does not go through <see cref="PatternEdit"/>, and it still has
    /// to be recorded like the rest, so it rings that class's own bell on the way in. The bell is
    /// rung before anything is checked, since a paste that turns out to land nowhere leaves no
    /// step of its own but must not swallow the step the pattern was owed.
    ///
    /// The editor is handed in rather than made here, and that is not ceremony. The hook lives on
    /// the editor, so a block holding one of its own would ring a bell nobody had tied to
    /// anything: the paste would land and leave no step, and undo would go back past it to
    /// whatever happened before. It was static once and the question could not arise; it can now,
    /// and the answer is that a paste is an edit by the same editor as every other edit.
    /// </remarks>
    /// <param name="edits">The editor the history is listening to. The caller's own, always.</param>
    /// <param name="pattern">Where it lands. Nothing lands nowhere and leaves no step.</param>
    /// <param name="at">The top left corner of where it goes.</param>
    public PatternSelection Paste(IPatternEdit edits, Pattern? pattern, PatternCursor at)
    {
        if (pattern is not null) edits.Watching?.Invoke(pattern, "pasting");

        if (pattern == null || IsEmpty) return PatternSelection.None;
        if (!pattern.Contains(at.Line, at.Track)) return PatternSelection.None;

        int lines = Math.Min(Lines, pattern.Lines - at.Line);
        int tracks = Math.Min(Tracks, pattern.TrackCount - at.Track);

        for (int line = 0; line < lines; line++)
        {
            for (int track = 0; track < tracks; track++)
            {
                int columns = Math.Min(ColumnsOn(track), pattern.ColumnsOn(at.Track + track));

                for (int column = 0; column < columns; column++)
                    pattern[at.Line + line, at.Track + track, column] = _cells[line, track, column];
            }
        }

        return new PatternSelection(at.Line, at.Track, at.Line + lines - 1, at.Track + tracks - 1);
    }
}
