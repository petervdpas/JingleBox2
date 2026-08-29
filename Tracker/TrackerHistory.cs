using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Two lists and a running total of what they cost, with the two kinds of step behind one
/// abstract type so undo does not have to know which it is holding.
/// </remarks>
public sealed class TrackerHistory : ITrackerHistory
{
    /// <summary>
    /// How many steps are kept.
    /// </summary>
    /// <remarks>
    /// A hundred is about ten minutes of typing and a few hundred kilobytes for a pattern of
    /// the usual size. The worst a pattern can be is two hundred and fifty six lines by
    /// thirty two tracks, and a hundred of those would be a hundred and eighty megabytes, so
    /// there is a ceiling on the whole lot as well as a count.
    /// </remarks>
    public const int MostSteps = 100;

    /// <summary>And the ceiling, so a very large pattern keeps fewer steps rather than all of them.</summary>
    public const long MostBytes = 32L * 1024 * 1024;

    /// <summary>What was done, oldest first, so the last is what undo takes back.</summary>
    private readonly List<Step> _done = new();

    /// <summary>What was undone, which is emptied the moment something new is done.</summary>
    private readonly List<Step> _undone = new();

    /// <summary>Roughly what both lists cost, kept as they are pushed and dropped.</summary>
    private long _bytes;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <summary>One thing that was done, of whichever kind.</summary>
    private abstract class Step
    {
        /// <summary>How it is named in a menu or a log line.</summary>
        public string What = "";

        /// <summary>Roughly what it costs to keep, for the ceiling.</summary>
        public abstract long Bytes { get; }

        /// <summary>Puts this state back. False when it cannot, which costs the step only.</summary>
        public abstract bool Put();

        /// <summary>The same thing as it stands now, so the other direction has somewhere to go.</summary>
        public abstract Step Now();
    }

    /// <summary>A pattern, as its cells. What typing produces, and it has to be cheap.</summary>
    private sealed class PatternStep : Step
    {
        /// <summary>Which pattern this is about, held by reference so undo can go back to it.</summary>
        /// <remarks>
        /// Which is why <see cref="Song.TakeFrom"/> fills the patterns it already has rather than
        /// replacing the list: a replaced list leaves every step here pointing at an orphan, and
        /// undoing a note after undoing an instrument then silently does nothing.
        /// </remarks>
        public Pattern Pattern = null!;

        /// <summary>Its cells as they were, copied out of the pattern's own array.</summary>
        public TrackerCell[] Cells = Array.Empty<TrackerCell>();

        /// <summary>How many lines it had, since a step can put a resize back too.</summary>
        public int Lines;

        /// <summary>And how many tracks.</summary>
        public int TrackCount;

        /// <summary>
        /// And how many note columns each of them had.
        /// </summary>
        /// <remarks>
        /// Part of the shape, so a step has to carry it for the same reason it carries the line
        /// and track counts: without it an undo across a change of column count would find a
        /// step whose cells are the wrong length, refuse to put it back and say nothing, which
        /// is a bug this codebase has had twice and both times because doing nothing looks like
        /// working.
        /// </remarks>
        public int[] Columns = Array.Empty<int>();

        /// <summary>
        /// And the movement, which is part of the pattern and had to be part of the step.
        /// </summary>
        /// <remarks>
        /// Kept beside the cells rather than folded into them because it is a different shape,
        /// and counted separately in <see cref="Bytes"/> because it can be the larger of the
        /// two: a pattern of empty cells with a sweep recorded across it is mostly points.
        ///
        /// A pattern with no lanes carries an empty list here, which is what almost every step
        /// will be, and it costs the list and nothing else.
        ///
        /// Left out, undo would put the notes back and leave the movement where it was.
        /// </remarks>
        public List<AutomationLane> Lanes = new();

