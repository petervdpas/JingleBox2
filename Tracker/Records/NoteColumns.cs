using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// How many note columns each track of a song has, and where each track's columns begin when
/// the whole row is counted from the left.
/// </summary>
/// <remarks>
/// The same walk is wanted in three unrelated places: the pattern works out where a cell sits,
/// the metrics work out where it is drawn, and the cursor works out where the next press of Tab
/// lands. Written out three times those would eventually disagree, and the way that fails is a
/// click landing on a cell other than the one under the pointer.
///
/// It holds the song's own list rather than a copy, because it is made per call from whatever
/// is in hand and thrown away. A caller with nothing to say hands over nothing, and every track
/// then has the one column every track had before note columns existed, which is what a pattern
/// drawn without this reads as.
/// </remarks>
/// <param name="Counts">
/// One count per track, as the song holds them. Short, missing or out of range entries read as
/// the default rather than throwing: this is drawn from and a song file is text anybody can
/// edit.
/// </param>
public readonly record struct NoteColumns(IReadOnlyList<int>? Counts = null)
{
    /// <summary>How many note columns a track has.</summary>
    public int On(int track)
    {
        if (track < 0) return 0;

        int said = Counts is not null && track < Counts.Count ? Counts[track] : Song.DefaultNoteColumns;

        return Math.Clamp(said, Song.MinNoteColumns, Song.MaxNoteColumns);
    }

    /// <summary>How many note columns there are to the left of a track, across the whole row.</summary>
    public int Before(int track)
    {
        int total = 0;

        for (int at = 0; at < track; at++) total += On(at);

        return total;
    }

    /// <summary>How many note columns the whole row holds.</summary>
    public int Total(int trackCount) => Before(Math.Max(0, trackCount));

    /// <summary>Which track a note column belongs to, counting across the row.</summary>
    /// <remarks>
    /// Held inside the song rather than answering with a track that is not there, since this is
    /// asked of a position a hand chose and a hand can be past the end.
    /// </remarks>
    public int TrackOf(int column, int trackCount)
    {
        int seen = 0;

        for (int track = 0; track < trackCount; track++)
        {
            seen += On(track);
            if (column < seen) return track;
        }

        return Math.Max(0, trackCount - 1);
    }

    /// <summary>And which of that track's columns it is.</summary>
    public int ColumnOf(int column, int trackCount) =>
        Math.Clamp(column - Before(TrackOf(column, trackCount)), 0,
                   Math.Max(0, On(TrackOf(column, trackCount)) - 1));
}
