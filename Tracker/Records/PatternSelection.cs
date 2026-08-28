using System;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// A block of the pattern: from where the selection was started to wherever it has been
/// dragged. Kept as the two corners rather than as a tidy rectangle, because a selection
/// dragged upwards or leftwards is still being dragged, and the anchor must not move.
/// </summary>
/// <param name="AnchorLine">The line the selection was started on, which never moves.</param>
/// <param name="AnchorTrack">And the track, for the same reason.</param>
/// <param name="FocusLine">The line the loose corner is on, which is wherever the hand is.</param>
/// <param name="FocusTrack">And its track.</param>
public readonly record struct PatternSelection(int AnchorLine, int AnchorTrack, int FocusLine, int FocusTrack)
{
    /// <summary>Nothing selected. Edits then fall back to the cursor, as they always did.</summary>
    public static readonly PatternSelection None = new(-1, -1, -1, -1);

    /// <summary>True when nothing is selected, which is any corner being off the pattern.</summary>
    public bool IsEmpty => AnchorLine < 0 || AnchorTrack < 0 || FocusLine < 0 || FocusTrack < 0;

    /// <summary>The topmost line covered, whichever corner it came from.</summary>
    public int FirstLine => Math.Min(AnchorLine, FocusLine);

    /// <summary>And the bottom one.</summary>
    public int LastLine => Math.Max(AnchorLine, FocusLine);

    /// <summary>The leftmost track covered.</summary>
    public int FirstTrack => Math.Min(AnchorTrack, FocusTrack);

    /// <summary>And the rightmost.</summary>
    public int LastTrack => Math.Max(AnchorTrack, FocusTrack);

    /// <summary>How many lines are covered, both ends included.</summary>
    public int LineCount => IsEmpty ? 0 : LastLine - FirstLine + 1;

    /// <summary>How many tracks are covered, both ends included.</summary>
    public int TrackCount => IsEmpty ? 0 : LastTrack - FirstTrack + 1;

    /// <summary>True when that cell is inside the block, for drawing it as selected.</summary>
    public bool Contains(int line, int track) =>
        !IsEmpty && line >= FirstLine && line <= LastLine && track >= FirstTrack && track <= LastTrack;

    /// <summary>A selection of one cell, where a drag or a shift-click starts.</summary>
    public static PatternSelection At(PatternCursor cursor) =>
        new(cursor.Line, cursor.Track, cursor.Line, cursor.Track);

    /// <summary>The whole pattern, for a select-all.</summary>
    public static PatternSelection All(int lines, int tracks) =>
        lines <= 0 || tracks <= 0 ? None : new(0, 0, lines - 1, tracks - 1);

    /// <summary>Drags the loose corner, leaving the anchor where the selection began.</summary>
    public PatternSelection ExtendTo(PatternCursor cursor) =>
        IsEmpty ? At(cursor) : this with { FocusLine = cursor.Line, FocusTrack = cursor.Track };

    /// <summary>Keeps a selection inside a pattern that may have shrunk under it.</summary>
    public PatternSelection Clamp(int lines, int tracks)
    {
        if (IsEmpty || lines <= 0 || tracks <= 0) return None;

        return new PatternSelection(
            Math.Clamp(AnchorLine, 0, lines - 1),
            Math.Clamp(AnchorTrack, 0, tracks - 1),
            Math.Clamp(FocusLine, 0, lines - 1),
            Math.Clamp(FocusTrack, 0, tracks - 1));
    }

    /// <summary>How the menu names what it is about to act on.</summary>
    /// <remarks>
    /// Empty for an empty selection rather than "0 lines on 0 tracks", since a menu entry that
    /// acts on the cursor should not be labelled with a size at all.
    /// </remarks>
    public string Describe() =>
        IsEmpty
            ? ""
            : LineCount + (LineCount == 1 ? " line" : " lines")
              + " on " + TrackCount + (TrackCount == 1 ? " track" : " tracks");
}
