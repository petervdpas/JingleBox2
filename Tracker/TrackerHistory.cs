using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;

namespace JingleBox2.Tracker;

/// <summary>
/// What was done in the tracker, so it can be undone.
/// </summary>
/// <remarks>
/// Whole copies rather than a description of each change, which is the right trade here and not
/// everywhere. A pattern is one array of value types with no allocation per cell, so a step is
/// a memory copy of a few kilobytes for an ordinary pattern; describing an edit instead would
/// mean a type per operation, an inverse for each, and the certainty that the nineteenth one
/// written would forget its inverse and undo would quietly corrupt a song. A copy cannot be
/// wrong about what it holds.
///
/// The unit is one call to <see cref="PatternEdit"/>, which is the whole reason that class is
/// the only door: one edit, one step, and a page of typing is a page of undos rather than one.
/// An edit that changed nothing leaves no step, worked out by noticing that the pattern still
/// holds what the last step kept, so a key that did nothing does not have to be undone.
///
/// Every step remembers which pattern it belongs to. Undo after switching patterns puts the
/// right one back and says which, rather than silently editing the one you are looking at.
///
/// It holds the song's patterns, so it is emptied when the song changes. A history that
/// outlived its song would hand somebody another song's notes.
///
/// Two kinds of step, one history, because Ctrl+Z means the last thing you did and not the last
/// thing you did of a particular kind. Typing a note is a pattern, and a step is its cells.
/// Taking an instrument out of a song is not: it renumbers every pattern that referred to it,
/// which is an edit across the whole document, so a step there is the song as its own file would
/// hold it. Keeping those apart in two histories would give one keystroke two meanings and a
/// person no way of knowing which they were about to get.
///
/// The kinds cost very different amounts, which is why they are kept apart at all. A pattern's
/// cells are a memory copy of a few kilobytes; a song is twelve to eighty. Serialising the whole
/// song for every keystroke would work and would be wasteful in exactly the place that must not
/// be.
/// </remarks>
public sealed class TrackerHistory
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

    private readonly List<Step> _done = new();
    private readonly List<Step> _undone = new();

    private long _bytes;

    /// <summary>Raised when there is something different to say about what can be undone.</summary>
    public event Action? Changed;

    /// <summary>One thing that was done, of whichever kind.</summary>
    private abstract class Step
    {
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
        public Pattern Pattern = null!;
        public TrackerCell[] Cells = Array.Empty<TrackerCell>();
        public int Lines;
        public int TrackCount;

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
        /// </remarks>
        public List<AutomationLane> Lanes = new();

        public override long Bytes => Cells.LongLength * 24 + Points * 16;

        private long Points
        {
            get
            {
                long count = 0;
                foreach (var lane in Lanes) count += lane.Points.Count;

                return count;
            }
        }

        public override bool Put()
        {
            Pattern.Restore(Cells, Lines, TrackCount, Lanes);

            return true;
        }

        public override Step Now() => Of(Pattern, What);

        public static PatternStep Of(Pattern pattern, string what) => new()
        {
            Pattern = pattern,
            Cells = pattern.Cells(),
            Lines = pattern.Lines,
            TrackCount = pattern.TrackCount,
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
        public Song Song = null!;
        public string Said = "";
        public Func<Song, Song, bool> Onto = null!;

        public override long Bytes => Said.Length;

        public override bool Put()
        {
            var was = SongStore.Uncopy(Said);

            return was is not null && Onto(Song, was);
        }

        public override Step Now() => Of(Song, What, Onto);

        public static SongStep Of(Song song, string what, Func<Song, Song, bool> onto) => new()
        {
            Song = song,
            Said = SongStore.Copy(song),
            Onto = onto,
            What = what
        };
    }

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    /// <summary>What undo would take back, for a menu or a tooltip.</summary>
    public string NextUndo => _done.Count > 0 ? _done[^1].What : "";

    /// <summary>And what redo would put back.</summary>
    public string NextRedo => _undone.Count > 0 ? _undone[^1].What : "";

    /// <summary>
    /// Something is about to be done to a pattern. Called before it happens, not after.
    /// </summary>
    /// <remarks>
    /// Before, because what has to be kept is the state being left rather than the one being
    /// arrived at, and afterwards the first is gone.
    /// </remarks>
    public void Taking(Pattern? pattern, string what)
    {
        if (pattern is null) return;

        // The edit before this one changed nothing, so its step is worth no more than this
        // one's and would cost somebody a keystroke to walk past. Reused rather than pushed.
        if (_done.Count > 0
            && _done[^1] is PatternStep last
            && ReferenceEquals(last.Pattern, pattern)
            && pattern.Holds(last.Cells, last.Lines, last.TrackCount, last.Lanes))
        {
            last.What = what;

            return;
        }

        _gathering = "";

        Push(PatternStep.Of(pattern, what));
    }

    /// <summary>
    /// The song itself is about to change: an instrument, the order, how many tracks.
    /// </summary>
    /// <remarks>
    /// Called before, like the other one, and for the same reason. What separates these from a
    /// pattern edit is not how big they are but what they reach: taking an instrument out of a
    /// song renumbers every pattern that referred to it, and no snapshot of one pattern would
    /// put that back.
    /// </remarks>
    /// <param name="onto">
    /// How to put a read-back song onto the one that is open. Everything in the tracker holds
    /// the live song, so a step cannot hand back a different object; the tracker knows how to
    /// pour one into the other and this does not.
    /// </param>
    public void Taking(Song? song, string what, Func<Song, Song, bool> onto)
    {
        if (song is null || onto is null) return;

        // Gathered by what it is and when, the same rule the instrument panel's knobs use, and
        // for the same reason: a fader dragged across its range says the mix changed a hundred
        // times and is one thing a person did. The name is the control here rather than a
        // parameter key, which is enough: two different edits do not share a description.
        var at = _since.Elapsed;

        bool same = _gathering == what && at - _last < SameGesture;

        _gathering = what;
        _last = at;

        if (same) return;

        Push(SongStep.Of(song, what, onto));
    }

    /// <summary>How long an edit of the same kind stays the same gesture.</summary>
    public static readonly TimeSpan SameGesture = TimeSpan.FromMilliseconds(500);

    private readonly System.Diagnostics.Stopwatch _since = System.Diagnostics.Stopwatch.StartNew();

    private string _gathering = "";
    private TimeSpan _last;

    private void Push(Step step)
    {
        _done.Add(step);
        _bytes += step.Bytes;

        // Doing something new is what makes what was undone unreachable. Anything else would
        // mean a redo that puts back an edit somebody has since typed over.
        if (_undone.Count > 0) _undone.Clear();

        Trim();

        Changed?.Invoke();
    }

    /// <summary>Takes the last edit back. False when there is nothing to take back.</summary>
    public bool Undo() => Walk(_done, _undone, "undid");

    /// <summary>Puts back the last thing undone.</summary>
    public bool Redo() => Walk(_undone, _done, "did again");

    private bool Walk(List<Step> from, List<Step> onto, string said)
    {
        if (from.Count == 0) return false;

        var step = from[^1];

        // Where it is now, taken before anything is put back, so the other direction has
        // somewhere to go.
        var back = step.Now();

        if (!step.Put())
        {
            // A step that will not go back is dropped rather than left at the top for somebody
            // to press again and again. Everything under it is still good.
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

        // Whatever was being gathered is over: what is under the hand is not where it was.
        _gathering = "";

        Log.Write(LogArea.Tracker, () => "history: " + said + " " + step.What);

        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Which pattern the next undo is about, so the view can go there first.
    /// </summary>
    /// <remarks>
    /// Nothing for a step about the song itself, which is not about one pattern and needs the
    /// view to stay where it is.
    /// </remarks>
    public Pattern? UndoIsAbout => _done.Count > 0 && _done[^1] is PatternStep one ? one.Pattern : null;

    /// <summary>And the next redo.</summary>
    public Pattern? RedoIsAbout => _undone.Count > 0 && _undone[^1] is PatternStep one ? one.Pattern : null;

    /// <summary>Empties it. For a song being closed, or opened.</summary>
    public void Forget()
    {
        if (_done.Count == 0 && _undone.Count == 0) return;

        _done.Clear();
        _undone.Clear();
        _bytes = 0;

        Changed?.Invoke();
    }

    /// <summary>Drops the oldest steps until it is within both of its limits.</summary>
    private void Trim()
    {
        while (_done.Count > MostSteps || (_bytes > MostBytes && _done.Count > 1))
        {
            _bytes -= _done[0].Bytes;
            _done.RemoveAt(0);
        }
    }
}
