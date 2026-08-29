using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Records;

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
    /// out from the counts beside it and every one of them moves. Read through
    /// <see cref="_layout"/>, which is what makes that replacement one step rather than three.
    /// </remarks>
    private TrackerCell[] _cells => _layout.Cells;

    /// <summary>
    /// The cells and the shape they are laid out in, held as one thing and swapped as one.
    /// </summary>
    /// <remarks>
    /// The three used to be three fields, and changing the shape wrote them one after another.
    /// The clock thread reads all three to work out where a cell is, so a pass running while
    /// somebody added a track or a note column could read the new running total against the old
    /// array and walk off the end of it. Swapping one reference cannot be caught half done: a
    /// reader takes the object it finds and everything in it agrees with everything else.
    ///
    /// Writing a cell still writes into whichever array was current when the write began, so a
    /// write racing a reshape is lost rather than misplaced. Every write comes from the drawing
    /// thread and every reshape with it, so that race does not arise; the read is the one that
    /// crosses threads.
    /// </remarks>
    private Layout _layout;

    /// <summary>Everything about where a cell sits, so that it can be replaced in one go.</summary>
    /// <param name="Cells">Every cell, line by line.</param>
    /// <param name="Columns">How many note columns each track has.</param>
    /// <param name="Starts">Where each track's columns begin in a line, with the stride on the end.</param>
    private sealed record Layout(TrackerCell[] Cells, int[] Columns, int[] Starts);

    /// <summary>
    /// How many note columns each track has, one entry per track.
    /// </summary>
    /// <remarks>
    /// A copy of what the song says, for the reason <see cref="TrackCount"/> is a copy: the
    /// place of a cell is worked out from these on every read, and reaching to the song for
    /// them would put a reference in the one place that has to stay a plain array lookup.
    ///
    /// Every pattern in a song has the same counts, because a part is played on so many voices
    /// whatever pattern it is in. Counts that varied per pattern would make copying a track
    /// between patterns a question with no good answer.
    ///
    /// Beside the cells in <see cref="_layout"/> rather than beside them as a field, so the two
    /// cannot be read a shape apart.
    /// </remarks>
    private int[] _columns => _layout.Columns;

    /// <summary>
    /// Where each track's columns begin within one line, with the row's own total on the end.
    /// </summary>
    /// <remarks>
    /// A running total, made whenever the shape changes, so a cell's place is an addition
    /// rather than a walk. One longer than there are tracks: the last entry is the stride, and
    /// having it there means the width of the last track needs no special case.
    /// </remarks>
    private int[] _starts => _layout.Starts;

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

    /// <summary>How many cells one line holds, which is every track's columns added up.</summary>
    public int TotalColumns => _starts[TrackCount];

    /// <summary>How many note columns a track has. Nought for a track that is not there.</summary>
    public int ColumnsOn(int track)
    {
        var columns = _layout.Columns;

        return track >= 0 && track < columns.Length ? columns[track] : 0;
    }

    /// <summary>
    /// The counts, to be kept: what a history step and a clone carry beside the cells.
    /// </summary>
    /// <remarks>
    /// A copy rather than the array, since the shape is what a step is compared against and a
    /// step holding the live counts would hold whatever they became.
    /// </remarks>
    public int[] ColumnCounts()
    {
        var kept = new int[TrackCount];
        Array.Copy(_columns, kept, TrackCount);

        return kept;
    }

    /// <summary>Gives a track that many note columns, keeping whatever still fits.</summary>
    public void SetColumns(int track, int count)
    {
        if (track < 0 || track >= TrackCount) return;

        int wanted = Math.Clamp(count, Song.MinNoteColumns, Song.MaxNoteColumns);
        if (wanted == _columns[track]) return;

        var columns = ColumnCounts();
        columns[track] = wanted;

        Rebuild(Lines, TrackCount, columns);
    }

    /// <summary>Sets every track's count at once, which is what a song pushes down.</summary>
    public void SetColumns(IReadOnlyList<int>? counts) => Rebuild(Lines, TrackCount, counts);

    /// <summary>A pattern of that shape, every cell blank. Both numbers are held to their range.</summary>
    public Pattern(int lines = DefaultLines, int trackCount = Song.DefaultTrackCount)
    {
        Lines = Math.Clamp(lines, MinLines, MaxLines);
        TrackCount = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);

        var columns = Widths(null, TrackCount);
        var starts = Starts(columns);

        _layout = new Layout(NewCells(Lines, starts[TrackCount]), columns, starts);
    }

    /// <summary>Where a cell sits in the block. The one piece of arithmetic every read goes through.</summary>
    /// <remarks>
    /// Asked of a layout rather than of the pattern, so that a caller that has already taken one
    /// cannot be handed a different shape half way through working out where its cell is.
    /// </remarks>
    private int Place(Layout layout, int line, int track, int column) =>
        line * layout.Starts[TrackCount] + layout.Starts[track] + column;

    /// <summary>A count per track, held to its range, filled out or cut down to fit.</summary>
    /// <remarks>
    /// A track the caller said nothing about gets one column, which is what every track had
    /// before there was more than one and is what a song written before this reads back as.
    /// </remarks>
    private static int[] Widths(IReadOnlyList<int>? counts, int trackCount)
    {
        var widths = new int[trackCount];

        for (int track = 0; track < trackCount; track++)
        {
            int said = counts is not null && track < counts.Count ? counts[track] : Song.DefaultNoteColumns;

            widths[track] = Math.Clamp(said, Song.MinNoteColumns, Song.MaxNoteColumns);
        }

        return widths;
    }

    /// <summary>The running total of those widths, with the stride on the end.</summary>
    private static int[] Starts(int[] columns)
    {
        var starts = new int[columns.Length + 1];

        for (int track = 0; track < columns.Length; track++)
            starts[track + 1] = starts[track] + columns[track];

        return starts;
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
        get => this[line, track, 0];
        set => this[line, track, 0] = value;
    }

    /// <summary>
    /// One cell of one note column. The first column is the track itself, which is what every
    /// caller that names only a track means.
    /// </summary>
    /// <remarks>
    /// Reading or writing outside the pattern throws, because that is a mistake in the caller
    /// rather than an ordinary state, and a write that would change nothing raises nothing.
    /// </remarks>
    public TrackerCell this[int line, int track, int column]
    {
        get
        {
            var layout = _layout;

            RequireInRange(line, track, column);

            return layout.Cells[Place(layout, line, track, column)];
        }
        set
        {
            var layout = _layout;

            RequireInRange(line, track, column);

            int index = Place(layout, line, track, column);
            if (layout.Cells[index] == value) return;

            layout.Cells[index] = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>True when that cell is inside the pattern. The bounds check every edit uses.</summary>
    public bool Contains(int line, int track) => Contains(line, track, 0);

    /// <summary>The same, of one note column of a track.</summary>
    public bool Contains(int line, int track, int column)
    {
        var columns = _layout.Columns;

        return line >= 0 && line < Lines
               && track >= 0 && track < TrackCount
               && column >= 0 && column < columns[track];
    }

    /// <summary>Changes the step count, keeping whatever still fits.</summary>
    public void Resize(int lines) => Rebuild(lines, TrackCount, _columns);

    /// <summary>Changes the track count, keeping whatever still fits.</summary>
    public void SetTrackCount(int trackCount) => Rebuild(Lines, trackCount, _columns);

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
            for (int column = 0; column < ColumnsOn(track); column++)
                this[line, track, column] = TrackerCell.Empty;

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
    ///
    /// The block is rebuilt rather than shuffled in place, because two tracks need not be the
    /// same width any more: a track of three note columns moved in front of a track of one is
    /// not a swap of equal pieces, and the row it lands in is a different length in every line.
    /// The note columns travel with their track, which is the whole point of them belonging to
    /// it.
    /// </remarks>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= TrackCount || to < 0 || to >= TrackCount) return;

        var order = new int[TrackCount];
        for (int track = 0; track < TrackCount; track++) order[track] = track;

        int step = from < to ? 1 : -1;
        for (int track = from; track != to; track += step) order[track] = order[track + step];
        order[to] = from;

        var columns = new int[TrackCount];
        for (int track = 0; track < TrackCount; track++) columns[track] = _columns[order[track]];

        var starts = Starts(columns);
        var replacement = NewCells(Lines, starts[TrackCount]);

        for (int line = 0; line < Lines; line++)
        {
            for (int track = 0; track < TrackCount; track++)
            {
                for (int column = 0; column < columns[track]; column++)
                    replacement[line * starts[TrackCount] + starts[track] + column] =
                        _cells[Place(_layout, line, order[track], column)];
            }
        }

        _layout = new Layout(replacement, columns, starts);

        foreach (var lane in _lanes)
        {
            if (lane.Track == from) lane.Track = to;
            else if (step > 0 && lane.Track > from && lane.Track <= to) lane.Track--;
            else if (step < 0 && lane.Track >= to && lane.Track < from) lane.Track++;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Every cell of one step, left to right: each track's columns in turn.</summary>
    public IEnumerable<TrackerCell> Row(int line)
    {
        RequireInRange(line, 0, 0);

        var layout = _layout;

        for (int track = 0; track < TrackCount; track++)
            for (int column = 0; column < layout.Columns[track]; column++)
                yield return layout.Cells[Place(layout, line, track, column)];
    }

    /// <summary>A pattern of its own holding the same music, with nothing shared.</summary>
    /// <remarks>
    /// The lanes are cloned rather than handed over, since a lane is a reference type edited in
    /// place and two patterns sharing one would move together.
    /// </remarks>
    public Pattern Clone()
    {
        var copy = new Pattern(Lines, TrackCount) { Name = Name };

        var columns = ColumnCounts();
        var cells = new TrackerCell[_cells.Length];

        Array.Copy(_cells, cells, cells.Length);

        copy._layout = new Layout(cells, columns, Starts(columns));
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
    public bool Holds(TrackerCell[]? cells, int lines, int trackCount, IReadOnlyList<int>? columns,
                      IReadOnlyList<AutomationLane>? lanes)
    {
        if (cells is null || lines != Lines || trackCount != TrackCount) return false;
        if (cells.Length != _cells.Length) return false;

        if (columns is null || columns.Count != TrackCount) return false;

        for (int track = 0; track < TrackCount; track++)
            if (columns[track] != _columns[track]) return false;

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
    public void Restore(TrackerCell[] cells, int lines, int trackCount, IReadOnlyList<int>? columns,
                        IReadOnlyList<AutomationLane>? lanes)
    {
        if (cells is null) return;

        int newLines = Math.Clamp(lines, MinLines, MaxLines);
        int newTracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);

        var widths = Widths(columns, newTracks);
        var starts = Starts(widths);

        if (cells.Length != newLines * starts[newTracks]) return;

        var kept = new TrackerCell[cells.Length];
        Array.Copy(cells, kept, cells.Length);

        _layout = new Layout(kept, widths, starts);

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
    private void Rebuild(int lines, int trackCount, IReadOnlyList<int>? columns)
    {
        int newLines = Math.Clamp(lines, MinLines, MaxLines);
        int newTracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);

        var widths = Widths(columns, newTracks);
        var starts = Starts(widths);

        if (newLines == Lines && newTracks == TrackCount && Same(widths)) return;

        var replacement = NewCells(newLines, starts[newTracks]);

        int keptLines = Math.Min(Lines, newLines);
        int keptTracks = Math.Min(TrackCount, newTracks);

        for (int line = 0; line < keptLines; line++)
        {
            for (int track = 0; track < keptTracks; track++)
            {
                int kept = Math.Min(_columns[track], widths[track]);

                for (int column = 0; column < kept; column++)
                    replacement[line * starts[newTracks] + starts[track] + column] =
                        _cells[Place(_layout, line, track, column)];
            }
        }

        _layout = new Layout(replacement, widths, starts);
        Lines = newLines;
        TrackCount = newTracks;

        _lanes.RemoveAll(one => !one.IsMaster && one.Track >= newTracks);
        foreach (var lane in _lanes) lane.FitTo(newLines);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when the counts it has now are exactly those.</summary>
    private bool Same(int[] widths)
    {
        if (widths.Length != _columns.Length) return false;

        for (int track = 0; track < widths.Length; track++)
            if (widths[track] != _columns[track]) return false;

        return true;
    }

    /// <summary>A block of blank cells: that many lines of that many cells each.</summary>
    private static TrackerCell[] NewCells(int lines, int stride)
    {
        var cells = new TrackerCell[lines * stride];
        Array.Fill(cells, TrackerCell.Empty);
        return cells;
    }

    /// <summary>Throws for a cell outside the pattern, naming the shape it was asked of.</summary>
    private void RequireInRange(int line, int track, int column)
    {
        if (!Contains(line, track, column))
            throw new ArgumentOutOfRangeException(
                nameof(line),
                $"Cell {line},{track},{column} is outside a {Lines}x{TrackCount} pattern.");
    }
}
