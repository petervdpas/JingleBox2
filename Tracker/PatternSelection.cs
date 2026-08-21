using System;

namespace JingleBox2.Tracker;

/// <summary>
/// A block of the pattern: from where the selection was started to wherever it has been
/// dragged. Kept as the two corners rather than as a tidy rectangle, because a selection
/// dragged upwards or leftwards is still being dragged, and the anchor must not move.
/// </summary>
public readonly record struct PatternSelection(int AnchorLine, int AnchorTrack, int FocusLine, int FocusTrack)
{
    /// <summary>Nothing selected. Edits then fall back to the cursor, as they always did.</summary>
    public static readonly PatternSelection None = new(-1, -1, -1, -1);

    public bool IsEmpty => AnchorLine < 0 || AnchorTrack < 0 || FocusLine < 0 || FocusTrack < 0;

    public int FirstLine => Math.Min(AnchorLine, FocusLine);

    public int LastLine => Math.Max(AnchorLine, FocusLine);

    public int FirstTrack => Math.Min(AnchorTrack, FocusTrack);

    public int LastTrack => Math.Max(AnchorTrack, FocusTrack);

    public int LineCount => IsEmpty ? 0 : LastLine - FirstLine + 1;

    public int TrackCount => IsEmpty ? 0 : LastTrack - FirstTrack + 1;

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
    public string Describe() =>
        IsEmpty
            ? ""
            : LineCount + (LineCount == 1 ? " line" : " lines")
              + " on " + TrackCount + (TrackCount == 1 ? " track" : " tracks");
}
