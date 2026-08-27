using System;

namespace JingleBox2.Tracker;

/// <summary>
/// A rectangle of cells lifted out of a pattern: what copy takes and what paste puts back.
/// Holds its own cells, so the pattern it came from can be edited, replaced or closed without
/// the copy changing under it.
/// </summary>
public sealed class PatternBlock
{
    private readonly TrackerCell[,] _cells;

    private PatternBlock(TrackerCell[,] cells, int lines, int tracks)
    {
        _cells = cells;
        Lines = lines;
        Tracks = tracks;
    }

    public int Lines { get; }

    public int Tracks { get; }

    public bool IsEmpty => Lines <= 0 || Tracks <= 0;

    public TrackerCell At(int line, int track) =>
        line >= 0 && line < Lines && track >= 0 && track < Tracks ? _cells[line, track] : TrackerCell.Empty;

    /// <summary>How a menu or a status line names it.</summary>
    public string Describe() =>
        Lines + (Lines == 1 ? " line" : " lines") + " on " + Tracks + (Tracks == 1 ? " track" : " tracks");

    /// <summary>Takes a copy of a block of the pattern, or null when there is nothing to take.</summary>
    public static PatternBlock? Copy(Pattern? pattern, PatternSelection selection)
    {
        if (pattern == null) return null;

        var block = selection.Clamp(pattern.Lines, pattern.TrackCount);
        if (block.IsEmpty) return null;

        var cells = new TrackerCell[block.LineCount, block.TrackCount];

        for (int line = 0; line < block.LineCount; line++)
        {
            for (int track = 0; track < block.TrackCount; track++)
                cells[line, track] = pattern[block.FirstLine + line, block.FirstTrack + track];
        }

        return new PatternBlock(cells, block.LineCount, block.TrackCount);
    }

    /// <summary>
    /// Writes the block into the pattern with its top left corner at the cursor, and returns
    /// what it covers so the paste can be left selected.
    /// </summary>
    /// <remarks>
    /// A block that hangs off the bottom or the right is clipped rather than refused: pasting
    /// four tracks into the last two puts two of them in, which is what a tracker does and
    /// what anyone dragging a phrase to the end of a pattern expects.
    ///
    /// Cells are replaced, not merged. A paste is a decision about that region.
    /// </remarks>
    public PatternSelection Paste(Pattern? pattern, PatternCursor at)
    {
        // The one edit that is not a PatternEdit, and it has to be recorded like the rest.
        if (pattern is not null) PatternEdit.Watching?.Invoke(pattern, "pasting");

        if (pattern == null || IsEmpty) return PatternSelection.None;
        if (!pattern.Contains(at.Line, at.Track)) return PatternSelection.None;

        int lines = Math.Min(Lines, pattern.Lines - at.Line);
        int tracks = Math.Min(Tracks, pattern.TrackCount - at.Track);

        for (int line = 0; line < lines; line++)
        {
            for (int track = 0; track < tracks; track++)
                pattern[at.Line + line, at.Track + track] = _cells[line, track];
        }

        return new PatternSelection(at.Line, at.Track, at.Line + lines - 1, at.Track + tracks - 1);
    }
}
