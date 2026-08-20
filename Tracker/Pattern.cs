using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// A block of steps across a fixed number of tracks. Cells are value types stored in one
/// array, so a pattern is cheap to copy and has no per-cell allocation.
/// </summary>
public sealed class Pattern
{
    public const int MinLines = 1;
    public const int MaxLines = 256;
    public const int DefaultLines = 64;

    private TrackerCell[] _cells;

    public string Name { get; set; } = "";
    public int Lines { get; private set; }
    public int TrackCount { get; private set; }

    public Pattern(int lines = DefaultLines, int trackCount = Song.DefaultTrackCount)
    {
        Lines = Math.Clamp(lines, MinLines, MaxLines);
        TrackCount = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        _cells = NewCells(Lines, TrackCount);
    }

    public TrackerCell this[int line, int track]
    {
        get
        {
            RequireInRange(line, track);
            return _cells[line * TrackCount + track];
        }
        set
        {
            RequireInRange(line, track);
            _cells[line * TrackCount + track] = value;
        }
    }

    public bool Contains(int line, int track) =>
        line >= 0 && line < Lines && track >= 0 && track < TrackCount;

    /// <summary>Changes the step count, keeping whatever still fits.</summary>
    public void Resize(int lines) => Rebuild(lines, TrackCount);

    /// <summary>Changes the track count, keeping whatever still fits.</summary>
    public void SetTrackCount(int trackCount) => Rebuild(Lines, trackCount);

    public void Clear() => Array.Fill(_cells, TrackerCell.Empty);

    public void ClearTrack(int track)
    {
        for (int line = 0; line < Lines; line++)
            this[line, track] = TrackerCell.Empty;
    }

    /// <summary>The cells of one step, left to right. Used by the player, one call per line.</summary>
    public IEnumerable<TrackerCell> Row(int line)
    {
        RequireInRange(line, 0);
        for (int track = 0; track < TrackCount; track++)
            yield return _cells[line * TrackCount + track];
    }

    public Pattern Clone()
    {
        var copy = new Pattern(Lines, TrackCount) { Name = Name };
        Array.Copy(_cells, copy._cells, _cells.Length);
        return copy;
    }

    private void Rebuild(int lines, int trackCount)
    {
        int newLines = Math.Clamp(lines, MinLines, MaxLines);
        int newTracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        if (newLines == Lines && newTracks == TrackCount) return;

        var replacement = NewCells(newLines, newTracks);

        int keptLines = Math.Min(Lines, newLines);
        int keptTracks = Math.Min(TrackCount, newTracks);
        for (int line = 0; line < keptLines; line++)
            for (int track = 0; track < keptTracks; track++)
                replacement[line * newTracks + track] = _cells[line * TrackCount + track];

        _cells = replacement;
        Lines = newLines;
        TrackCount = newTracks;
    }

    private static TrackerCell[] NewCells(int lines, int trackCount)
    {
        var cells = new TrackerCell[lines * trackCount];
        Array.Fill(cells, TrackerCell.Empty);
        return cells;
    }

    private void RequireInRange(int line, int track)
    {
        if (!Contains(line, track))
            throw new ArgumentOutOfRangeException(
                nameof(line), $"Cell {line},{track} is outside a {Lines}x{TrackCount} pattern.");
    }
}