        /// <summary>
        /// The cells at twenty four bytes apiece and the points at sixteen.
        /// </summary>
        /// <remarks>
        /// Both numbers are the shape of the value types rather than anything measured, and both
        /// only have to be near enough: this feeds a ceiling on how much is kept, not an
        /// allocator.
        /// </remarks>
        public override long Bytes => Cells.LongLength * 24 + Points * 16;

        /// <summary>How many points there are across every lane in the step.</summary>
        private long Points
        {
            get
            {
                long count = 0;
                foreach (var lane in Lanes) count += lane.Points.Count;

                return count;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Always true: a pattern cannot refuse its own cells back.</remarks>
        public override bool Put()
        {
            Pattern.Restore(Cells, Lines, TrackCount, Columns, Lanes);

            return true;
        }

        /// <inheritdoc/>
        public override Step Now() => Of(Pattern, What);

        /// <summary>A step holding what that pattern is now.</summary>
        public static PatternStep Of(Pattern pattern, string what) => new()
        {
            Pattern = pattern,
            Cells = pattern.Cells(),
            Lines = pattern.Lines,
            TrackCount = pattern.TrackCount,
            Columns = pattern.ColumnCounts(),
            Lanes = pattern.LaneCopy(),
            What = what
        };
    }

    /// <summary>
    /// The whole song, as its own file would hold it.
    /// </summary>
    /// <remarks>
    /// For the edits that are not about one pattern: an instrument added or taken out, the
    /// order, how many tracks there are. Taking an instrument out renumbers every pattern that
    /// referred to it, so nothing smaller than the document would put it back.
    ///
    /// It cannot put the song object back, because everything in the tracker holds that one. So
    /// it hands the read-back song to whoever is looking after the live one, and that is the
    /// only part of this that the tracker has to do for itself.
    /// </remarks>
    private sealed class SongStep : Step
    {
        /// <summary>The live song, which is what the read-back one is poured into.</summary>
        public Song Song = null!;

        /// <summary>The song as its own file would hold it, which is the step itself.</summary>
        public string Said = "";

        /// <summary>How to pour a read-back song onto the live one.</summary>
        public Func<Song, Song, bool> Onto = null!;

        /// <inheritdoc/>
        /// <remarks>The text, which is what is actually being kept.</remarks>
        public override long Bytes => Said.Length;

        /// <inheritdoc/>
        /// <remarks>False when the text will not read back, which costs this step and no other.</remarks>
        public override bool Put()
        {
            var was = SongStore.Uncopy(Said);

            return was is not null && Onto(Song, was);
        }

        /// <inheritdoc/>
        public override Step Now() => Of(Song, What, Onto);

        /// <summary>A step holding the song as it is now.</summary>
        public static SongStep Of(Song song, string what, Func<Song, Song, bool> onto) => new()
        {
            Song = song,
            Said = SongStore.Copy(song),
            Onto = onto,
            What = what
        };
    }

    /// <inheritdoc/>
    public bool CanUndo => _done.Count > 0;

    /// <inheritdoc/>
    public bool CanRedo => _undone.Count > 0;

    /// <inheritdoc/>
    public string NextUndo => _done.Count > 0 ? _done[^1].What : "";

    /// <inheritdoc/>
    public string NextRedo => _undone.Count > 0 ? _undone[^1].What : "";

    /// <inheritdoc/>
    public void Taking(Pattern? pattern, string what)
    {
        if (pattern is null) return;

        if (_done.Count > 0
            && _done[^1] is PatternStep last
            && ReferenceEquals(last.Pattern, pattern)
            && pattern.Holds(last.Cells, last.Lines, last.TrackCount, last.Columns, last.Lanes))
        {
            last.What = what;

            return;
        }

        _gathering = "";

        Push(PatternStep.Of(pattern, what));
    }

    /// <inheritdoc/>
    public void Taking(Song? song, string what, Func<Song, Song, bool> onto)
    {
        if (song is null || onto is null) return;

        var at = _since.Elapsed;

        bool same = _gathering == what && at - _last < SameGesture;

        _gathering = what;
        _last = at;

        if (same) return;

        Push(SongStep.Of(song, what, onto));
    }

    /// <summary>How long an edit of the same kind stays the same gesture.</summary>
    /// <remarks>
    /// Deliberately a length of time rather than "while the mouse is down", which is true of a
    /// mouse and false of a controller and of automation.
    /// </remarks>
    public static readonly TimeSpan SameGesture = TimeSpan.FromMilliseconds(500);

    /// <summary>A clock of its own, so gathering does not depend on the wall clock being sane.</summary>
    private readonly System.Diagnostics.Stopwatch _since = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>What is being gathered now, by its description. Empty when nothing is.</summary>
    private string _gathering = "";

    /// <summary>When the gathered edit was last added to.</summary>
    private TimeSpan _last;

    /// <summary>
    /// Puts a step on the done list and drops whatever was undone.
    /// </summary>
    /// <remarks>
    /// Doing something new is what makes what was undone unreachable. Anything else would mean a
    /// redo that puts back an edit somebody has since typed over.
    /// </remarks>
    private void Push(Step step)
    {
        _done.Add(step);
        _bytes += step.Bytes;

        if (_undone.Count > 0) _undone.Clear();

        Trim();

        Changed?.Invoke();
    }

    /// <inheritdoc/>
    public bool Undo() => Walk(_done, _undone, "undid");

    /// <inheritdoc/>
    public bool Redo() => Walk(_undone, _done, "did again");

    /// <summary>
    /// Moves one step from one list to the other, putting its state back on the way.
    /// </summary>
    /// <remarks>
    /// Where the pattern or the song is now is taken before anything is put back, so the other
    /// direction has somewhere to go: undo and redo are the same walk in opposite directions.
    ///
    /// A step that will not go back is dropped rather than left at the top for somebody to press
    /// again and again. Everything under it is still good.
    ///
    /// Whatever was being gathered is over either way, since what is under the hand is no longer
    /// where it was.
    /// </remarks>
    private bool Walk(List<Step> from, List<Step> onto, string said)
    {
        if (from.Count == 0) return false;

        var step = from[^1];

        var back = step.Now();

        if (!step.Put())
        {
            from.RemoveAt(from.Count - 1);
            _bytes -= step.Bytes;

            Log.Write(LogArea.Tracker, () => "history: could not " + said + " " + step.What + ", so that step is gone");

            Changed?.Invoke();

            return false;
        }

        from.RemoveAt(from.Count - 1);
        _bytes -= step.Bytes;

        onto.Add(back);
        _bytes += back.Bytes;

        _gathering = "";

        Log.Write(LogArea.Tracker, () => "history: " + said + " " + step.What);

        Changed?.Invoke();

        return true;
    }

    /// <inheritdoc/>
    public Pattern? UndoIsAbout => _done.Count > 0 && _done[^1] is PatternStep one ? one.Pattern : null;

    /// <inheritdoc/>
    public Pattern? RedoIsAbout => _undone.Count > 0 && _undone[^1] is PatternStep one ? one.Pattern : null;

    /// <inheritdoc/>
    public void Forget()
    {
        if (_done.Count == 0 && _undone.Count == 0) return;

        _done.Clear();
        _undone.Clear();
        _bytes = 0;

        Changed?.Invoke();
    }

    /// <summary>
    /// Drops the oldest steps until it is within both of its limits.
    /// </summary>
    /// <remarks>
    /// The last step is never dropped for weight, however heavy it is. One step is the least a
    /// history can be and still be one, and a pattern at its largest is over the ceiling on its
    /// own.
    /// </remarks>
    private void Trim()
    {
        while (_done.Count > MostSteps || (_bytes > MostBytes && _done.Count > 1))
        {
            _bytes -= _done[0].Bytes;
            _done.RemoveAt(0);
        }
    }
}
