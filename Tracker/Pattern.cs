using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// A block of steps across a fixed number of tracks. Cells are value types stored in one
/// array, so a pattern is cheap to copy and has no per-cell allocation.
/// </summary>
public sealed class Pattern
{
    /// <summary>
    /// Raised whenever the contents or the shape change. A pattern is edited in place, so
    /// without this a view bound to the pattern has no way to know anything happened: the
    /// reference it holds is still the same object.
    /// </summary>
    public event EventHandler? Changed;

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

            int index = line * TrackCount + track;
            if (_cells[index] == value) return;

            _cells[index] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Contains(int line, int track) =>
        line >= 0 && line < Lines && track >= 0 && track < TrackCount;

    /// <summary>Changes the step count, keeping whatever still fits.</summary>
    public void Resize(int lines) => Rebuild(lines, TrackCount);

    /// <summary>Changes the track count, keeping whatever still fits.</summary>
    public void SetTrackCount(int trackCount) => Rebuild(Lines, trackCount);

    public void Clear()
    {
        Array.Fill(_cells, TrackerCell.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearTrack(int track)
    {
        for (int line = 0; line < Lines; line++)
            this[line, track] = TrackerCell.Empty;
    }

    /// <summary>
    /// Takes a track out of where it is and puts it back in at another position, sliding the
    /// tracks it passes over to fill the gap.
    /// </summary>
    /// <remarks>
    /// A move, not a swap. Dragging track four in front of track one should leave the others
    /// in the order they were, one place along, which is what somebody dragging a column
    /// expects and what a swap does not do.
    /// </remarks>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= TrackCount || to < 0 || to >= TrackCount) return;

        var column = new TrackerCell[Lines];
        for (int line = 0; line < Lines; line++) column[line] = _cells[line * TrackCount + from];

        int step = from < to ? 1 : -1;

        for (int track = from; track != to; track += step)
        {
            for (int line = 0; line < Lines; line++)
                _cells[line * TrackCount + track] = _cells[line * TrackCount + track + step];
        }

        for (int line = 0; line < Lines; line++) _cells[line * TrackCount + to] = column[line];

        Changed?.Invoke(this, EventArgs.Empty);
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

    /// <summary>Everything in it, as one array to keep. For a history to hold a step in.</summary>
    /// <remarks>
    /// The array itself and not the pattern, because a step is a thing to be kept a hundred of
    /// and a pattern carries an event with whatever is listening to it attached. Cells are value
    /// types in one block, so this is a copy of the contents and shares nothing.
    /// </remarks>
    public TrackerCell[] Cells()
    {
        var kept = new TrackerCell[_cells.Length];
        Array.Copy(_cells, kept, _cells.Length);

        return kept;
    }

    /// <summary>True when what it holds now is exactly that.</summary>
    public bool Holds(TrackerCell[]? cells, int lines, int trackCount)
    {
        if (cells is null || lines != Lines || trackCount != TrackCount) return false;
        if (cells.Length != _cells.Length) return false;

        for (int at = 0; at < _cells.Length; at++)
            if (_cells[at] != cells[at]) return false;

        return true;
    }

    /// <summary>
    /// Puts a kept copy back, shape and all, and says so once.
    /// </summary>
    /// <remarks>
    /// Once, rather than a change per cell, because putting a step back is one thing that
    /// happened however many cells it touched. Going through the indexer would raise the event a
    /// couple of hundred times and redraw the grid as many.
    /// </remarks>
    public void Restore(TrackerCell[] cells, int lines, int trackCount)
    {
        if (cells is null) return;

        int newLines = Math.Clamp(lines, MinLines, MaxLines);
        int newTracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);

        if (cells.Length != newLines * newTracks) return;

        _cells = new TrackerCell[cells.Length];
        Array.Copy(cells, _cells, cells.Length);

        Lines = newLines;
        TrackCount = newTracks;

        Changed?.Invoke(this, EventArgs.Empty);
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

        Changed?.Invoke(this, EventArgs.Empty);
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
