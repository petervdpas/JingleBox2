using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>A pattern of one step, which is as short as one can be.</summary>
    public const int MinLines = 1;

    /// <summary>
    /// Two hundred and fifty six, which is the longest a pattern can be.
    /// </summary>
    /// <remarks>
    /// The same ceiling trackers have always had, and the reason it is worth having is the
    /// history: a step is a copy of the cells, so the largest pattern there can be decides what
    /// a hundred steps cost.
    /// </remarks>
    public const int MaxLines = 256;

    /// <summary>Sixty four, which is four bars at the usual four lines to a beat.</summary>
    public const int DefaultLines = 64;

    /// <summary>
    /// Every cell, line by line, one array rather than an array of rows.
    /// </summary>
    /// <remarks>
    /// A cell is a value type, so this is one block with no allocation per cell and no reference
    /// to follow per read. That is what makes a copy of a whole pattern a memory copy of a few
    /// kilobytes, which is what the history is built on.
    ///
    /// Replaced rather than resized when the shape changes, since the index of a cell is worked
    /// out from the track count and every one of them moves.
    /// </remarks>
    private TrackerCell[] _cells;

    /// <summary>
    /// The parameters that move over this pattern, one lane apiece.
    /// </summary>
    /// <remarks>
    /// Beside the cells rather than among them, because they are a different shape: a cell is a
    /// value type at a fixed place in a grid and a lane is a sparse list of points that most
    /// tracks do not have at all. A pattern with no automation carries an empty list and pays
    /// nothing for it.
    ///
    /// In the pattern rather than in the song, which is Renoise's arrangement and the only one
    /// that makes sense here: copying a pattern has to copy its movement, and a lane's length is
    /// the pattern's length rather than a number of its own.
    /// </remarks>
    private readonly List<AutomationLane> _lanes = new();

    /// <summary>The lanes, to be read. <see cref="Lane"/> is the only way one is added.</summary>
    public IReadOnlyList<AutomationLane> Lanes => _lanes;

    /// <summary>What the order list calls it, which is two digits unless somebody renames it.</summary>
    public string Name { get; set; } = "";

    /// <summary>How many steps it has.</summary>
    public int Lines { get; private set; }

    /// <summary>
    /// How many tracks wide it is, which is the song's track count and not a choice of its own.
    /// </summary>
    /// <remarks>
    /// Every pattern in a song is the same width. Kept here as well because the index of a cell
    /// is worked out from it, and reaching to the song for it on every read would put a
    /// reference in the one place that has to stay a plain array lookup.
    /// </remarks>
    public int TrackCount { get; private set; }

    /// <summary>A pattern of that shape, every cell blank. Both numbers are held to their range.</summary>
    public Pattern(int lines = DefaultLines, int trackCount = Song.DefaultTrackCount)
    {
        Lines = Math.Clamp(lines, MinLines, MaxLines);
        TrackCount = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        _cells = NewCells(Lines, TrackCount);
    }

    /// <summary>
    /// One cell. Reading or writing outside the pattern throws, because that is a mistake in the
    /// caller rather than an ordinary state.
    /// </summary>
    /// <remarks>
    /// A write that would change nothing raises nothing, which is what lets an edit that turned
    /// out to be a no-op leave no undo step and no redraw.
    /// </remarks>
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

    /// <summary>True when that cell is inside the pattern. The bounds check every edit uses.</summary>
    public bool Contains(int line, int track) =>
        line >= 0 && line < Lines && track >= 0 && track < TrackCount;

    /// <summary>Changes the step count, keeping whatever still fits.</summary>
    public void Resize(int lines) => Rebuild(lines, TrackCount);

    /// <summary>Changes the track count, keeping whatever still fits.</summary>
    public void SetTrackCount(int trackCount) => Rebuild(Lines, trackCount);

    /// <summary>Empties it: every cell, and every lane. One change, not one per cell.</summary>
    public void Clear()
    {
        Array.Fill(_cells, TrackerCell.Empty);
        _lanes.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Empties a track: its notes and the movement of its parameters both.</summary>
    /// <remarks>
    /// Both, because a lane is as much a part of what a track plays as its notes are. Clearing
    /// the notes and leaving the filter still sweeping would be a track that had been emptied
    /// and went on making a noise.
    /// </remarks>
    public void ClearTrack(int track)
    {
        for (int line = 0; line < Lines; line++)
            this[line, track] = TrackerCell.Empty;

        if (_lanes.RemoveAll(one => one.Track == track) > 0)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The lane about that parameter on that track, if there is one.</summary>
    public AutomationLane? LaneFor(ControlMapping mapping, int track) =>
        _lanes.FirstOrDefault(one => one.About(mapping, track));

    /// <summary>Every lane on a track, in the order they were made.</summary>
    public IEnumerable<AutomationLane> LanesOn(int track) =>
        _lanes.Where(one => one.Track == track);

    /// <summary>
    /// Puts a lane in, or hands back the one already there.
    /// </summary>
    /// <remarks>
    /// One lane per parameter per track, so this is the only way one is made: asking twice for
    /// the same parameter gets the same lane, and a second one for it cannot be created by
    /// accident. Renoise's rule, and what stops two envelopes fighting over one knob.
    /// </remarks>
    public AutomationLane Lane(AutomationLane wanted)
    {
        var already = _lanes.FirstOrDefault(one => one.About(wanted.Mapping(), wanted.Track));
        if (already is not null) return already;

        wanted.FitTo(Lines);
        _lanes.Add(wanted);
        Changed?.Invoke(this, EventArgs.Empty);

        return wanted;
    }

    /// <summary>Takes a lane out. The parameter stops moving and stays where it was left.</summary>
    public bool RemoveLane(AutomationLane? lane)
    {
        if (lane is null || !_lanes.Remove(lane)) return false;

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Says a lane's points moved, since the lane itself cannot reach the pattern.</summary>
    /// <remarks>
    /// A lane is edited through its own methods and knows nothing about who holds it, which is
    /// what keeps it testable on its own. So the one call that has to be made afterwards is
    /// here, and it is made by whoever did the editing.
    /// </remarks>
    public void LaneChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Takes a track out of where it is and puts it back in at another position, sliding the
    /// tracks it passes over to fill the gap.
    /// </summary>
    /// <remarks>
    /// A move, not a swap. Dragging track four in front of track one should leave the others
    /// in the order they were, one place along, which is what somebody dragging a column
    /// expects and what a swap does not do.
    ///
    /// The lanes slide with the cells. A lane belongs to a track by number, so a track moved
    /// with its automation left behind would be somebody else's filter sweeping.
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

        foreach (var lane in _lanes)
        {
            if (lane.Track == from) lane.Track = to;
            else if (step > 0 && lane.Track > from && lane.Track <= to) lane.Track--;
            else if (step < 0 && lane.Track >= to && lane.Track < from) lane.Track++;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The cells of one step, left to right. Used by the player, one call per line.</summary>
    public IEnumerable<TrackerCell> Row(int line)
    {
        RequireInRange(line, 0);
        for (int track = 0; track < TrackCount; track++)
            yield return _cells[line * TrackCount + track];
    }

    /// <summary>A pattern of its own holding the same music, with nothing shared.</summary>
    /// <remarks>
    /// The lanes are cloned rather than handed over, since a lane is a reference type edited in
    /// place and two patterns sharing one would move together.
    /// </remarks>
    public Pattern Clone()
    {
        var copy = new Pattern(Lines, TrackCount) { Name = Name };
        Array.Copy(_cells, copy._cells, _cells.Length);
        copy._lanes.AddRange(_lanes.Select(one => one.Clone()));
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

    /// <summary>
    /// The lanes, copied, to be kept beside the cells.
    /// </summary>
    /// <remarks>
    /// Clones and not the lanes themselves. A lane is a reference type and is edited in place,
    /// so a step holding the live one would hold whatever it became rather than what it was,
    /// and undo would put the present back.
    /// </remarks>
    public List<AutomationLane> LaneCopy() =>
        _lanes.Select(one => one.Clone()).ToList();

    /// <summary>True when what it holds now is exactly that.</summary>
    /// <remarks>
    /// What the history asks to find out whether the last edit changed anything, so a keystroke
    /// that did nothing does not cost a step.
    /// </remarks>
    public bool Holds(TrackerCell[]? cells, int lines, int trackCount, IReadOnlyList<AutomationLane>? lanes)
    {
        if (cells is null || lines != Lines || trackCount != TrackCount) return false;
        if (cells.Length != _cells.Length) return false;

        for (int at = 0; at < _cells.Length; at++)
            if (_cells[at] != cells[at]) return false;

        int had = lanes?.Count ?? 0;
        if (had != _lanes.Count) return false;

        for (int at = 0; at < _lanes.Count; at++)
            if (!_lanes[at].Same(lanes![at])) return false;

        return true;
    }

    /// <summary>
    /// Puts a kept copy back, shape and all, and says so once.
    /// </summary>
    /// <remarks>
    /// Once, rather than a change per cell, because putting a step back is one thing that
    /// happened however many cells it touched. Going through the indexer would raise the event a
    /// couple of hundred times and redraw the grid as many.
    ///
    /// The lanes are copied again on the way back in, for the same reason they were copied on
    /// the way out: a step is kept and may be put back more than once, and handing over its own
    /// lanes would let the next edit reach into the history and change what it holds.
    /// </remarks>
    public void Restore(TrackerCell[] cells, int lines, int trackCount,
                        IReadOnlyList<AutomationLane>? lanes)
    {
        if (cells is null) return;

        int newLines = Math.Clamp(lines, MinLines, MaxLines);
        int newTracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);

        if (cells.Length != newLines * newTracks) return;

        _cells = new TrackerCell[cells.Length];
        Array.Copy(cells, _cells, cells.Length);

        _lanes.Clear();
        if (lanes is not null)
            foreach (var lane in lanes) _lanes.Add(lane.Clone());

        Lines = newLines;
        TrackCount = newTracks;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Changes the shape, keeping whatever still fits, and says so once.
    /// </summary>
    /// <remarks>
    /// The lanes follow the same rule the cells do. A pattern made shorter drops the points past
    /// its end, and tracks taken off take their lanes with them. The master's lane stays: it is
    /// not one of the tracks and there is no count of them that reaches it.
    /// </remarks>
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

        _lanes.RemoveAll(one => !one.IsMaster && one.Track >= newTracks);
        foreach (var lane in _lanes) lane.FitTo(newLines);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A block of blank cells of that shape.</summary>
    private static TrackerCell[] NewCells(int lines, int trackCount)
    {
        var cells = new TrackerCell[lines * trackCount];
        Array.Fill(cells, TrackerCell.Empty);
        return cells;
    }

    /// <summary>Throws for a cell outside the pattern, naming the shape it was asked of.</summary>
    private void RequireInRange(int line, int track)
    {
        if (!Contains(line, track))
            throw new ArgumentOutOfRangeException(
                nameof(line), $"Cell {line},{track} is outside a {Lines}x{TrackCount} pattern.");
    }
}
